using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TO and D-AFC-TO(α) — the direct prototype-based algorithm with a transitive opening in
/// place of the transitive closure (Viattchenin and Tati 2013, sec. 2.4).
///
/// Pipeline:
///   1. Build the fuzzy tolerance matrix T (or take a given T)
///   2. Compute a transitive opening T̆ ⊆ T (<see cref="TransitiveOpening"/>; by default the
///      complete-linkage construction, which reproduces the published example)
///   3. Select the threshold α* with the leap heuristic on the levels of T̆, by default in the
///      ratio form stated in the source (the smallest α_{ℓ−1}/α_ℓ); with a least threshold
///      given, as in D-AFC-TC(α)
///   4. Construct and rank the allotments of that level exactly as D-AFC-TC does.
///
/// T̆ is a fuzzy equivalence, so the allotments of a level are the choices of one typical point
/// per equivalence class (<see cref="ClosureAllotment"/>), and the memberships are entries of T̆.
/// </summary>
public sealed class DAfcToAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TransitiveOpening      opening,
    ICriterion             criterion,
    int                    minClusterPower = 1,
    int                    maxClusterPower = 0,
    int                    maxResults      = 200,
    double?                leastAlpha      = null,
    LeapForm               leapForm        = LeapForm.Ratio) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>Runs the algorithm on a given tolerance matrix (see <see cref="DAfcTcAlgorithm.Run(FuzzyToleranceMatrix, RawDataMatrix?, CancellationToken)"/>).</summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
    {
        var openingMatrix = opening.Compute(tolerance);
        var levels = openingMatrix.GetDistinctValues();
        double? threshold = leastAlpha is double least
            ? DAfcTcAlphaAlgorithm.SelectThreshold(levels, least, leapForm)
            : LeapHeuristicAlphaSelector.SelectFrom(levels, leapForm);
        if (threshold is not double alpha) return [];

        return ClosureAllotment.Solve(openingMatrix, alpha, criterion, minClusterPower, maxClusterPower,
                                      normalizedData, maxResults, ct);
    }

    /// <summary>Exposes the transitive opening of the data for inspection (e.g. UI).</summary>
    public FuzzyToleranceMatrix BuildOpeningMatrix(RawDataMatrix data)
        => opening.Compute(toleranceBuilder.Build(data));
}
