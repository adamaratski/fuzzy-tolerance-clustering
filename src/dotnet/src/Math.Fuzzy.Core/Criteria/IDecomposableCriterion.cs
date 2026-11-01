using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Criteria;

/// <summary>
/// Criterion whose value decomposes into independent per-cluster contributions:
/// Evaluate(...) == Aggregate(Σₗ EvaluateCluster(centerₗ, Cₗ), c, alpha).
/// Search engines exploit this to cache the per-candidate contribution once per
/// alpha level instead of re-evaluating the whole allotment at every leaf.
/// Aggregate must be non-decreasing in clusterSum for fixed c and alpha: H-AFC-TC
/// relies on it to maximise each cluster's contribution independently.
/// </summary>
public interface IDecomposableCriterion : ICriterion
{
    /// <summary>Contribution of a single cluster (center + its member set).</summary>
    double EvaluateCluster(FuzzyToleranceMatrix matrix, int center, int[] members, double alpha);

    /// <summary>Combines the summed cluster contributions into the criterion value.</summary>
    double Aggregate(double clusterSum, int clusterCount, double alpha);
}
