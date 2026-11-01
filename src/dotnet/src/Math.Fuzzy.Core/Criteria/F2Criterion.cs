using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Criteria;

/// <summary>
/// F2 = (1/c) · Σᵢ [ Σⱼ∈Cᵢ μT(cᵢ,xⱼ) / |Cᵢ| ]
/// Mean coverage across clusters, without the -α·c penalty.
/// Maximize.
/// </summary>
public sealed class F2Criterion : IDecomposableCriterion
{
    public static readonly F2Criterion Instance = new();

    public string Name => "F2";

    public double Evaluate(FuzzyToleranceMatrix matrix, int[] centers, int[][] memberSets, double alpha)
    {
        if (centers.Length == 0) return 0.0;

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
        => clusterCount == 0 ? 0.0 : clusterSum / clusterCount;
}
