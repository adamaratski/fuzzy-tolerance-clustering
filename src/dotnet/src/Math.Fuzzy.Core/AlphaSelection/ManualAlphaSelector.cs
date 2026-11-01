using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.AlphaSelection;

/// <summary>Returns a user-specified list of alpha values.</summary>
public sealed class ManualAlphaSelector(IReadOnlyList<double> alphas) : IAlphaSelector
{
    public IReadOnlyList<double> Select(FuzzyToleranceMatrix matrix) => alphas;
}
