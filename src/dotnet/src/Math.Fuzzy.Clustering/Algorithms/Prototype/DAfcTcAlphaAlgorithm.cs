using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TC(α) — D-AFC-TC with a user-specified least tolerance threshold
/// (Viattchenin 2013, sec. 2.3.1).
///
/// The threshold α* comes from the leap heuristic; when it falls below the given value, the
/// heuristic is applied again to the levels {α_ℓ : α_ℓ ≥ userAlpha} only.
/// </summary>
public sealed class DAfcTcAlphaAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    double                  userAlpha,
    int                     minClusterPower = 1,
    int                     maxClusterPower = 0,
    int                     maxResults      = 200) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>Runs the algorithm on a given tolerance matrix (see <see cref="DAfcTcAlgorithm.Run(FuzzyToleranceMatrix, RawDataMatrix?, CancellationToken)"/>).</summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
    {
        var closureMatrix = closure.Compute(tolerance);
        if (SelectThreshold(closureMatrix.GetDistinctValues(), userAlpha) is not double alpha)
            return [];

        return ClosureAllotment.Solve(closureMatrix, alpha, criterion, minClusterPower, maxClusterPower,
                                      normalizedData, maxResults, ct);
    }

    /// <summary>
    /// The leap-heuristic threshold not below <paramref name="leastAlpha"/>, from a descending
    /// list of levels; null when every level lies below the given value.
    /// </summary>
    internal static double? SelectThreshold(
        IReadOnlyList<double> descendingLevels, double leastAlpha, LeapForm form = LeapForm.Difference)
    {
        if (LeapHeuristicAlphaSelector.SelectFrom(descendingLevels, form) is not double alpha) return null;
        if (alpha >= leastAlpha) return alpha;

        var admissible = descendingLevels.Where(level => level >= leastAlpha).ToArray(); // still descending
        return LeapHeuristicAlphaSelector.SelectFrom(admissible, form);
    }
}
