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
    int MinClusterPower = 1,
    int MaxClusterPower = 0,
    string? ThresholdScanOrder = null,
    string? IntuitionisticMeasure = null
);

/// <summary>Structured run progress: phase text plus optional 0..1 completion fraction.</summary>
public sealed record RunProgress(string Phase, double? Fraction = null);

/// <summary>
/// Result of a run. <paramref name="SearchIncomplete"/> is true when the combination search
/// stopped at its node budget, so better allotments than the returned ones may exist.
/// </summary>
public sealed record RunOutcome(IReadOnlyList<ClusterResult> Results, bool SearchIncomplete, long NodesVisited);

public interface IAlgorithmRunnerService
{
    Task<RunOutcome> RunAsync(AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct);
}
