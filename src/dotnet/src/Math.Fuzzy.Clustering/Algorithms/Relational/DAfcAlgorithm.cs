using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Relational;

/// <summary>
/// D-AFC(c) — the direct algorithm for detecting the allotment among c fuzzy clusters.
///
/// Semantics (Viattchenin 2013, sec. 2.2.1): with <see cref="DAfcOptions.AutoIntersections"/>
/// the search starts from fully separated clusters (w = 0) and relaxes the separation
/// condition one object at a time only while no allotment among exactly c clusters
/// exists. Results are returned in the deterministic order <see cref="ClusterResult.Order"/>.
///
/// Performance design (alpha-centric):
///   • Phase 0 (level pre-filter): per alpha, object powers are computed in O(log n) each
///     from per-object descending-sorted rows. A level survives only if it has ≥ c valid
///     center candidates (power ∈ [minP, min(maxP, n−c+1)]), can cover all objects
///     (maxCandPower·c ≥ n) and does not structurally overflow the intersection budget
///     (minCandPower·c − n ≤ w). For real data this rejects most levels outright.
///   • Phase 1 (bitset engine): on each surviving level, α-cuts of the candidates are
///     materialised as bitsets (AVX2 compare + movemask, 4 doubles → 4 bits per op).
///     Combination search is a DFS where:
///       – center disjointness is a single bit test,
///       – coverage is a word-wise OR with an incremental PopCount,
///       – a branch &amp; bound on the suffix-maximum candidate power prunes subtrees
///         that can no longer cover all n objects,
///       – an exact-cover bound prunes subtrees whose cluster powers cannot add up:
///         with at most w objects in intersection areas the powers of the c clusters sum
///         to a value in [n, n + (c−1)·w] (to exactly n for fully separated clusters), and a
///         subset-sum table over the remaining candidates tells in O(1) whether that is
///         still possible. The same test rejects whole levels in Phase 0.
///       – for w = 0 two candidates with a common neighbour are never centers together
///         (a fully separated allotment is an efficient dominating set of the α-cut graph).
///   • Levels are processed in parallel, descending by alpha, in chunks of
///     EarlyStopChunkSize; when EarlyStop is on, the scan stops after the first chunk
///     that yields no score improvement (once at least one solution exists).
/// </summary>
public sealed class DAfcAlgorithm(
    DAfcOptions    options,
    IAlphaSelector alphaSelector,
    ICriterion     criterion) : IRelationalClusterer
{
    // One int[] per OS thread; re-used across solutions to avoid per-call heap alloc.
    [ThreadStatic] private static int[]? _intersectionScratch;
    [ThreadStatic] private static ulong[]? _reachScratch;

    /// <summary>A pre-validated alpha level: candidate centers and their cluster powers.</summary>
    private readonly record struct AlphaLevel(double Alpha, int[] Candidates, int[] Powers);

    public IReadOnlyList<ClusterResult> Run(FuzzyToleranceMatrix matrix, CancellationToken ct = default)
    {
        int n = matrix.Size;
        int c = options.ClusterCount;
        if (c < 2 || c > n) return [];

        ct.ThrowIfCancellationRequested();

        // ── Phase 0: per-object descending-sorted rows → O(log n) power queries ──────
        var sortedRows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var arr = matrix.GetRow(i).ToArray();
            Array.Sort(arr);
            Array.Reverse(arr); // descending
            sortedRows[i] = arr;
        }

        var alphas = alphaSelector.Select(matrix).OrderByDescending(a => a).ToArray();

        if (options.MaxIntersections >= 0)
            return ScanAllLevels(matrix, sortedRows, alphas, n, c, options.MaxIntersections, ct, out _);

        // Original D-AFC(c), steps 3–5: w := 0; if no allotment among exactly c
        // clusters exists at any level, w := w + 1 and repeat. An exhausted node budget
        // proves nothing about the current w, so the relaxation stops there.
        for (int w = 0; w <= n; w++)
        {
            var found = ScanAllLevels(matrix, sortedRows, alphas, n, c, w, ct, out bool budgetExhausted);
            if (found.Count > 0 || budgetExhausted) return found;
        }
        return [];
    }

    private IReadOnlyList<ClusterResult> ScanAllLevels(
        FuzzyToleranceMatrix matrix, double[][] sortedRows, double[] alphas,
        int n, int c, int maxIntersections, CancellationToken ct, out bool budgetExhausted)
    {
        var budget = new SearchBudget(options.MaxCombinations);

        var levelSlots = new AlphaLevel?[alphas.Length];
        Parallel.For(0, alphas.Length,
            new ParallelOptions { CancellationToken = ct },
            () => new int[n], // per-thread power buffer
            (idx, _, powerBuffer) =>
            {
                for (int i = 0; i < n; i++)
                    powerBuffer[i] = CountAtLeastDescending(sortedRows[i], alphas[idx]);
                if (TryCreateLevel(alphas[idx], powerBuffer, n, c, maxIntersections, out var level))
                    levelSlots[idx] = level;
                return powerBuffer;
            },
            static _ => { });
        var levels = levelSlots.Where(l => l.HasValue).Select(l => l!.Value).ToList();

        // ── Phase 1: bitset combination search per level, parallel, chunked ──────────
        var collector = new TopNCollector(options.MaxResults);
        var parallelOptions = new ParallelOptions { CancellationToken = ct };

        // Scan-progress: one Interlocked per level, reported every 32 levels.
        int levelsDone = 0;
        int levelsTotal = levels.Count;
        var reportProgress = options.LevelProgress;
        void OnLevelDone()
        {
            if (reportProgress is null) return;
            int done = Interlocked.Increment(ref levelsDone);
            if ((done & 31) == 0 || done == levelsTotal)
                reportProgress(done, levelsTotal);
        }

        // Inner DFS parallelism only pays off when the level fan-out alone cannot
        // saturate the machine; otherwise nested parallel tasks just add overhead.
        bool innerParallel;

        if (options.EarlyStop)
        {
            int chunkSize = Math.Max(1, options.EarlyStopChunkSize);
            innerParallel = chunkSize < Environment.ProcessorCount;
            for (int start = 0; start < levels.Count; start += chunkSize)
            {
                long versionBefore = collector.Version;
                int end = Math.Min(start + chunkSize, levels.Count);
                Parallel.For(start, end, parallelOptions,
                    idx =>
                    {
                        EvaluateLevel(matrix, levels[idx], n, c, collector, budget, ct, maxIntersections, innerParallel);
                        OnLevelDone();
                    });

                if (budget.Exhausted) break;

                // Stop once a whole chunk brings no improvement — but only after
                // at least one solution exists (high-alpha levels are often barren).
                if (collector.Count > 0 && collector.Version == versionBefore)
                    break;
            }
        }
        else
        {
            innerParallel = levels.Count < Environment.ProcessorCount;
            Parallel.ForEach(levels, parallelOptions,
                level =>
                {
                    EvaluateLevel(matrix, level, n, c, collector, budget, ct, maxIntersections, innerParallel);
                    OnLevelDone();
                });
        }

        budgetExhausted = budget.Exhausted;
        options.Diagnostics?.Record(budget.Used, budgetExhausted, alphas.Length, levels.Count);
        return collector.Drain();
    }

    /// <summary>Run D-AFC for a single pre-selected alpha (used by prototype pipelines).</summary>
    public IReadOnlyList<ClusterResult> RunForAlpha(
        FuzzyToleranceMatrix matrix, double alpha, CancellationToken ct = default)
    {
        int n = matrix.Size;
        int c = options.ClusterCount;
        if (c < 2 || c > n) return [];

        var powers = new int[n];
        for (int i = 0; i < n; i++)
            powers[i] = CountAboveThreshold(matrix.GetRow(i), alpha);

        if (options.MaxIntersections >= 0)
            return RunLevel(matrix, alpha, powers, n, c, options.MaxIntersections, ct, out _);

        for (int w = 0; w <= n; w++)
        {
            var found = RunLevel(matrix, alpha, powers, n, c, w, ct, out bool budgetExhausted);
            if (found.Count > 0 || budgetExhausted) return found;
        }
        return [];
    }

    private IReadOnlyList<ClusterResult> RunLevel(
        FuzzyToleranceMatrix matrix, double alpha, int[] powers,
        int n, int c, int maxIntersections, CancellationToken ct, out bool budgetExhausted)
    {
        budgetExhausted = false;
        if (!TryCreateLevel(alpha, powers, n, c, maxIntersections, out var level))
        {
            options.Diagnostics?.Record(0, false, 1, 0);
            return [];
        }

        var collector = new TopNCollector(options.MaxResults);
        var budget = new SearchBudget(options.MaxCombinations);
        EvaluateLevel(matrix, level, n, c, collector, budget, ct,
            maxIntersections, parallelSearch: true); // single level — fan the DFS out across cores
        budgetExhausted = budget.Exhausted;
        options.Diagnostics?.Record(budget.Used, budgetExhausted, 1, 1);
        return collector.Drain();
    }

    /// <summary>
    /// Build an alpha level from precomputed object powers, applying the structural
    /// feasibility filters. Returns false when the level cannot possibly produce a
    /// valid allotment for the given intersection bound.
    /// </summary>
    private bool TryCreateLevel(double alpha, ReadOnlySpan<int> allPowers, int n, int c,
        int maxIntersections, out AlphaLevel level)
    {
        level = default;
        int upperBound = Math.Min(options.EffectiveMaxPower(n), n - c + 1);

        var candidates = new List<int>(n);
        var powers = new List<int>(n);
        int maxFound = 0;
        int minFound = int.MaxValue;

        for (int i = 0; i < n; i++)
        {
            int power = allPowers[i];
            if (power < options.MinClusterPower || power > upperBound) continue;
            candidates.Add(i);
            powers.Add(power);
            if (power > maxFound) maxFound = power;
            if (power < minFound) minFound = power;
        }

        if (candidates.Count < c) return false;

        // Undercoverage: even the c strongest candidates cannot cover all n objects.
        if ((long)maxFound * c < n) return false;

        // Overflow: every candidate cluster is so large that the partition must exceed
        // the intersection budget (sum of powers − n ≥ number of multi-covered objects).
        if (maxIntersections != int.MaxValue
            && (long)minFound * c - n > maxIntersections)
            return false;

        // Exact cover: the powers of some c candidates must add up to a value in
        // [n, n + (c−1)·w]; for fully separated clusters to exactly n.
        if (options.ExactCoverPruning && maxIntersections != int.MaxValue
            && !PowerSumFeasible(CollectionsMarshal.AsSpan(powers), n, c, maxIntersections))
            return false;

        level = new AlphaLevel(alpha, [.. candidates], [.. powers]);
        return true;
    }

    /// <summary>Largest admissible sum of the cluster powers: n + (c−1)·w, capped at c·n.</summary>
    private static int MaxPowerSum(int n, int c, int maxIntersections)
        => (int)Math.Min((long)n + (long)(c - 1) * maxIntersections, (long)c * n);

    /// <summary>
    /// True when some c of the given powers sum to a value in [n, n + (c−1)·w]: a subset-sum
    /// table over (number of clusters, sum) kept as bitsets, O(k·c·n/64) word operations.
    /// </summary>
    private static bool PowerSumFeasible(ReadOnlySpan<int> powers, int n, int c, int maxIntersections)
    {
        int maxSum = MaxPowerSum(n, c, maxIntersections);
        int sumWords = (maxSum >> 6) + 1;

        ref var scratch = ref _reachScratch;
        int needed = (c + 1) * sumWords;
        if (scratch is null || scratch.Length < needed)
            scratch = new ulong[Math.Max(needed, 64)];
        var reach = scratch.AsSpan(0, needed);
        reach.Clear();
        reach[0] = 1UL; // zero clusters reach the sum 0

        foreach (int power in powers)
        {
            for (int clusters = c; clusters >= 1; clusters--)
                ShiftOr(reach.Slice(clusters * sumWords, sumWords),
                        reach.Slice((clusters - 1) * sumWords, sumWords), power);
        }
        return AnyBitInRange(reach.Slice(c * sumWords, sumWords), n, maxSum);
    }

    /// <summary>destination |= source &lt;&lt; shift, over multi-word bitsets of equal length.</summary>
    private static void ShiftOr(Span<ulong> destination, ReadOnlySpan<ulong> source, int shift)
    {
        int wordShift = shift >> 6;
        int bitShift = shift & 63;
        for (int word = destination.Length - 1; word >= wordShift; word--)
        {
            ulong moved = source[word - wordShift] << bitShift;
            if (bitShift != 0 && word - wordShift - 1 >= 0)
                moved |= source[word - wordShift - 1] >> (64 - bitShift);
            destination[word] |= moved;
        }
    }

    /// <summary>True when the bitset has a set bit with index in [low, high].</summary>
    private static bool AnyBitInRange(ReadOnlySpan<ulong> bits, int low, int high)
    {
        if (low < 0) low = 0;
        int limit = (bits.Length << 6) - 1;
        if (high > limit) high = limit;
        if (low > high) return false;

        int firstWord = low >> 6, lastWord = high >> 6;
        ulong firstMask = ulong.MaxValue << (low & 63);
        ulong lastMask = ulong.MaxValue >> (63 - (high & 63));
        if (firstWord == lastWord) return (bits[firstWord] & firstMask & lastMask) != 0;
        if ((bits[firstWord] & firstMask) != 0) return true;
        for (int word = firstWord + 1; word < lastWord; word++)
            if (bits[word] != 0) return true;
        return (bits[lastWord] & lastMask) != 0;
    }

    /// <summary>
    /// Bitset combination engine for one alpha level: materialise candidate α-cuts as
    /// bitsets, then DFS with bit-test disjointness, OR-coverage and power-bound pruning.
    /// </summary>
    private void EvaluateLevel(
        FuzzyToleranceMatrix matrix, AlphaLevel level,
        int n, int c, TopNCollector collector, SearchBudget budget, CancellationToken ct,
        int maxIntersections, bool parallelSearch = false)
    {
        if (budget.Exhausted) return;

        int[] cands = level.Candidates;
        int[] powers = level.Powers;
        int k = cands.Length;
        int words = (n + 63) >> 6;

        // α-cut bitsets: one row of `words` ulongs per candidate.
        var cuts = new ulong[k * words];
        for (int i = 0; i < k; i++)
            BuildAlphaCutBitset(matrix.GetRow(cands[i]), level.Alpha, cuts.AsSpan(i * words, words));

        // suffixMaxPower[j] = max cluster power among candidates j..k−1 (branch & bound).
        var suffixMaxPower = new int[k + 1];
        for (int j = k - 1; j >= 0; j--)
            suffixMaxPower[j] = Math.Max(powers[j], suffixMaxPower[j + 1]);

        // Candidate-incompatibility bitsets: bit jj of row j is set when candidates
        // j and jj are mutually tolerant at this alpha (one lies in the α-cut of the
        // other) and therefore can never be centers together. The DFS keeps an
        // "available candidates" mask and clears whole incompatibility rows with one
        // AND per chosen center — no per-node pairwise re-checks.
        int kWords = (k + 63) >> 6;
        var incompatible = new ulong[k * kWords];
        bool fullySeparated = maxIntersections == 0 && options.ExactCoverPruning;
        for (int i = 0; i < k; i++)
        {
            var cutRow = cuts.AsSpan(i * words, words);
            for (int j = 0; j < k; j++)
            {
                if (j == i) continue;
                bool clash;
                if (fullySeparated)
                {
                    // w = 0: the two α-cuts must be disjoint — no common neighbour.
                    var otherRow = cuts.AsSpan(j * words, words);
                    clash = false;
                    for (int ww = 0; ww < words; ww++)
                        if ((cutRow[ww] & otherRow[ww]) != 0) { clash = true; break; }
                }
                else
                {
                    int obj = cands[j];
                    clash = (cutRow[obj >> 6] >> (obj & 63) & 1UL) != 0;
                }
                if (clash)
                    incompatible[i * kWords + (j >> 6)] |= 1UL << (j & 63);
            }
            incompatible[i * kWords + (i >> 6)] |= 1UL << (i & 63); // never re-pick self
        }

        // Suffix subset-sum table for the exact-cover bound: reach[(j, r)] = set of sums of
        // the powers of r candidates taken from j..k−1. Skipped for unbounded w and when the
        // table would be unreasonably large.
        ulong[]? reach = null;
        int sumWords = 0, maxSum = 0;
        if (options.ExactCoverPruning && maxIntersections != int.MaxValue)
        {
            maxSum = MaxPowerSum(n, c, maxIntersections);
            sumWords = (maxSum >> 6) + 1;
            long tableWords = (long)(k + 1) * c * sumWords;
            if (tableWords <= 8_000_000)
            {
                reach = new ulong[tableWords];
                reach[(long)k * c * sumWords] = 1UL; // (k, 0): the empty selection sums to 0
                for (int j = k - 1; j >= 0; j--)
                {
                    var current = reach.AsSpan(j * c * sumWords, c * sumWords);
                    var next = reach.AsSpan((j + 1) * c * sumWords, c * sumWords);
                    next.CopyTo(current);
                    for (int picks = c - 1; picks >= 1; picks--)
                        ShiftOr(current.Slice(picks * sumWords, sumWords),
                                next.Slice((picks - 1) * sumWords, sumWords), powers[j]);
                }
            }
        }

        // Per-level lazy caches: a candidate's member set (and, for decomposable
        // criteria, its per-cluster contribution) is fixed for the whole level —
        // computing it once instead of at every DFS leaf is the difference between
        // O(n) and O(1) per (leaf, cluster). One level is searched by one thread.
        var context = new LevelContext
        {
            Cuts           = cuts,
            SuffixMaxPower = suffixMaxPower,
            Incompatible   = incompatible,
            KWords         = kWords,
            MemberSets     = new int[k][],
            Decomposable   = criterion as IDecomposableCriterion,
            MaxIntersections = maxIntersections,
            Reach          = reach,
            SumWords       = sumWords,
            MaxSum         = maxSum,
        };
        if (context.Decomposable is not null)
        {
            context.Terms = new double[k];
            Array.Fill(context.Terms, double.NaN); // NaN = not computed yet
        }

        if (!parallelSearch)
        {
            var chosen = new int[c];
            var unionStack = new ulong[(c + 1) * words]; // prefix unions; depth d at [d·words, (d+1)·words)
            var availStack = new ulong[(c + 1) * kWords]; // available candidates per depth
            for (int j = 0; j < k; j++)
                availStack[j >> 6] |= 1UL << (j & 63);   // depth 0: everyone available

            var cursor = new BudgetCursor(budget);
            Search(matrix, level, context, chosen, unionStack, availStack,
                depth: 0, firstCandidate: 0, coveredCount: 0, powerSum: 0, n, c, words, collector,
                cursor, ct);
            cursor.Flush();
            return;
        }

        // Intra-level parallelism: fan out over the first-center choice. Used when the
        // caller has too few levels to keep all cores busy (e.g. RunForAlpha = 1 level).
        // The depth-0 coverage bound is monotone in j, so branch generation stops at
        // the first failure exactly like the sequential sweep.
        var allAvailable = new ulong[kWords];
        for (int j = 0; j < k; j++)
            allAvailable[j >> 6] |= 1UL << (j & 63);

        var firstBranches = new List<int>(k);
        for (int j = 0; j <= k - c; j++)
        {
            if ((long)c * suffixMaxPower[j] < n) break;
            if (!PowerSumStillFeasible(context, j, picksAfter: c - 1, powerSum: powers[j], n, c)) continue;
            firstBranches.Add(j);
        }
        if (!budget.TryCharge(firstBranches.Count)) return;

        Parallel.ForEach(firstBranches,
            new ParallelOptions { CancellationToken = ct },
            j =>
            {
                var chosen = new int[c];
                var unionStack = new ulong[(c + 1) * words];
                var availStack = new ulong[(c + 1) * kWords];

                chosen[0] = j;
                int covered = 0;
                for (int ww = 0; ww < words; ww++)
                {
                    ulong merged = cuts[j * words + ww];
                    unionStack[words + ww] = merged;
                    covered += BitOperations.PopCount(merged);
                }
                for (int ww = 0; ww < kWords; ww++)
                    availStack[kWords + ww] = allAvailable[ww] & ~incompatible[j * kWords + ww];

                var cursor = new BudgetCursor(budget);
                Search(matrix, level, context, chosen, unionStack, availStack,
                    depth: 1, firstCandidate: j + 1, covered, powerSum: powers[j], n, c, words, collector,
                    cursor, ct);
                cursor.Flush();
            });
    }

    /// <summary>
    /// Exact-cover bound: after choosing candidate <paramref name="candidate"/> (its power is
    /// already included in <paramref name="powerSum"/>), can <paramref name="picksAfter"/> later
    /// candidates bring the sum of powers into [n, n + (c−1)·w]?
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PowerSumStillFeasible(LevelContext context, int candidate, int picksAfter, int powerSum, int n, int c)
    {
        var reach = context.Reach;
        if (reach is null) return true;
        if (powerSum > context.MaxSum) return false;
        int sumWords = context.SumWords;
        var row = reach.AsSpan(((candidate + 1) * c + picksAfter) * sumWords, sumWords);
        return AnyBitInRange(row, n - powerSum, context.MaxSum - powerSum);
    }

    /// <summary>
    /// Per-alpha-level search state shared across the DFS. The lazy caches tolerate
    /// concurrent branches: a duplicated computation yields an identical value, and
    /// both the array-reference and the aligned-double writes are atomic, so the
    /// worst case is harmless redundant work — never a torn or wrong value.
    /// </summary>
    private sealed class LevelContext
    {
        public required ulong[] Cuts;
        public required int[] SuffixMaxPower;
        public required ulong[] Incompatible;          // k × KWords candidate-pair mask
        public required int KWords;
        public required int[][] MemberSets;            // lazy, per candidate
        public required IDecomposableCriterion? Decomposable;
        public required int MaxIntersections;          // w for this scan
        public required ulong[]? Reach;                // suffix subset-sum table, (k+1) × c × SumWords
        public required int SumWords;
        public required int MaxSum;                    // n + (c−1)·w
        public double[]? Terms;                        // lazy per-candidate contribution (NaN = unset)
    }

    private void Search(
        FuzzyToleranceMatrix matrix, AlphaLevel level, LevelContext context,
        int[] chosen, ulong[] unionStack, ulong[] availStack,
        int depth, int firstCandidate, int coveredCount, int powerSum,
        int n, int c, int words, TopNCollector collector, BudgetCursor budget, CancellationToken ct)
    {
        if (depth == c)
        {
            if (coveredCount == n)
                ScoreSolution(matrix, level, context, chosen, n, c, words, collector);
            return;
        }

        ct.ThrowIfCancellationRequested();

        int k = level.Candidates.Length;
        int kWords = context.KWords;
        int remaining = c - depth;
        int lastStart = k - remaining; // candidates beyond this can't seed `remaining` picks
        if (lastStart < firstCandidate) return;

        // One node = one budget unit; the cursor batches the shared-counter sync.
        // When the budget runs out the search stops gracefully — already-collected
        // results are still returned.
        if (!budget.Spend()) return;

        var avail = availStack.AsSpan(depth * kWords, kWords);
        var cuts = context.Cuts;
        var incompatible = context.Incompatible;

        for (int w = firstCandidate >> 6; w < kWords; w++)
        {
            ulong word = avail[w];
            if (w == firstCandidate >> 6)
                word &= ulong.MaxValue << (firstCandidate & 63);

            while (word != 0)
            {
                int j = (w << 6) + BitOperations.TrailingZeroCount(word);
                word &= word - 1;
                if (j > lastStart) return;

                // Branch & bound: even the strongest remaining candidates cannot complete
                // coverage. suffixMaxPower is non-increasing, so all later j fail too.
                if (coveredCount + (long)remaining * context.SuffixMaxPower[j] < n) return;

                // Exact-cover bound on the sum of the cluster powers.
                int newPowerSum = powerSum + level.Powers[j];
                if (!PowerSumStillFeasible(context, j, remaining - 1, newPowerSum, n, c)) continue;

                // Coverage union with incremental popcount.
                var prefix = unionStack.AsSpan(depth * words, words);
                var union  = unionStack.AsSpan((depth + 1) * words, words);
                var cut    = cuts.AsSpan(j * words, words);
                int newCovered = 0;
                for (int ww = 0; ww < words; ww++)
                {
                    ulong merged = prefix[ww] | cut[ww];
                    union[ww] = merged;
                    newCovered += BitOperations.PopCount(merged);
                }

                // Next depth sees only candidates compatible with j (and everyone before).
                var childAvail = availStack.AsSpan((depth + 1) * kWords, kWords);
                for (int ww = 0; ww < kWords; ww++)
                    childAvail[ww] = avail[ww] & ~incompatible[j * kWords + ww];

                chosen[depth] = j;
                Search(matrix, level, context, chosen, unionStack, availStack,
                    depth + 1, j + 1, newCovered, newPowerSum, n, c, words, collector, budget, ct);
            }
        }
    }

    /// <summary>Assemble member sets from the per-level caches, score and collect.</summary>
    private void ScoreSolution(
        FuzzyToleranceMatrix matrix, AlphaLevel level, LevelContext context,
        int[] chosen,
        int n, int c, int words, TopNCollector collector)
    {
        var centers = new int[c];
        var memberSets = new int[c][];
        for (int l = 0; l < c; l++)
        {
            int j = chosen[l];
            centers[l] = level.Candidates[j];
            memberSets[l] = context.MemberSets[j]
                ??= ExtractSetBits(context.Cuts.AsSpan(j * words, words), level.Powers[j]);
        }

        if (context.MaxIntersections != int.MaxValue
            && CountIntersections(memberSets, n) > context.MaxIntersections)
            return;

        if (options.AllotmentFilter is { } admissible && !admissible(centers, memberSets))
            return;

        double score;
        if (context.Decomposable is { } decomposable)
        {
            // Per-candidate contributions are fixed within the level — cache them.
            var terms = context.Terms!;
            double sum = 0.0;
            for (int l = 0; l < c; l++)
            {
                int j = chosen[l];
                double term = terms[j];
                if (double.IsNaN(term))
                    terms[j] = term = decomposable.EvaluateCluster(matrix, centers[l], memberSets[l], level.Alpha);
                sum += term;
            }
            score = decomposable.Aggregate(sum, c, level.Alpha);
        }
        else
        {
            score = criterion.Evaluate(matrix, centers, memberSets, level.Alpha);
        }

        // Cheap pre-check: skip the O(n·c) membership materialisation for the vast
        // majority of valid solutions that cannot enter the top-N heap anyway.
        // TryAdd re-checks under the lock, so a stale read here is harmless.
        if (!collector.WouldAccept(score)) return;

        var membership = MembershipMatrix.Build(matrix, centers, memberSets, n);
        collector.TryAdd(new ClusterResult(c, level.Alpha, centers, memberSets, membership, score, criterion.Name));
    }

    /// <summary>
    /// Set dest bit j when row[j] ≥ alpha. AVX path compares 4 doubles per op and packs
    /// the 4-bit movemask straight into the bitset (j is a multiple of 4, so a group
    /// never straddles a 64-bit word boundary).
    /// </summary>
    private static void BuildAlphaCutBitset(ReadOnlySpan<double> row, double alpha, Span<ulong> dest)
    {
        dest.Clear();
        int n = row.Length;
        int j = 0;

        if (Avx.IsSupported && n >= 4)
        {
            var vAlpha = Vector256.Create(alpha);
            ref double start = ref MemoryMarshal.GetReference(row);
            for (; j <= n - 4; j += 4)
            {
                var cmp = Avx.Compare(
                    Vector256.LoadUnsafe(ref start, (nuint)j), vAlpha,
                    FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
                ulong bits = (uint)Avx.MoveMask(cmp); // 4 bits, one per lane
                dest[j >> 6] |= bits << (j & 63);
            }
        }

        for (; j < n; j++)
            if (row[j] >= alpha)
                dest[j >> 6] |= 1UL << (j & 63);
    }

    /// <summary>Indices of set bits in ascending order (required by MembershipMatrix.Build).</summary>
    private static int[] ExtractSetBits(ReadOnlySpan<ulong> bits, int count)
    {
        var setIndices = new int[count];
        int written = 0;
        for (int wordIndex = 0; wordIndex < bits.Length; wordIndex++)
        {
            ulong remainingBits = bits[wordIndex];
            int firstIndexInWord = wordIndex << 6;
            while (remainingBits != 0)
            {
                setIndices[written++] = firstIndexInWord + BitOperations.TrailingZeroCount(remainingBits);
                remainingBits &= remainingBits - 1;   // clear the lowest set bit
            }
        }
        return setIndices;
    }

    /// <summary>Number of elements ≥ alpha in a DESCENDING-sorted array (binary search).</summary>
    private static int CountAtLeastDescending(double[] sortedDescending, double alpha)
    {
        int lo = 0, hi = sortedDescending.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (sortedDescending[mid] >= alpha) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Count elements ≥ alpha in an unsorted row.
    /// Uses AVX2 (4 doubles/Vector256) when available; falls back to Vector&lt;double&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountAboveThreshold(ReadOnlySpan<double> row, double alpha)
    {
        int count = 0;
        int j = 0;

        if (Avx.IsSupported && row.Length >= 4)
        {
            var vAlpha = Vector256.Create(alpha);
            ref double start = ref MemoryMarshal.GetReference(row);
            for (; j <= row.Length - 4; j += 4)
            {
                var cmp = Avx.Compare(
                    Vector256.LoadUnsafe(ref start, (nuint)j), vAlpha,
                    FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
                count += BitOperations.PopCount((uint)Avx.MoveMask(cmp));
            }
        }
        else
        {
            int vectorWidth = Vector<double>.Count;
            var vectorAlpha = new Vector<double>(alpha);
            for (; j <= row.Length - vectorWidth; j += vectorWidth)
            {
                var mask = Vector.AsVectorUInt64(
                    Vector.GreaterThanOrEqual(new Vector<double>(row.Slice(j, vectorWidth)), vectorAlpha));
                for (int lane = 0; lane < vectorWidth; lane++)
                    count += (int)(mask[lane] >> 63);
            }
        }

        for (; j < row.Length; j++)
            if (row[j] >= alpha) count++;

        return count;
    }

    /// <summary>Number of objects belonging to more than one cluster.</summary>
    private static int CountIntersections(int[][] memberSets, int objectCount)
    {
        ref var scratch = ref _intersectionScratch;
        if (scratch is null || scratch.Length < objectCount)
            scratch = new int[Math.Max(objectCount, 256)];

        var coverage = scratch.AsSpan(0, objectCount);
        coverage.Clear();

        foreach (var set in memberSets)
            foreach (var idx in set)
                coverage[idx]++;

        int intersections = 0;
        for (int i = 0; i < objectCount; i++)
            if (coverage[i] > 1) intersections++;
        return intersections;
    }

    /// <summary>
    /// Runaway guard: counts combination-search nodes actually visited across all
    /// levels of one Run. Unlike an upfront C(n,c) estimate, this respects the
    /// branch &amp; bound pruning — when the budget is spent the search stops
    /// gracefully and already-collected results are returned.
    /// </summary>
    private sealed class SearchBudget(long limit)
    {
        private long _used;

        public bool Exhausted => Volatile.Read(ref _used) >= limit;

        /// <summary>Search nodes charged so far.</summary>
        public long Used => Volatile.Read(ref _used);

        public void Add(int nodes) => Interlocked.Add(ref _used, nodes);

        /// <summary>Returns false when the budget is spent (charge still recorded).</summary>
        public bool TryCharge(int nodes) => Interlocked.Add(ref _used, nodes) <= limit;
    }

    /// <summary>
    /// Per-search-thread view of the shared budget: counts nodes locally and syncs
    /// to the shared counter once per batch, so the DFS hot path performs no
    /// interlocked operation per node (worst-case overshoot is one batch per thread).
    /// </summary>
    private sealed class BudgetCursor(SearchBudget shared)
    {
        private const int BatchSize = 4096;
        private int _local;
        private bool _exhausted;

        public bool Spend()
        {
            if (_exhausted) return false;
            if (++_local < BatchSize) return true;
            _local = 0;
            if (shared.TryCharge(BatchSize)) return true;
            _exhausted = true;
            return false;
        }

        /// <summary>Adds the nodes counted since the last batch to the shared counter.</summary>
        public void Flush()
        {
            if (_local > 0) shared.Add(_local);
            _local = 0;
        }
    }

    /// <summary>
    /// Thread-safe bounded top-N collector backed by a min-heap ordered by
    /// <see cref="ClusterResult.Order"/>. Keeps the N best results under that total
    /// order, so the retained set — and hence the final ranking — does not depend on
    /// the order in which parallel workers submit equal-score solutions.
    /// Version increments on every accepted result (used by the early-stop heuristic).
    /// </summary>
    private sealed class TopNCollector(int capacity)
    {
        // Priority = the result itself; the comparer is inverted so that the root of the
        // min-heap is the WORST retained result.
        private readonly PriorityQueue<ClusterResult, ClusterResult> _heap =
            new(Comparer<ClusterResult>.Create((a, b) => ClusterResult.Order.Compare(b, a)));
        private readonly object _lock = new();
        private long _version;
        private double _admissionThreshold = double.NegativeInfinity; // worst kept score once full

        public long Version { get { lock (_lock) return _version; } }

        public int Count { get { lock (_lock) return _heap.Count; } }

        /// <summary>Lock-free pre-check; may be stale — TryAdd is the authority.</summary>
        public bool WouldAccept(double score) => score >= Volatile.Read(ref _admissionThreshold);

        public void TryAdd(ClusterResult result)
        {
            lock (_lock)
            {
                if (_heap.Count < capacity)
                {
                    _heap.Enqueue(result, result);
                    _version++;
                    if (_heap.Count == capacity && _heap.TryPeek(out var filledWorst, out _))
                        Volatile.Write(ref _admissionThreshold, filledWorst.Score);
                }
                else if (_heap.TryPeek(out var worst, out _) && ClusterResult.Order.Compare(result, worst) < 0)
                {
                    _heap.Dequeue();
                    _heap.Enqueue(result, result);
                    _version++;
                    if (_heap.TryPeek(out var newWorst, out _))
                        Volatile.Write(ref _admissionThreshold, newWorst.Score);
                }
            }
        }

        /// <summary>Drains the heap and returns results in <see cref="ClusterResult.Order"/>.</summary>
        public IReadOnlyList<ClusterResult> Drain()
        {
            var results = new List<ClusterResult>(_heap.Count);
            while (_heap.TryDequeue(out var item, out _))
                results.Add(item);
            results.Sort(ClusterResult.Order);
            return results;
        }
    }
}
