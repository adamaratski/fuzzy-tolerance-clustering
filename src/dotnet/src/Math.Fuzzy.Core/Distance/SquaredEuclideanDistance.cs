using System.Numerics.Tensors;

namespace MathFuzzy.Core.Distance;

/// <summary>
/// Squared normalised Euclidean distance between fuzzy sets (Viattchenin 2013, eq. (2.15)):
///   d_E²(a, b) = (1/m) · Σ_t (a_t − b_t)².
/// For data normalised to [0,1] the result lies in [0,1]. It is not a metric (the
/// triangle inequality fails) but compresses tolerance values towards 1, which is the
/// setting used for the reference experiments of the monograph.
/// </summary>
public sealed class SquaredEuclideanDistance : IDistance
{
    public static readonly SquaredEuclideanDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length == 0) return 0.0;
        double raw = TensorPrimitives.Distance(a, b); // sqrt(Σ (a−b)²), SIMD accelerated
        return Math.Clamp(raw * raw / a.Length, 0.0, 1.0);
    }
}
