namespace MathFuzzy.Core.Distance;

public interface IDistance
{
    /// <summary>Computes distance between two vectors. Result in [0,1].</summary>
    double Compute(ReadOnlySpan<double> a, ReadOnlySpan<double> b);
}
