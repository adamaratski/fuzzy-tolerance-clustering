namespace MathFuzzy.Core.Distance;

/// <summary>Chebyshev (L∞): max|a_i - b_i|. Result in [0,1] after MinMax normalization.</summary>
public sealed class ChebyshevDistance : IDistance
{
    public static readonly ChebyshevDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double max = 0.0;
        for (int i = 0; i < a.Length; i++)
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        return max;
    }
}
