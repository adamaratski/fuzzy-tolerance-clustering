using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Desktop.Services;

public sealed class AlgorithmRunnerService(IWorkspaceService workspace) : IAlgorithmRunnerService
{
    private static async Task<RunOutcome> RunIntuitionisticAsync(
        Workset workset, AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct)
    {
        progress.Report(new($"Running {options.Algorithm}…"));
        var diagnostics = new SearchDiagnostics();
        var algOptions = new AlgorithmOptions(
            ClusterCount:          options.ClusterCount,
            MaxResults:            options.MaxResults > 0 ? options.MaxResults : 200,
            MaxSearchNodes:        new MathFuzzy.Clustering.Algorithms.Intuitionistic.IntuitionisticOptions().MaxSearchNodes,
            Diagnostics:           diagnostics,
            ThresholdScanOrder:    options.ThresholdScanOrder,
            IntuitionisticMeasure: workset.IntuitionisticData is null ? null : options.IntuitionisticMeasure);

        var results = await Task.Run(
            () => workset.IntuitionisticData is { } data
                ? AlgorithmFactory.RunIntuitionistic(options.Algorithm, data, algOptions, ct)
                : AlgorithmFactory.RunIntuitionistic(options.Algorithm, workset.IntuitionisticTolerance!, algOptions, ct),
            ct);

        progress.Report(new($"Done. {results.Count} solution(s) found.", 1.0));
        return new RunOutcome(results, diagnostics.BudgetExhausted, diagnostics.NodesVisited)
        {
            Frontier = [.. diagnostics.AdmissiblePairs.Where(pair => pair.OnFrontier).OrderBy(pair => pair.Alpha)]
        };
    }

    public async Task<RunOutcome> RunAsync(
        AlgorithmRunOptions options, IProgress<RunProgress> progress, CancellationToken ct)
    {
        var workset = workspace.ActiveWorkset
            ?? throw new InvalidOperationException("No data loaded. Open a data file or a tolerance matrix first.");
        bool intuitionisticAlgorithm = AlgorithmFactory.IsIntuitionistic(options.Algorithm);
        if (intuitionisticAlgorithm != workset.IsIntuitionistic)
            throw new InvalidOperationException(intuitionisticAlgorithm
                ? "This algorithm needs intuitionistic fuzzy data. Open a pair of membership and non-membership matrices or intuitionistic attribute data (Open data)."
                : "The active workset holds intuitionistic fuzzy data. Choose an algorithm of the Intuitionistic category, or open ordinary data.");
        if (intuitionisticAlgorithm)
            return await RunIntuitionisticAsync(workset, options, progress, ct);

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
