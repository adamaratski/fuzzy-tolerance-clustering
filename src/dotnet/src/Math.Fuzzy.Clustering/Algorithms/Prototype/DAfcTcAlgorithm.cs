using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TC — the direct algorithm with the transitive closure (Viattchenin 2013, sec. 2.3.1).
///
/// Pipeline:
///   1. Build the fuzzy tolerance matrix T from the data (or take a given T)
///   2. Compute the max-min transitive closure T̃
///   3. Select the threshold α* with the leap heuristic
///   4. Construct the allotments of the level α* and rank them by the criterion;
///      ties are resolved by the distance of the typical points to the cluster centroids.
///
/// The number of clusters is determined structurally: on a transitive closure the α-cut
/// relation is an equivalence, so the allotments of a level are exactly the choices of one
/// typical point per equivalence class (see <see cref="ClosureAllotment"/>).
/// </summary>
public sealed class DAfcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower = 1,
    int                     maxClusterPower = 0,
    int                     maxResults      = 200) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>
    /// Runs the algorithm on a given tolerance matrix. <paramref name="normalizedData"/> — the
    /// normalised attribute data the matrix was built from — enables the geometric tie-breaking;
    /// pass null for a relation given directly.
    /// </summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
    {
        var closureMatrix = closure.Compute(tolerance);
        if (LeapHeuristicAlphaSelector.SelectFrom(closureMatrix.GetDistinctValues()) is not double alpha)
            return [];

        return ClosureAllotment.Solve(closureMatrix, alpha, criterion, minClusterPower, maxClusterPower,
                                      normalizedData, maxResults, ct);
    }

    /// <summary>
    /// The variant with a given number of clusters: the allotments among exactly
    /// <paramref name="clusterCount"/> clusters over all levels of the closure, best first.
    /// The number of classes does not decrease with α, so such levels form one interval.
    /// </summary>
    public IReadOnlyList<ClusterResult> RunForClusterCount(
        FuzzyToleranceMatrix tolerance, int clusterCount, RawDataMatrix? normalizedData, CancellationToken ct = default)
    {
        var closureMatrix = closure.Compute(tolerance);
        var found = new List<ClusterResult>();
        foreach (double alpha in closureMatrix.GetDistinctValues().Order())
        {
            ct.ThrowIfCancellationRequested();
            int classCount = PrototypePipeline.CountAlphaClasses(closureMatrix, alpha);
            if (classCount < clusterCount) continue;
            if (classCount > clusterCount) break;
            found.AddRange(ClosureAllotment.Solve(closureMatrix, alpha, criterion, minClusterPower, maxClusterPower,
                                                  normalizedData, maxResults, ct));
        }
        found.Sort(ClusterResult.Order);
        return found.Count > maxResults ? found.GetRange(0, maxResults) : found;
    }

    /// <summary>Exposes the transitive closure matrix for inspection (e.g. UI).</summary>
    public FuzzyToleranceMatrix BuildClosureMatrix(RawDataMatrix data)
        => closure.Compute(toleranceBuilder.Build(data));
}
