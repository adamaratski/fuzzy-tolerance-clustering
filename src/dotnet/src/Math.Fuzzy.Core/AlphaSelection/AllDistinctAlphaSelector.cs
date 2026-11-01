using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.AlphaSelection;

/// <summary>Returns all distinct matrix values in descending order.</summary>
public sealed class AllDistinctAlphaSelector : IAlphaSelector
{
    public static readonly AllDistinctAlphaSelector Instance = new();

    public IReadOnlyList<double> Select(FuzzyToleranceMatrix matrix)
        => matrix.GetDistinctValues();
}
