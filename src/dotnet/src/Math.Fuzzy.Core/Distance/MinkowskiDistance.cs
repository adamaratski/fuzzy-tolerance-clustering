namespace MathFuzzy.Core.Distance;

/// <summary>
/// Minkowski (Lp): (mean(|a_i - b_i|^p))^(1/p). Result in [0,1] after MinMax normalization.
/// Special cases: p=1 → Manhattan, p=2 → Euclidean, p→∞ → Chebyshev.
/// </summary>
public sealed class MinkowskiDistance(double p = 3.0) : IDistance
{
    public double P { get; } = p > 0 ? p
        : throw new ArgumentOutOfRangeException(nameof(p), "Minkowski p must be > 0.");

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double sum = 0.0;
        for (int i = 0; i < a.Length; i++)
            sum += Math.Pow(Math.Abs(a[i] - b[i]), P);
        return Math.Clamp(Math.Pow(sum / a.Length, 1.0 / P), 0.0, 1.0);
    }
}
