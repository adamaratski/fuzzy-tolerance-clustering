using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.DimensionReduction;

/// <summary>Reduces dimensionality of raw data using attribute-space clustering.</summary>
public interface IDimensionReducer
{
    RawDataMatrix Reduce(RawDataMatrix data, CancellationToken ct = default);

    /// <summary>Indices of selected attributes in the original data.</summary>
    IReadOnlyList<int> SelectedAttributeIndices { get; }
}
