using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Relational;

/// <summary>Clustering algorithm that operates directly on a fuzzy tolerance matrix.</summary>
public interface IRelationalClusterer
{
    IReadOnlyList<ClusterResult> Run(FuzzyToleranceMatrix matrix, CancellationToken ct = default);
}
