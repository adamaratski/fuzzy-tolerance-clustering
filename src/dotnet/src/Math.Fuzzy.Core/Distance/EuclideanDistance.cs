using System.Numerics.Tensors;

namespace MathFuzzy.Core.Distance;

/// <summary>Normalized Euclidean: sqrt(sum((a-b)^2) / dim). Result in [0, sqrt(dim)] normalized to [0,1].</summary>
public sealed class EuclideanDistance : IDistance
{
    public static readonly EuclideanDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        // TensorPrimitives.Distance = sqrt(sum((a-b)^2)) — SIMD accelerated
        double raw = TensorPrimitives.Distance(a, b);
        double normalized = raw / Math.Sqrt(a.Length);
        return Math.Clamp(normalized, 0.0, 1.0);
    }
}
