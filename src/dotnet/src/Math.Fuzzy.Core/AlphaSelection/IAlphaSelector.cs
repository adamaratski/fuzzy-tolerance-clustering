using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.AlphaSelection;

public interface IAlphaSelector
{
    /// <summary>Returns candidate alpha threshold values from the matrix.</summary>
    IReadOnlyList<double> Select(FuzzyToleranceMatrix matrix);
}
