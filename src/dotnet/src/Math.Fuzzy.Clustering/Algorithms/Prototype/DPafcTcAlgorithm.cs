using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-PAFC-TC — the principal allotment with the transitive closure (Viattchenin 2013, sec. 2.3.2).
///
/// The leap heuristic is not used: for the ascending sequence α₀ &lt; α₁ &lt; … of levels of T̃
/// the principal allotment — the allotment among the least number c* ≥ 2 of fully separated
/// clusters — is constructed at the first level at which the closure splits into several
/// equivalence classes that satisfy the power bounds. Its allotments are then ranked as in
/// D-AFC-TC (criterion, then the geometric tie-breaking).
/// </summary>
public sealed class DPafcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower = 1,
    int                     maxClusterPower = 0,
    int                     maxResults      = 200) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>Runs the algorithm on a given tolerance matrix (see <see cref="DAfcTcAlgorithm.Run(FuzzyToleranceMatrix, RawDataMatrix?, CancellationToken)"/>).</summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
        => FirstAdmissibleLevel(closure.Compute(tolerance), criterion, minClusterPower, maxClusterPower,
                                normalizedData, maxResults, ct);

    /// <summary>
    /// The allotments of the lowest level of a fuzzy equivalence that has at least two classes
    /// and satisfies the power bounds; empty when no level does.
    /// </summary>
    internal static IReadOnlyList<ClusterResult> FirstAdmissibleLevel(
        FuzzyToleranceMatrix equivalence, ICriterion criterion, int minClusterPower, int maxClusterPower,
        RawDataMatrix? normalizedData, int maxResults, CancellationToken ct)
    {
        foreach (double alpha in equivalence.GetDistinctValues().Order())
        {
            ct.ThrowIfCancellationRequested();
            var found = ClosureAllotment.Solve(equivalence, alpha, criterion, minClusterPower, maxClusterPower,
                                               normalizedData, maxResults, ct);
            if (found.Count > 0) return found;
        }
        return [];
    }

    public FuzzyToleranceMatrix BuildClosureMatrix(RawDataMatrix data)
        => closure.Compute(toleranceBuilder.Build(data));
}
