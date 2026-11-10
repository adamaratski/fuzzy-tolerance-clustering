using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Distance;

namespace MathFuzzy.Desktop.Services;

public sealed record AlgorithmRunOptions(
    string Algorithm,
    int? ClusterCount,
    IDistance Distance,
    string Normalization,
    string? TagaOperator,
    double? UserAlpha,
    string Criterion = "f1",
    int MaxResults = 200,
    double? Accuracy = null,
    int MaxIntersections = 0,
    int MinClusterPower = 1
);

/// <summary>Structured run progress: phase text plus optional 0..1 completion fraction.</summary>
public sealed record RunProgress(string Phase, double? Fraction = null);

public interface IAlgorithmRunnerService
{
    Task<IReadOnlyList<ClusterResult>> RunAsync(AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct);
}
