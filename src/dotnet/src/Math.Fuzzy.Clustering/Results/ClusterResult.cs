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
    double? TiebreakerScore = null
)
{
    public static ClusterResult Empty { get; } =
        new(0, 0, [], [], new double[0, 0], double.NegativeInfinity, "");

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
