using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.Interval;

namespace MathFuzzy.Clustering.Algorithms.Interval;

/// <summary>Rule that selects the clusters whose typical points are kept.</summary>
public enum PathSelectionRule
{
    /// <summary>The clusters of maximal density D.</summary>
    MaximalDensity,

    /// <summary>The clusters of minimal linear index of fuzziness I_L.</summary>
    MinimalLinearFuzziness,

    /// <summary>The clusters of minimal quadratic index of fuzziness I_Q.</summary>
    MinimalQuadraticFuzziness
}

/// <summary>Result of the selection of paths.</summary>
/// <param name="Structure">The stable clustering structure of the paths (step 1).</param>
/// <param name="Indices">I_L, I_Q and D of every cluster of the selected allotment (step 2).</param>
/// <param name="SelectedClusters">Indices of the selected clusters in the allotment, ascending.</param>
/// <param name="SelectedPaths">Typical points of the selected clusters, ascending object indices (step 3).</param>
public sealed record ProjectPathSelectionResult(
    StableStructureResult      Structure,
    IReadOnlyList<ClusterIndices> Indices,
    IReadOnlyList<int>         SelectedClusters,
    IReadOnlyList<int>         SelectedPaths)
{
    public ClusterResult Allotment => Structure.Selected!;
}

/// <summary>
/// Selection of a minimal subset of paths of a project network with interval-valued activity
/// durations (Viattchenin 2015, sec. 2.4). Each path of the minimised network is an
/// interval-valued object whose attributes are the vertices other than the start and end
/// vertices: the duration interval of an activity on the path, [0, 0] for an activity not on it.
///   1. the stable clustering structure of the paths is constructed (<see cref="StableStructureTechnique"/>);
///   2. the density D, or the linear or quadratic index of fuzziness, of every cluster is computed,
///      and the clusters of maximal density (or minimal index) are selected;
///   3. the typical points of the selected clusters form the selected subset of paths.
/// </summary>
public static class ProjectPathSelection
{
    /// <summary>Values that differ by no more than this are equal when the extreme clusters are chosen.</summary>
    public const double Tolerance = 1e-12;

    public static ProjectPathSelectionResult Select(
        IntervalDataMatrix paths, StableStructureOptions? options = null,
        PathSelectionRule rule = PathSelectionRule.MaximalDensity, CancellationToken ct = default)
    {
        var structure = StableStructureTechnique.Run(paths, options, ct);
        if (structure.Selected is null)
            throw new InvalidOperationException("No admissible allotment of the paths was found.");
        return SelectFrom(structure, rule);
    }

    /// <summary>Steps 2 and 3 for a constructed structure.</summary>
    public static ProjectPathSelectionResult SelectFrom(StableStructureResult structure, PathSelectionRule rule)
    {
        var allotment = structure.Selected ?? throw new ArgumentException("The structure has no selected allotment.", nameof(structure));
        var indices = AllotmentMeasures.Compute(allotment).Clusters;
        var keys = indices.Select(cluster => rule switch
        {
            PathSelectionRule.MaximalDensity => -cluster.Density,
            PathSelectionRule.MinimalLinearFuzziness => cluster.LinearFuzziness,
            _ => cluster.QuadraticFuzziness
        }).ToArray();
        double bestKey = keys.Min();
        var selectedClusters = Enumerable.Range(0, keys.Length).Where(cluster => keys[cluster] <= bestKey + Tolerance).ToArray();
        var selectedPaths = selectedClusters.Select(cluster => allotment.Centers[cluster]).Order().ToArray();
        return new ProjectPathSelectionResult(structure, indices, selectedClusters, selectedPaths);
    }

    /// <summary>Parses a rule name: density, lmf (linear index), qmf (quadratic index).</summary>
    public static PathSelectionRule ParseRule(string name) => name.Trim().ToLowerInvariant() switch
    {
        "density" => PathSelectionRule.MaximalDensity,
        "lmf" or "linear" => PathSelectionRule.MinimalLinearFuzziness,
        "qmf" or "quadratic" => PathSelectionRule.MinimalQuadraticFuzziness,
        _ => throw new ArgumentException($"Unknown selection rule: '{name}'. Valid names: density, lmf, qmf")
    };
}
