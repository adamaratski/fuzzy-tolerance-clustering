using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Relational;

/// <summary>
/// D-AFC(u) — the direct algorithm for a given maximal number u of objects in a fuzzy cluster
/// (Viattchenin, Yaroma and Damaratski 2014). Neither the number of clusters nor the threshold
/// is given: the analyst only bounds the size of a cluster from above, 1 ≤ n_l ≤ u &lt; n.
///
/// The statement implemented here is the one that reproduces the results published for the
/// algorithm (the Tamura portraits, the Sneath–Sokal data, Anderson's Iris data):
///   for w = 0, 1, 2, …                (objects allowed in intersection areas)
///     scan the levels of T upwards, skipping those at which some α-neighbourhood is X;
///     at the first level that admits an allotment among clusters of at most u objects with
///     at most w objects in intersection areas, take the least number of clusters c and
///     return the class of these allotments ordered by the criterion.
/// For a fixed w this is D-PAFC with the upper power bound u, so the algorithm is a D-PAFC that
/// relaxes the separation only when the size bound leaves no fully separated allotment.
/// With u at least the largest cluster of the principal allotment the result is the principal
/// allotment itself.
/// </summary>
public sealed class DAfcUAlgorithm(
    int                maxClusterPower,
    ICriterion         criterion,
    int                minClusterPower = 1,
    int                maxIntersections = DAfcOptions.AutoIntersections,
    long               maxSearchNodes   = 500_000_000L,
    SearchDiagnostics? diagnostics      = null,
    int                maxResults       = 200,
    Action<int, int>?  levelProgress    = null) : IRelationalClusterer
{
    public IReadOnlyList<ClusterResult> Run(FuzzyToleranceMatrix matrix, CancellationToken ct = default)
    {
        int objectCount = matrix.Size;
        if (maxClusterPower < 1 || maxClusterPower >= objectCount)
            throw new ArgumentException(
                $"The maximal number of objects in a cluster must satisfy 1 ≤ u < n = {objectCount}; got {maxClusterPower}.");

        int firstBound = maxIntersections >= 0 ? maxIntersections : 0;
        int lastBound  = maxIntersections >= 0 ? maxIntersections : objectCount;

        for (int intersections = firstBound; intersections <= lastBound; intersections++)
        {
            ct.ThrowIfCancellationRequested();
            var found = new DPafcAlgorithm(
                    AllDistinctAlphaSelector.Instance, criterion, minClusterPower, maxClusterPower,
                    intersections, levelProgress, maxSearchNodes, diagnostics, maxResults)
                .Run(matrix, ct);

            // An exhausted node budget proves nothing about this w: do not relax further.
            if (found.Count > 0 || diagnostics?.BudgetExhausted == true) return found;
        }
        return [];
    }
}
