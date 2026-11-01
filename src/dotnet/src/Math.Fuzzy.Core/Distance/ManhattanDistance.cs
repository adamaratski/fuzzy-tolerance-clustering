namespace MathFuzzy.Core.Distance;

/// <summary>Normalized Manhattan (L1): mean of |a_i - b_i|. Result in [0,1] after MinMax normalization.</summary>
public sealed class ManhattanDistance : IDistance
{
    public static readonly ManhattanDistance Instance = new();

    public double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double sum = 0.0;
        for (int i = 0; i < a.Length; i++)
            sum += Math.Abs(a[i] - b[i]);
        return sum / a.Length;
    }
}
