using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Desktop.Services;

public sealed class AlgorithmRunnerService(IWorkspaceService workspace) : IAlgorithmRunnerService
{
    public async Task<IReadOnlyList<ClusterResult>> RunAsync(
        AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct)
    {
        var data = workspace.RawData
            ?? throw new InvalidOperationException("No data loaded. Load a CSV file first.");

        progress.Report(new($"Running {options.Algorithm}…"));

        var algOptions = new AlgorithmOptions(
            DistanceOverride: options.Distance,
            Normalization:    options.Normalization,
            ClusterCount:     options.ClusterCount,
            TagaOperator:     options.TagaOperator,
            UserAlpha:        options.UserAlpha,
            Criterion:        options.Criterion,
            Accuracy:         options.Accuracy,
            LevelProgress:    (done, total) => progress.Report(new(
                $"Level scan · {done:N0} / {total:N0} levels",
                total > 0 ? (double)done / total : null)),
            MaxIntersections: options.MaxIntersections,
            MinClusterPower:  options.MinClusterPower);

        var results = await Task.Run(
            () => AlgorithmFactory.Run(options.Algorithm, data, algOptions, ct),
            ct);

        var limited = options.MaxResults > 0 && options.Algorithm != "hafc-tc"
            ? (IReadOnlyList<ClusterResult>)results.Take(options.MaxResults).ToList()
            : results;

        progress.Report(new($"Done. {limited.Count} solution(s) found.", 1.0));
        return limited;
    }
}
