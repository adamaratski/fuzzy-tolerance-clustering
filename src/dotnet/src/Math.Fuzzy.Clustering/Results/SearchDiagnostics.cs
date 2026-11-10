namespace MathFuzzy.Clustering.Results;

/// <summary>
/// A pair of thresholds (α, β) of an intuitionistic fuzzy tolerance at which an allotment among
/// fully separated clusters exists, with the number of clusters of these allotments.
/// </summary>
/// <param name="Alpha">Threshold on the membership degrees.</param>
/// <param name="Beta">Threshold on the non-membership degrees.</param>
/// <param name="ClusterCount">Number of clusters of every fully separated allotment at the pair.</param>
/// <param name="OnFrontier">True when no other admissible pair is at least as loose in both thresholds.</param>
public sealed record ThresholdPair(double Alpha, double Beta, int ClusterCount, bool OnFrontier);

/// <summary>
/// Statistics of a combination search, filled by the algorithm that received the object.
/// The most important one is <see cref="BudgetExhausted"/>: when it is true the search was
/// cut short by its node budget and the returned allotments are the best found so far,
/// not necessarily the best that exist.
/// </summary>
public sealed class SearchDiagnostics
{
    private long _nodesVisited;
    private int _budgetExhausted;
    private int _levelsTotal;
    private int _levelsSearched;

    /// <summary>Search-tree nodes visited over all levels and all passes of the run.</summary>
    public long NodesVisited => Interlocked.Read(ref _nodesVisited);

    /// <summary>True when some pass stopped because the node budget was spent.</summary>
    public bool BudgetExhausted => Volatile.Read(ref _budgetExhausted) != 0;

    /// <summary>α-levels offered to the search (summed over passes).</summary>
    public int LevelsTotal => Volatile.Read(ref _levelsTotal);

    /// <summary>α-levels that passed the structural tests and were searched (summed over passes).</summary>
    public int LevelsSearched => Volatile.Read(ref _levelsSearched);

    /// <summary>
    /// Order-free intuitionistic search only: all pairs of thresholds at which a fully separated
    /// allotment exists; those marked <see cref="ThresholdPair.OnFrontier"/> are the loosest ones.
    /// </summary>
    public IReadOnlyList<ThresholdPair> AdmissiblePairs { get; internal set; } = [];

    internal void Record(long nodesVisited, bool budgetExhausted, int levelsTotal, int levelsSearched)
    {
        Interlocked.Add(ref _nodesVisited, nodesVisited);
        if (budgetExhausted) Volatile.Write(ref _budgetExhausted, 1);
        Interlocked.Add(ref _levelsTotal, levelsTotal);
        Interlocked.Add(ref _levelsSearched, levelsSearched);
    }
}
