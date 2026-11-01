using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Benchmarks;

/// <summary>Best allotment found by a search: threshold, criterion value and typical points.</summary>
public sealed record SearchOutcome(double Alpha, double Score, int[] Centres, long CombinationsChecked, int LevelsSearched);

/// <summary>
/// Straightforward D-AFC(c) level search (fully separated clusters, w = 0, criterion F₁),
/// written as Step 4 of the algorithm reads: every c-combination of candidate typical
/// points is formed and checked by counting how often each object is covered.
/// The bit-set engine of the library visits a subset of these combinations and must
/// return the same best allotment.
/// </summary>
public static class SearchVariants
{
    /// <summary>
    /// Searches the given levels. Without the pre-filter every object is a candidate on
    /// every level; with it, levels that fail the structural tests are skipped and only
    /// objects with an admissible cluster power are candidates.
    /// </summary>
    public static SearchOutcome Enumerate(
        FuzzyToleranceMatrix matrix, IReadOnlyList<double> levels, int clusterCount, bool preFilter)
    {
        int objectCount = matrix.Size;
        var best = new SearchOutcome(double.NaN, double.NegativeInfinity, [], 0, 0);
        long combinationsChecked = 0;
        int levelsSearched = 0;
        var coverage = new int[objectCount];
        var combination = new int[clusterCount];

        foreach (double alpha in levels)
        {
            int[] candidates = preFilter
                ? AdmissibleCandidates(matrix, alpha, clusterCount)
                : Enumerable.Range(0, objectCount).ToArray();
            if (candidates.Length < clusterCount) continue;
            levelsSearched++;

            // combination = indices into candidates, in lexicographic order
            for (int slot = 0; slot < clusterCount; slot++) combination[slot] = slot;
            while (true)
            {
                combinationsChecked++;
                double score = ScoreIfFullySeparated(matrix, alpha, candidates, combination, coverage);
                if (score > best.Score)
                    best = new SearchOutcome(alpha, score, combination.Select(slot => candidates[slot]).ToArray(), 0, 0);

                int position = clusterCount - 1;
                while (position >= 0 && combination[position] == candidates.Length - clusterCount + position) position--;
                if (position < 0) break;
                combination[position]++;
                for (int later = position + 1; later < clusterCount; later++)
                    combination[later] = combination[later - 1] + 1;
            }
        }
        return best with { CombinationsChecked = combinationsChecked, LevelsSearched = levelsSearched };
    }

    /// <summary>
    /// The structural level tests of the library (D-AFC pre-filter): candidates are the
    /// objects whose α-cut size lies in [1, n − c + 1]; the level is dropped when fewer than c
    /// candidates exist, when c of the largest cuts cannot cover X, or when c of the smallest
    /// cuts already overlap.
    /// </summary>
    public static int[] AdmissibleCandidates(FuzzyToleranceMatrix matrix, double alpha, int clusterCount)
    {
        int objectCount = matrix.Size;
        var candidates = new List<int>();
        int largestPower = 0, smallestPower = int.MaxValue;
        for (int candidate = 0; candidate < objectCount; candidate++)
        {
            int power = 0;
            foreach (double relation in matrix.GetRow(candidate))
                if (relation >= alpha) power++;
            if (power > objectCount - clusterCount + 1) continue;
            candidates.Add(candidate);
            largestPower = Math.Max(largestPower, power);
            smallestPower = Math.Min(smallestPower, power);
        }
        if (candidates.Count < clusterCount) return [];
        if ((long)largestPower * clusterCount < objectCount) return [];
        if ((long)smallestPower * clusterCount - objectCount > 0) return [];
        return [.. candidates];
    }

    /// <summary>
    /// F₁ of the allotment whose typical points are the chosen candidates, or −∞ when the
    /// α-cuts do not partition X (an object uncovered or covered twice).
    /// </summary>
    private static double ScoreIfFullySeparated(
        FuzzyToleranceMatrix matrix, double alpha, int[] candidates, int[] combination, int[] coverage)
    {
        Array.Clear(coverage);
        foreach (int slot in combination)
        {
            var row = matrix.GetRow(candidates[slot]);
            for (int member = 0; member < row.Length; member++)
                if (row[member] >= alpha) coverage[member]++;
        }
        foreach (int count in coverage)
            if (count != 1) return double.NegativeInfinity;

        double score = -alpha * combination.Length;
        foreach (int slot in combination)
        {
            var row = matrix.GetRow(candidates[slot]);
            double membershipSum = 0.0;
            int clusterSize = 0;
            foreach (double relation in row)
            {
                if (relation < alpha) continue;
                membershipSum += relation;
                clusterSize++;
            }
            score += membershipSum / clusterSize;
        }
        return score;
    }
}
