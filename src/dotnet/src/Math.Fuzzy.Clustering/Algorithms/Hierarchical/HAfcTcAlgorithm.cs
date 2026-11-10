using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Hierarchical;

/// <summary>
/// H-AFC-TC — the hierarchical algorithm with the transitive closure
/// (Viattchenin 2013, sec. 2.3.2).
///
/// Produces a full dendrogram by finding the best allotment at ALL levels of the transitive
/// closure T̃ (ascending order: coarse → fine). At each level the flat clustering is one
/// layer of the hierarchy: few large clusters at a low α, many small ones at a high α.
///
/// Per level the best allotment is the best typical point of each α-class
/// (<see cref="ClosureAllotment"/>), with the same tie-breaking as D-AFC-TC, so the layer of
/// the hierarchy at a level coincides with what D-AFC-TC returns for that level.
/// </summary>
public sealed class HAfcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower = 1) : IHierarchicalClusterer
{
    public HierarchicalResult Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>
    /// Runs the algorithm on a given tolerance matrix; <paramref name="normalizedData"/> enables
    /// the geometric tie-breaking (null for a relation given directly).
    /// </summary>
    public HierarchicalResult Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
        => Build(closure.Compute(tolerance), criterion, minClusterPower, normalizedData, ct);

    /// <summary>The hierarchy of a fuzzy equivalence (a transitive closure or approximation).</summary>
    internal static HierarchicalResult Build(
        FuzzyToleranceMatrix equivalence, ICriterion criterion, int minClusterPower,
        RawDataMatrix? normalizedData, CancellationToken ct)
    {
        int objectCount = equivalence.Size;

        var levels = new List<ClusterResult>();
        foreach (double alpha in equivalence.GetDistinctValues().Order()) // coarse → fine
        {
            ct.ThrowIfCancellationRequested();
            var best = ClosureAllotment.Solve(equivalence, alpha, criterion, minClusterPower,
                                              maxClusterPower: 0, normalizedData, maxResults: 1, ct);
            if (best.Count > 0)
                levels.Add(best[0]);
        }

        return new HierarchicalResult(BuildDendrogram(levels, objectCount), levels);
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
