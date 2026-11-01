using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Hierarchical;

/// <summary>Hierarchical clustering algorithm producing a dendrogram.</summary>
public interface IHierarchicalClusterer
{
    HierarchicalResult Run(RawDataMatrix data, CancellationToken ct = default);
}
