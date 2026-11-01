using System.Numerics.Tensors;

namespace MathFuzzy.Core.Aggregation.Taga;

public sealed class MaximumTAGA : ITAGAOperator
{
    public static readonly MaximumTAGA Instance = new();
    public string Name => "Maximum";
    public double Aggregate(IReadOnlyList<double> sorted) => sorted[^1];
}

public sealed class MinimumTAGA : ITAGAOperator
{
    public static readonly MinimumTAGA Instance = new();
    public string Name => "Minimum";
    public double Aggregate(IReadOnlyList<double> sorted) => sorted[0];
}

public sealed class MeanTAGA : ITAGAOperator
{
    public static readonly MeanTAGA Instance = new();
    public string Name => "Mean";
    public double Aggregate(IReadOnlyList<double> sorted)
    {
        double sum = 0.0;
        foreach (var v in sorted) sum += v;
        return sum / sorted.Count;
    }
}

public sealed class MedianTAGA : ITAGAOperator
{
    public static readonly MedianTAGA Instance = new();
    public string Name => "Median";
    public double Aggregate(IReadOnlyList<double> sorted)
    {
        int n = sorted.Count;
        return n % 2 == 1
            ? sorted[n / 2]
            : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }
}

public sealed class UpperMedianTAGA : ITAGAOperator
{
    public static readonly UpperMedianTAGA Instance = new();
    public string Name => "UpperMedian";
    public double Aggregate(IReadOnlyList<double> sorted) => sorted[(sorted.Count - 1 + 1) / 2];
}

public sealed class LowerMedianTAGA : ITAGAOperator
{
    public static readonly LowerMedianTAGA Instance = new();
    public string Name => "LowerMedian";
    public double Aggregate(IReadOnlyList<double> sorted) => sorted[(sorted.Count - 1) / 2];
}
