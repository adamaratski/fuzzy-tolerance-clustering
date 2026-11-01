namespace MathFuzzy.Clustering.Algorithms.Relational;

/// <param name="ClusterCount">Number of clusters c.</param>
/// <param name="MinClusterPower">Minimum allowed cluster size p_min (≥ 1).</param>
/// <param name="MaxClusterPower">Maximum allowed cluster size p_max. 0 = unlimited (n).</param>
/// <param name="MaxIntersections">
/// Maximum number of objects that may belong to more than one cluster (w).
/// <see cref="AutoIntersections"/> (the default) follows the original D-AFC(c):
/// the search starts with fully separated clusters (w = 0) and increases w by one
/// only while no allotment among exactly c clusters exists. 0 = strict partition,
/// <see cref="int.MaxValue"/> = unlimited.
/// </param>
/// <param name="MaxCombinations">
/// Runaway guard: maximum number of combination-search nodes actually visited
/// across all alpha levels of one run (branch &amp; bound pruning counts only what
/// is really explored, so this is far less than C(n,c)). When the budget is
/// spent the search stops gracefully and the best results found so far are
/// returned — no exception is thrown.
/// </param>
/// <param name="MaxResults">Keep only the top-N results by score to bound memory usage.</param>
/// <param name="EarlyStop">
/// When true, alpha levels are scanned descending in chunks of
/// <see cref="EarlyStopChunkSize"/>; the scan stops at the first chunk that yields
/// no score improvement (once at least one solution exists).
/// This is a HEURISTIC speed-up: the criterion is not monotone in alpha, so the
/// global optimum may lie at a lower alpha than where the scan stops.
/// Default false — exhaustive scan over all feasible levels (exact result).
/// </param>
/// <param name="EarlyStopChunkSize">
/// Number of valid alpha levels processed in parallel per chunk when
/// <see cref="EarlyStop"/> is true. Default 8.
/// </param>
/// <param name="LevelProgress">
/// Optional scan-progress callback: (levels done, levels total). Invoked roughly
/// every 32 completed levels from worker threads — keep the handler cheap and thread-safe.
/// </param>
public sealed record DAfcOptions(
    int  ClusterCount,
    int  MinClusterPower      = 1,
    int  MaxClusterPower      = 0,
    int  MaxIntersections     = DAfcOptions.AutoIntersections,
    long MaxCombinations      = 500_000_000L,
    int  MaxResults           = 200,
    bool EarlyStop            = false,
    int  EarlyStopChunkSize   = 8,
    Action<int, int>? LevelProgress = null
)
{
    /// <summary>Sentinel for <see cref="MaxIntersections"/>: w := 0 with automatic increment.</summary>
    public const int AutoIntersections = -1;

    public int EffectiveMaxPower(int n) => MaxClusterPower > 0 ? MaxClusterPower : n;
}
