using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TAGA and D-AFC-TAGA(α) — the direct prototype-based algorithm with a transitive
/// approximation in place of the transitive closure (Viattchenin and Damaratski 2013).
///
/// Pipeline:
///   1. Build the fuzzy tolerance matrix T (or take a given T)
///   2. Compute the transitive approximation T^φ with the aggregation operator φ
///   3. Select the threshold α* with the leap heuristic on the levels of T^φ, in the ratio
///      form of the paper (the smallest α_{ℓ−1}/α_ℓ); with a least threshold given, as in
///      D-AFC-TC(α)
///   4. Construct and rank the allotments of that level exactly as D-AFC-TC does.
///
/// T^φ is a fuzzy equivalence for every operator, so the allotments of a level are the
/// choices of one typical point per equivalence class (<see cref="ClosureAllotment"/>).
/// </summary>
public sealed class DAfcTagaAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TAGAProcessor          taga,
    ICriterion             criterion,
    int                    minClusterPower = 1,
    int                    maxClusterPower = 0,
    int                    maxResults      = 200,
    double?                leastAlpha      = null) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>Runs the algorithm on a given tolerance matrix (see <see cref="DAfcTcAlgorithm.Run(FuzzyToleranceMatrix, RawDataMatrix?, CancellationToken)"/>).</summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
    {
        var approximation = taga.Apply(tolerance);
        var levels = approximation.GetDistinctValues();
        double? threshold = leastAlpha is double least
            ? DAfcTcAlphaAlgorithm.SelectThreshold(levels, least, LeapForm.Ratio)
            : LeapHeuristicAlphaSelector.SelectFrom(levels, LeapForm.Ratio);
        if (threshold is not double alpha) return [];

        return ClosureAllotment.Solve(approximation, alpha, criterion, minClusterPower, maxClusterPower,
                                      normalizedData, maxResults, ct);
    }
}
