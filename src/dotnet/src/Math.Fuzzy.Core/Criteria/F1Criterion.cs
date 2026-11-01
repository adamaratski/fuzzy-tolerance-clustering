using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Criteria;

/// <summary>
/// F1 = Σᵢ [ Σⱼ∈Cᵢ μT(cᵢ,xⱼ) / |Cᵢ| ] - alpha * c
/// Maximize.
/// </summary>
public sealed class F1Criterion : IDecomposableCriterion
{
    public static readonly F1Criterion Instance = new();

    public string Name => "F1";

    public double Evaluate(FuzzyToleranceMatrix matrix, int[] centers, int[][] memberSets, double alpha)
    {
        double sum = 0.0;
        for (int l = 0; l < centers.Length; l++)
            sum += EvaluateCluster(matrix, centers[l], memberSets[l], alpha);
        return Aggregate(sum, centers.Length, alpha);
    }

    public double EvaluateCluster(FuzzyToleranceMatrix matrix, int center, int[] members, double alpha)
    {
        if (members.Length == 0) return 0.0;
        double clusterSum = 0.0;
        var row = matrix.GetRow(center);
        foreach (var j in members)
            clusterSum += row[j];
        return clusterSum / members.Length;
    }

    public double Aggregate(double clusterSum, int clusterCount, double alpha)
        => clusterSum - alpha * clusterCount;
}
