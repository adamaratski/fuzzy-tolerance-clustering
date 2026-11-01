using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Hierarchical;

/// <summary>
/// H-AFC-TC — Hierarchical (divisive) AFC with Transitive Closure
/// (Viattchenin 2013, sec. 2.3.2).
///
/// Produces a full dendrogram by finding the best allotment at ALL alpha levels
/// of the transitive closure T̃ (ascending order: coarse → fine).
/// At each alpha level, the flat clustering represents one layer of the hierarchy.
///
/// Dendrogram structure: at low alpha → few large clusters;
/// at high alpha → many small clusters (divisive decomposition).
///
/// Per level, decomposable criteria (F₁, F₂) use <see cref="ClosureAllotment"/>:
/// the best typical point of each α-class, which is the arg max of a D-AFC search
/// without enumerating the ∏|class| allotments (10⁹–10¹⁰ per level on Iris).
/// Other criteria fall back to the D-AFC combination search.
/// </summary>
public sealed class HAfcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower = 1) : IHierarchicalClusterer
{
    public HierarchicalResult Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var closureMatrix = PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);
        int objectCount = closureMatrix.Size;

        // All alpha levels ascending (coarse → fine grained)
        var alphaLevels = closureMatrix.GetDistinctValues()
                                       .OrderBy(level => level)
                                       .ToList();

        var levels = new List<ClusterResult>();
        foreach (var alpha in alphaLevels)
        {
            ct.ThrowIfCancellationRequested();

            // On T̃ the α-cuts partition X into equivalence classes; only
            // c = class count yields valid allotments at this level.
            var classes = PrototypePipeline.AlphaClasses(closureMatrix, alpha);
            if (classes.Count < 2 || classes.Count > objectCount) continue;

            var best = BestAllotment(closureMatrix, alpha, classes, ct);
            if (best is not null)
                levels.Add(best);
        }

        var root = BuildDendrogram(levels, objectCount);
        return new HierarchicalResult(root, levels);
    }

    /// <summary>The criterion-maximising allotment of one α-level, or null if none exists.</summary>
    private ClusterResult? BestAllotment(
        FuzzyToleranceMatrix closureMatrix, double alpha, List<int[]> classes, CancellationToken ct)
    {
        if (criterion is IDecomposableCriterion decomposable)
            return ClosureAllotment.FindBest(closureMatrix, alpha, classes, decomposable, minClusterPower, ct);

        var dafc = new DAfcAlgorithm(
            new DAfcOptions(classes.Count, minClusterPower),
            new ManualAlphaSelector([alpha]),
            criterion);

        var results = dafc.RunForAlpha(closureMatrix, alpha, ct);
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Builds a dendrogram tree from flat clusterings at each alpha level.
    /// Root = lowest alpha (all objects in few clusters).
    /// Leaves = highest alpha (finest partition).
    /// </summary>
    private static DendrogramNode BuildDendrogram(List<ClusterResult> levels, int objectCount)
    {
        if (levels.Count == 0)
        {
            // Degenerate: single cluster with all objects
            return new DendrogramNode(0.0, Enumerable.Range(0, objectCount).ToArray(), []);
        }

        // Build bottom-up: each level's clusters become children of the parent cluster
        // that contains most of their members at the next coarser level.
        DendrogramNode? previous = null;
        foreach (var level in levels.AsEnumerable().Reverse())
        {
            var nodes = level.MemberSets
                .Select(members => new DendrogramNode(
                    level.Alpha,
                    members,
                    previous is not null ? FindChildNodes(members, previous) : []))
                .ToList();

            // Wrap this level into a single root node representing the alpha cut
            previous = new DendrogramNode(level.Alpha,
                level.MemberSets.SelectMany(members => members).Distinct().OrderBy(objectIndex => objectIndex).ToArray(),
                nodes);
        }

        return previous!;
    }

    private static IReadOnlyList<DendrogramNode> FindChildNodes(int[] parentMembers, DendrogramNode finerLevel)
    {
        var parentSet = new HashSet<int>(parentMembers);
        return finerLevel.Children
            .Where(child => child.ObjectIndices.Any(objectIndex => parentSet.Contains(objectIndex)))
            .ToList();
    }
}
