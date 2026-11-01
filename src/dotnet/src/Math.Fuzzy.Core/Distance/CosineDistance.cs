using System.Numerics.Tensors;

namespace MathFuzzy.Core.Distance;

/// <summary>
/// Cosine distance: 1 - cos(a, b). Result in [0,1] after MinMax normalization
/// (all components non-negative → cosine similarity ∈ [0,1]).
/// Zero vectors (all-minimum after normalization) are treated as identical: distance = 0.
/// </summary>
public sealed class CosineDistance : IDistance
{
    public static readonly CosineDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double normA = TensorPrimitives.Norm(a);
        double normB = TensorPrimitives.Norm(b);

        if (normA < 1e-12 || normB < 1e-12)
            return 0.0;

        double similarity = TensorPrimitives.Dot(a, b) / (normA * normB);
        return Math.Clamp(1.0 - similarity, 0.0, 1.0);
    }
}
