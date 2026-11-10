namespace MathFuzzy.Clustering.Results;

/// <summary>Result of a flat clustering run for a specific (c, alpha) pair.</summary>
public sealed record ClusterResult(
    int ClusterCount,
    double Alpha,
    int[] Centers,
    int[][] MemberSets,
    double[,] Membership,
    double Score,
    string CriterionName,
    /// <summary>
    /// Geometric tiebreaker: sum of Euclidean distances from each typical point to its cluster
    /// centroid, computed in the normalised feature space. Lower = more compact solution.
    /// Populated by D-AFC-TC; null for all other algorithms.
    /// </summary>
    double? TiebreakerScore = null,
    /// <summary>
    /// Intuitionistic algorithms only: the threshold β on the non-membership degrees
    /// (<see cref="Alpha"/> is then the threshold on the membership degrees). Null otherwise.
    /// </summary>
    double? Beta = null,
    /// <summary>
    /// Intuitionistic algorithms only: non-membership degrees, [object, cluster], zero outside
    /// the carrier of the cluster. Null otherwise.
    /// </summary>
    double[,]? NonMembership = null,
    /// <summary>
    /// Intuitionistic algorithms only: the bound w on the number of objects shared by two
    /// clusters under which the allotment was constructed. Null otherwise.
    /// </summary>
    int? IntersectionBound = null
)
{
    public static ClusterResult Empty { get; } =
        new(0, 0, [], [], new double[0, 0], double.NegativeInfinity, "");

    /// <summary>
    /// True when the other result consists of the same clusters as sets of objects; the typical
    /// points, and hence the membership degrees, may differ.
    /// </summary>
    public bool HasSameClusters(ClusterResult other)
    {
        if (other.MemberSets.Length != MemberSets.Length) return false;
        static string[] Keys(int[][] sets) =>
            [.. sets.Select(set => string.Join(',', set.Order())).Order(StringComparer.Ordinal)];
        return Keys(MemberSets).SequenceEqual(Keys(other.MemberSets));
    }

    /// <summary>
    /// Describes a tie at the top of a ranked list: how many results share the best value of the
    /// criterion (and of the tie-breaker), and whether they differ in the clusters or only in the
    /// typical points. Null when the best result is unique.
    /// </summary>
    public static string? DescribeTie(IReadOnlyList<ClusterResult> ranked)
    {
        if (ranked.Count < 2) return null;
        var best = ranked[0];
        var tied = ranked.Where(result => result.ClusterCount == best.ClusterCount && result.Score == best.Score
                                          && result.TiebreakerScore == best.TiebreakerScore).ToList();
        if (tied.Count < 2) return null;
        return tied.All(best.HasSameClusters)
            ? $"{tied.Count} choices of typical points share the largest value of the criterion; the clusters are the same."
            : $"{tied.Count} allotments share the largest value of the criterion; the result is not unique.";
    }

    /// <summary>
    /// Deterministic total order of results: higher score first, then (when the
    /// geometric tiebreaker is present) smaller tiebreaker, then higher alpha, then
    /// fewer clusters, then lexicographically smaller center indices. Two runs on
    /// the same input therefore always rank identical solutions identically,
    /// regardless of the order in which parallel workers produced them.
    /// </summary>
    public static readonly IComparer<ClusterResult> Order = new ResultOrder();

    private sealed class ResultOrder : IComparer<ClusterResult>
    {
        public int Compare(ClusterResult? x, ClusterResult? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;
            if (y is null) return -1;

            int cmp = y.Score.CompareTo(x.Score);                 // score desc
            if (cmp != 0) return cmp;

            if (x.TiebreakerScore.HasValue && y.TiebreakerScore.HasValue)
            {
                cmp = x.TiebreakerScore.Value.CompareTo(y.TiebreakerScore.Value); // asc
                if (cmp != 0) return cmp;
            }

            cmp = y.Alpha.CompareTo(x.Alpha);                     // alpha desc
            if (cmp != 0) return cmp;

            cmp = x.ClusterCount.CompareTo(y.ClusterCount);       // fewer clusters first
            if (cmp != 0) return cmp;

            int len = Math.Min(x.Centers.Length, y.Centers.Length);
            for (int i = 0; i < len; i++)
            {
                cmp = x.Centers[i].CompareTo(y.Centers[i]);       // centers lexicographic
                if (cmp != 0) return cmp;
            }
            return x.Centers.Length.CompareTo(y.Centers.Length);
        }
    }

    /// <summary>Max cluster size across all clusters.</summary>
    public int MaxClusterSize => MemberSets.Length == 0 ? 0 : MemberSets.Max(s => s.Length);

    /// <summary>Number of objects belonging to more than one cluster.</summary>
    public int IntersectionCount
    {
        get
        {
            if (MemberSets.Length == 0) return 0;
            var count = new Dictionary<int, int>();
            foreach (var set in MemberSets)
            foreach (var idx in set)
                count[idx] = count.GetValueOrDefault(idx) + 1;
            return count.Values.Count(c => c > 1);
        }
    }
}
