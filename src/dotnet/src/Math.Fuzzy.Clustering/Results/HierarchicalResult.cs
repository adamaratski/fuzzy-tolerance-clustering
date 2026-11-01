namespace MathFuzzy.Clustering.Results;

public sealed record DendrogramNode(
    double Alpha,
    int[] ObjectIndices,
    IReadOnlyList<DendrogramNode> Children
);

public sealed record HierarchicalResult(
    DendrogramNode Root,
    IReadOnlyList<ClusterResult> Levels
);
