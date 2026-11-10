using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Desktop.Services;

public sealed class AlgorithmRunnerService(IWorkspaceService workspace) : IAlgorithmRunnerService
{
    public async Task<RunOutcome> RunAsync(
        AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct)
    {
        var workset = workspace.ActiveWorkset
            ?? throw new InvalidOperationException("No data loaded. Open a data file or a tolerance matrix first.");
        var data = workset.RawData;
        var tolerance = workset.IsMatrixInput ? workset.InputMatrix : null;
        if (data is null && tolerance is null)
            throw new InvalidOperationException("No data loaded. Open a data file or a tolerance matrix first.");

        progress.Report(new($"Running {options.Algorithm}…"));
        var diagnostics = new SearchDiagnostics();

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
            // the bound w is a parameter of D-PAFC only; D-AFC(c) relaxes w by its own rule
            MaxIntersections: options.Algorithm == "dpafc" ? options.MaxIntersections : null,
            MinClusterPower:  options.MinClusterPower,
            MaxClusterPower:  options.MaxClusterPower,
            Diagnostics:      diagnostics);

        var results = await Task.Run(
            () => data is not null
                ? AlgorithmFactory.Run(options.Algorithm, data, algOptions, ct)
                : AlgorithmFactory.Run(options.Algorithm, tolerance!, algOptions, ct),
            ct);

        var limited = options.MaxResults > 0 && !options.Algorithm.StartsWith("hafc-", StringComparison.Ordinal)
            ? (IReadOnlyList<ClusterResult>)results.Take(options.MaxResults).ToList()
            : results;

        progress.Report(new(diagnostics.BudgetExhausted
            ? $"Search stopped at its node budget ({diagnostics.NodesVisited:N0} nodes): {limited.Count} solution(s) found so far, the result may be incomplete."
            : $"Done. {limited.Count} solution(s) found.", 1.0));
        return new RunOutcome(limited, diagnostics.BudgetExhausted, diagnostics.NodesVisited);
    }
}
