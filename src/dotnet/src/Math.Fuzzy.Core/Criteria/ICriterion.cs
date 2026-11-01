using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Criteria;

public interface ICriterion
{
    string Name { get; }

    /// <summary>Evaluates quality of a cluster assignment. Higher = better.</summary>
    double Evaluate(FuzzyToleranceMatrix matrix, int[] centers, int[][] memberSets, double alpha);
}
