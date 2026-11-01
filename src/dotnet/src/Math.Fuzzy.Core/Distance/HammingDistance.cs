namespace MathFuzzy.Core.Distance;

/// <summary>
/// Normalised Hamming distance between fuzzy sets (Viattchenin 2013, eq. (2.13)):
///   d_H(a, b) = (1/m) · Σ_t |a_t − b_t|.
/// For data normalised to [0,1] the result lies in [0,1], so T = 1 − d_H is a
/// fuzzy tolerance. (This is the L1 mean, not the binary Hamming count.)
/// </summary>
public sealed class HammingDistance : IDistance
{
    public static readonly HammingDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length == 0) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < a.Length; i++)
            sum += Math.Abs(a[i] - b[i]);
        return sum / a.Length;
    }
}
