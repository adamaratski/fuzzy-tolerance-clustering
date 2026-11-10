using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// The allotments of one α-level of a fuzzy equivalence (a transitive closure or a transitive
/// approximation), ranked by the criterion (Viattchenin 2013, sec. 2.3.1–2.3.2).
///
/// On a fuzzy equivalence the α-cut of every object is its equivalence class, so every
/// allotment in B(α) has the same supports — the classes — and differs only in the typical
/// point chosen inside each class. For a decomposable criterion (F₁, F₂) the value is a
/// monotone function of the sum of per-class contributions, therefore
///   • the best allotment takes the best typical point of each class independently, and
///   • the k best allotments are the k best elements of a product of sorted lists,
/// which costs O(Σ|class|²) plus O(k·c·log(k·c)) instead of the ∏|class| allotments a
/// combination search enumerates.
///
/// Ties are the rule rather than the exception: the relation is ultrametric, so several
/// members of a class have the same contribution in exact arithmetic while the computed
/// values differ in the last bits. Contributions within <see cref="TieTolerance"/> of each
/// other are therefore one value (the largest of the group is reported), and tied typical
/// points are ordered by their distance to the centroid of the class in the normalised
/// attribute space — the geometric rule of the prototype-based algorithms — and then by
/// object index. Without attribute data (a relation given directly) only the index is used.
/// </summary>
internal static class ClosureAllotment
{
    /// <summary>
    /// Contributions are means of memberships in [0, 1] and distances are of order 1; rounding
    /// error stays far below this bound, genuinely different values differ far above it.
    /// </summary>
    internal const double TieTolerance = 1e-12;

    /// <summary>A typical-point candidate of one class with its canonical (tie-merged) values.</summary>
    private readonly record struct Candidate(int Center, double Contribution, double Distance);

    /// <summary>
    /// The allotments of the level α of a fuzzy equivalence, best first, at most
    /// <paramref name="maxResults"/> of them; empty when the level has fewer than two classes
    /// or a class violates the power bounds.
    /// </summary>
    internal static IReadOnlyList<ClusterResult> Solve(
        FuzzyToleranceMatrix equivalence,
        double               alpha,
        ICriterion           criterion,
        int                  minClusterPower,
        int                  maxClusterPower,
        RawDataMatrix?       normalizedData,
        int                  maxResults,
        CancellationToken    ct)
    {
        var classes = PrototypePipeline.AlphaClasses(equivalence, alpha);
        if (classes.Count < 2) return [];

        if (criterion is IDecomposableCriterion decomposable)
            return Rank(equivalence, alpha, classes, decomposable, minClusterPower, maxClusterPower,
                        normalizedData, maxResults, ct);

        // A criterion without per-cluster contributions: search the level as D-AFC does.
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(classes.Count, minClusterPower, maxClusterPower, MaxIntersections: 0, MaxResults: maxResults),
            new ManualAlphaSelector([alpha]),
            criterion);
        var found = dafc.RunForAlpha(equivalence, alpha, ct);
        if (normalizedData is null) return found;
        return [.. found
            .Select(result => result with { TiebreakerScore = CentroidDistance(result, normalizedData) })
            .Order(ClusterResult.Order)];
    }

    /// <summary>The k best allotments among the given α-classes (see the class remarks).</summary>
    internal static IReadOnlyList<ClusterResult> Rank(
        FuzzyToleranceMatrix   equivalence,
        double                 alpha,
        IReadOnlyList<int[]>   classes,
        IDecomposableCriterion criterion,
        int                    minClusterPower,
        int                    maxClusterPower,
        RawDataMatrix?         normalizedData,
        int                    maxResults,
        CancellationToken      ct)
    {
        int largestAllowed = maxClusterPower > 0 ? maxClusterPower : int.MaxValue;
        if (classes.Any(members => members.Length < minClusterPower || members.Length > largestAllowed)) return [];
        if (maxResults < 1) return [];

        int classCount = classes.Count;
        var ranked = new Candidate[classCount][];
        for (int classIndex = 0; classIndex < classCount; classIndex++)
        {
            ct.ThrowIfCancellationRequested();
            ranked[classIndex] = RankTypicalPoints(equivalence, alpha, classes[classIndex], criterion, normalizedData);
        }

        // Best-first enumeration of the product of the ranked lists. A state is a tuple of
        // positions; its successors advance one position at or after the last advanced one,
        // which generates every tuple exactly once, and the key never improves along a step.
        var queue = new PriorityQueue<(int[] Positions, int LastAdvanced), SelectionKey>(SelectionKey.BestFirst);
        var start = new int[classCount];
        queue.Enqueue((start, 0), KeyOf(ranked, start));

        var results = new List<ClusterResult>(Math.Min(maxResults, 256));
        while (results.Count < maxResults && queue.TryDequeue(out var state, out var key))
        {
            ct.ThrowIfCancellationRequested();
            results.Add(Assemble(equivalence, alpha, classes, ranked, state.Positions, key, criterion,
                                 withDistances: normalizedData is not null));

            for (int classIndex = state.LastAdvanced; classIndex < classCount; classIndex++)
            {
                if (state.Positions[classIndex] + 1 >= ranked[classIndex].Length) continue;
                var next = (int[])state.Positions.Clone();
                next[classIndex]++;
                queue.Enqueue((next, classIndex), KeyOf(ranked, next));
            }
        }
        return results;
    }

    /// <summary>
    /// The members of a class as typical-point candidates, best first: larger contribution,
    /// then smaller distance to the class centroid, then smaller object index; values that tie
    /// within <see cref="TieTolerance"/> are replaced by one canonical value.
    /// </summary>
    private static Candidate[] RankTypicalPoints(
        FuzzyToleranceMatrix equivalence, double alpha, int[] members,
        IDecomposableCriterion criterion, RawDataMatrix? normalizedData)
    {
        var contributions = new double[members.Length];
        for (int position = 0; position < members.Length; position++)
            contributions[position] = criterion.EvaluateCluster(equivalence, members[position], members, alpha);
        MergeTies(contributions, largestWins: true);

        var distances = new double[members.Length];
        if (normalizedData is not null)
        {
            var centroid = Centroid(normalizedData, members);
            for (int position = 0; position < members.Length; position++)
                distances[position] = Distance(normalizedData.GetRow(members[position]), centroid);
            MergeTies(distances, largestWins: false);
        }

        var candidates = new Candidate[members.Length];
        for (int position = 0; position < members.Length; position++)
            candidates[position] = new Candidate(members[position], contributions[position], distances[position]);

        Array.Sort(candidates, (first, second) =>
        {
            int order = second.Contribution.CompareTo(first.Contribution);
            if (order != 0) return order;
            order = first.Distance.CompareTo(second.Distance);
            return order != 0 ? order : first.Center.CompareTo(second.Center);
        });
        return candidates;
    }

    /// <summary>
    /// Replaces values that tie by one canonical value, in place: the sorted values are swept,
    /// a gap larger than <see cref="TieTolerance"/> starts a new group, and every value of a
    /// group becomes the group's largest (or smallest) value.
    /// </summary>
    private static void MergeTies(double[] values, bool largestWins)
    {
        var order = Enumerable.Range(0, values.Length).ToArray();
        Array.Sort(order, (first, second) => values[first].CompareTo(values[second]));

        int groupStart = 0;
        for (int position = 1; position <= order.Length; position++)
        {
            bool groupEnds = position == order.Length
                             || values[order[position]] - values[order[position - 1]] > TieTolerance;
            if (!groupEnds) continue;
            double canonical = largestWins ? values[order[position - 1]] : values[order[groupStart]];
            for (int member = groupStart; member < position; member++)
                values[order[member]] = canonical;
            groupStart = position;
        }
    }

    /// <summary>Ordering key of a selection: sums of the canonical values and the sorted typical points.</summary>
    private readonly record struct SelectionKey(double ContributionSum, double DistanceSum, int[] SortedCenters)
    {
        public static readonly IComparer<SelectionKey> BestFirst = Comparer<SelectionKey>.Create((first, second) =>
        {
            int order = second.ContributionSum.CompareTo(first.ContributionSum);
            if (order != 0) return order;
            order = first.DistanceSum.CompareTo(second.DistanceSum);
            if (order != 0) return order;
            for (int index = 0; index < first.SortedCenters.Length; index++)
            {
                order = first.SortedCenters[index].CompareTo(second.SortedCenters[index]);
                if (order != 0) return order;
            }
            return 0;
        });
    }

    private static SelectionKey KeyOf(Candidate[][] ranked, int[] positions)
    {
        double contributionSum = 0.0, distanceSum = 0.0;
        var centers = new int[positions.Length];
        for (int classIndex = 0; classIndex < positions.Length; classIndex++)
        {
            var candidate = ranked[classIndex][positions[classIndex]];
            contributionSum += candidate.Contribution;
            distanceSum += candidate.Distance;
            centers[classIndex] = candidate.Center;
        }
        Array.Sort(centers);
        return new SelectionKey(contributionSum, distanceSum, centers);
    }

    private static ClusterResult Assemble(
        FuzzyToleranceMatrix equivalence, double alpha, IReadOnlyList<int[]> classes,
        Candidate[][] ranked, int[] positions, SelectionKey key,
        IDecomposableCriterion criterion, bool withDistances)
    {
        // Clusters are listed by ascending typical point, as the relational algorithms list them.
        var clusters = new (int Center, int[] Members)[positions.Length];
        for (int classIndex = 0; classIndex < positions.Length; classIndex++)
            clusters[classIndex] = (ranked[classIndex][positions[classIndex]].Center, classes[classIndex]);
        Array.Sort(clusters, (first, second) => first.Center.CompareTo(second.Center));

        var centers    = clusters.Select(cluster => cluster.Center).ToArray();
        var memberSets = clusters.Select(cluster => cluster.Members).ToArray();
        var membership = MembershipMatrix.Build(equivalence, centers, memberSets, equivalence.Size);
        double score   = criterion.Aggregate(key.ContributionSum, centers.Length, alpha);

        return new ClusterResult(centers.Length, alpha, centers, memberSets, membership, score, criterion.Name,
                                 withDistances ? key.DistanceSum : null);
    }

    /// <summary>
    /// Σ over clusters of the Euclidean distance from the typical point to the centroid of the
    /// cluster in the normalised attribute space; smaller means more central typical points.
    /// </summary>
    internal static double CentroidDistance(ClusterResult result, RawDataMatrix normalizedData)
    {
        double total = 0.0;
        for (int cluster = 0; cluster < result.ClusterCount; cluster++)
        {
            var members = result.MemberSets[cluster];
            if (members.Length == 0) continue;
            total += Distance(normalizedData.GetRow(result.Centers[cluster]), Centroid(normalizedData, members));
        }
        return total;
    }

    private static double[] Centroid(RawDataMatrix normalizedData, int[] members)
    {
        var centroid = new double[normalizedData.Cols];
        foreach (int member in members)
        {
            var row = normalizedData.GetRow(member);
            for (int feature = 0; feature < centroid.Length; feature++)
                centroid[feature] += row[feature];
        }
        for (int feature = 0; feature < centroid.Length; feature++)
            centroid[feature] /= members.Length;
        return centroid;
    }

    private static double Distance(ReadOnlySpan<double> point, double[] centroid)
    {
        double sumOfSquares = 0.0;
        for (int feature = 0; feature < centroid.Length; feature++)
        {
            double difference = point[feature] - centroid[feature];
            sumOfSquares += difference * difference;
        }
        return Math.Sqrt(sumOfSquares);
    }
}
