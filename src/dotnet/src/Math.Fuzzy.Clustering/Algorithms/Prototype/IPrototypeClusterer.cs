using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>Clustering algorithm that works on raw data, building the tolerance matrix internally.</summary>
public interface IPrototypeClusterer
{
    IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default);
}
