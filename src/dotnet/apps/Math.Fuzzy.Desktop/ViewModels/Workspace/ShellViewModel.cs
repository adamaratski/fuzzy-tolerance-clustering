using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Desktop.Services;
using MathFuzzy.Desktop.ViewModels.Algorithm;
using MathFuzzy.Desktop.ViewModels.Data;
using MathFuzzy.Desktop.ViewModels.Inference;
using MathFuzzy.Desktop.ViewModels.Theory;
using MathFuzzy.Desktop.ViewModels.Results;
using MathFuzzy.Core.Matrix;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MathFuzzy.Desktop.ViewModels.Workspace;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IWorkspaceService       _workspace;
    private readonly IAlgorithmRunnerService _runner;
    private readonly ResultsPanelViewModel   _resultsPanel;
    private CancellationTokenSource? _cts;
    // Ticks the elapsed time shown next to the Stop button while a run is in progress.
    private readonly DispatcherTimer _runClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _runStarted;
    private string   _runAlgorithm = "";

    [ObservableProperty] string  _statusMessage = "Ready. Open a CSV file to start.";
    [ObservableProperty] string  _dataInfo = "";
    [ObservableProperty] bool    _isBusy = false;
    [ObservableProperty] object? _selectedTab;
    [ObservableProperty] WorksetViewModel? _activeWorkset;
    [ObservableProperty] double  _progressValue;
    [ObservableProperty] bool    _progressIndeterminate;
    [ObservableProperty] string? _progressPhaseText;
    [ObservableProperty] string  _runningText = "";
    // Best-result summary (shown below tabs after a run)
    [ObservableProperty] bool    _hasBestResult;
    [ObservableProperty] string  _bestResultSummary = "";
    [ObservableProperty] string  _bestC = "—";
    [ObservableProperty] string  _bestPurity = "—";
    [ObservableProperty] string  _bestAri = "—";
    [ObservableProperty] IReadOnlyList<string> _bestCentres = [];
    // Page navigation
    [ObservableProperty] string _currentPage = "dashboard";
    [ObservableProperty] string _dataPreview = "";

    public ObservableCollection<WorksetViewModel> Worksets { get; } = [];

    public DataPanelViewModel      DataPanel      { get; }
    public AlgorithmPanelViewModel AlgorithmPanel { get; }
    public VisualizePanelViewModel VisualizePanel { get; }
    public FuzzySetsPageViewModel  FuzzySets      { get; }
    public InferencePageViewModel  Inference      { get; }
    public ResourceMonitorViewModel ResourceMonitor { get; }
    public IReadOnlyList<ResultTabViewModel> ResultTabs { get; }

    public string PageTitle => CurrentPage switch
    {
        "data"      => "Data & worksets",
        "visualize" => "Visualize",
        "mf"        => "Fuzzy sets",
        "fis"       => "Inference (FIS)",
        "settings"  => "Settings",
        _           => "Fuzzy Clustering",
    };

    public bool IsDashboard => CurrentPage == "dashboard";

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(IsDashboard));
    }

    [RelayCommand]
    private void Navigate(string tag)
    {
        // Algorithm-category items select the category's default algorithm
        // and land on the dashboard where the run happens.
        switch (tag)
        {
            // a category keeps the variant chosen in it; a new category starts with its first algorithm
            case "cat-relational":   SelectCategory("Relational", "D-AFC");          break;
            case "cat-prototype":    SelectCategory("Prototype-based", "D-AFC-TC");  break;
            case "cat-hierarchical": SelectCategory("Hierarchical", "H-AFC-TC");     break;
            case "cat-dr":           SelectCategory("Dim. reduction", "D-PAFC-DR");  break;
            case "cat-ifs":          SelectCategory("Intuitionistic", "D-PAIFC");    break;
            default:                 CurrentPage = tag; break;
        }
    }

    private void SelectCategory(string category, string defaultAlgorithm)
    {
        if (AlgorithmPanel.CategoryText != category) AlgorithmPanel.SelectedAlgorithm = defaultAlgorithm;
        CurrentPage = "dashboard";
    }

    public ShellViewModel(
        IWorkspaceService       workspace,
        IAlgorithmRunnerService runner,
        DataPanelViewModel      dataPanel,
        AlgorithmPanelViewModel algorithmPanel,
        ResultsPanelViewModel   resultsPanel,
        VisualizePanelViewModel visualizePanel,
        FuzzySetsPageViewModel  fuzzySets,
        InferencePageViewModel  inference,
        ResourceMonitorViewModel resourceMonitor)
    {
        _workspace    = workspace;
        _runner       = runner;
        _resultsPanel = resultsPanel;
        DataPanel      = dataPanel;
        AlgorithmPanel = algorithmPanel;
        VisualizePanel = visualizePanel;
        FuzzySets      = fuzzySets;
        Inference      = inference;
        ResourceMonitor = resourceMonitor;
        ResultTabs     = resultsPanel.Tabs;
        SelectedTab    = ResultTabs[0];
        _runClock.Tick += (_, _) => UpdateRunningText();
        _workspace.ResultsChanged += (_, _) => RefreshExportState();
        _workspace.DataChanged    += (_, _) => RefreshExportState();

        LoadSampleDatasets();
    }

    // ─── Workset switching ────────────────────────────────────────────────

    partial void OnActiveWorksetChanged(WorksetViewModel? value)
    {
        if (value?.Model is not { } ws) return;
        if (!ReferenceEquals(_workspace.ActiveWorkset, ws))
        {
            _workspace.Activate(ws);
            StatusMessage = $"Active workset: {ws.Name} · {ws.Meta}";   // do not leave another set's "Loaded: …"
        }
        RefreshFromWorkset(ws);
    }

    private void RefreshFromWorkset(Workset ws)
    {
        DataInfo    = ws.Meta;
        DataPreview = BuildPreview(ws);

        _resultsPanel.ToleranceMatrixTab.Clear();
        if (ws.ToleranceMatrix is { } tm)
        {
            if (ws.ClosureApplied) _resultsPanel.ToleranceMatrixTab.UpdateClosure(MatrixTo2D(tm));
            else                   _resultsPanel.ToleranceMatrixTab.Update(MatrixTo2D(tm));
        }

        if (ws.Results.Count > 0)
        {
            PushResultsToTabs(ws.Results);
            UpdateBestBlock(ws.Results, ws);
        }
        else
        {
            _resultsPanel.ClusterTableTab.Update([]);
            _resultsPanel.MembershipTab.Update([]);
            _resultsPanel.ValidityTab.Update([]);
            _resultsPanel.ValidityTab.ClearExternal();
            _resultsPanel.DendrogramTab.Clear();
            HasBestResult = false;
        }
    }

    // ─── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenCsv()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title  = "Open data file"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            LoadCsvFromPath(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Loads a CSV without a dialog (command-line argument or drag &amp; drop).</summary>
    public void LoadCsvFromPath(string path)
    {
        var ws = _workspace.LoadCsv(path);
        var vm = new WorksetViewModel { Model = ws, Name = ws.Name, Meta = ws.Meta };
        Worksets.Add(vm);
        ActiveWorkset = vm;
        StatusMessage = $"Loaded: {ws.Name}";
    }

    /// <summary>Opens an intuitionistic fuzzy tolerance: the membership matrix first, then the non-membership matrix.</summary>
    [RelayCommand]
    private void OpenIntuitionisticRelation()
    {
        var membership = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title  = "Intuitionistic tolerance, file 1 of 2: membership matrix"
        };
        if (membership.ShowDialog() != true) return;
        var nonMembership = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title  = "Intuitionistic tolerance, file 2 of 2: non-membership matrix",
            InitialDirectory = Path.GetDirectoryName(membership.FileName)
        };
        if (nonMembership.ShowDialog() != true) return;

        try
        {
            AddIntuitionisticWorkset(_workspace.LoadIntuitionisticRelation(membership.FileName, nonMembership.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Opens intuitionistic attribute data (columns in pairs: membership, non-membership).</summary>
    [RelayCommand]
    private void OpenIntuitionisticData()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title  = "Open intuitionistic attribute data"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            AddIntuitionisticWorkset(_workspace.LoadIntuitionisticData(dlg.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddIntuitionisticWorkset(Workset ws)
    {
        var vm = new WorksetViewModel { Model = ws, Name = ws.Name, Meta = ws.Meta, IsMatrixInput = true };
        Worksets.Add(vm);
        ActiveWorkset = vm;
        if (!AlgorithmPanel.IsIntuitionistic) AlgorithmPanel.SelectedAlgorithm = "D-PAIFC";
        StatusMessage = $"Intuitionistic fuzzy data loaded: {ws.Name} · {ws.Meta}. The algorithms of the Intuitionistic category run on it.";
        SelectedTab = ResultTabs[0];
    }

    [RelayCommand]
    private void OpenMatrix()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title  = "Open tolerance matrix"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var ws = _workspace.LoadMatrix(dlg.FileName);
            var vm = new WorksetViewModel { Model = ws, Name = ws.Name, Meta = ws.Meta, IsMatrixInput = true };
            Worksets.Add(vm);
            ActiveWorkset = vm;
            StatusMessage = $"Matrix loaded: {ws.Name}  n={ws.ToleranceMatrix!.Size}";
            SelectedTab = ResultTabs[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BuildMatrix()
    {
        if (_workspace.RawData is null)
        {
            StatusMessage = "No data loaded. Open a CSV first.";
            return;
        }
        try
        {
            var o = AlgorithmPanel.BuildOptions();
            _workspace.BuildToleranceMatrix(o.Distance, o.Normalization, o.Accuracy);
            StatusMessage = "Tolerance matrix built.";
            if (_workspace.ToleranceMatrix is { } tm)
                _resultsPanel.ToleranceMatrixTab.Update(MatrixTo2D(tm));
            ActiveWorkset?.RefreshMeta();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Closure()
    {
        if (_workspace.ToleranceMatrix is null)
        {
            StatusMessage = "Build tolerance matrix first.";
            return;
        }
        try
        {
            _workspace.ComputeTransitiveClosure();
            StatusMessage = "Transitive closure computed.";
            if (_workspace.ToleranceMatrix is { } tm)
                _resultsPanel.ToleranceMatrixTab.UpdateClosure(MatrixTo2D(tm));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Run()
    {
        if (_workspace.RawData is null && _workspace.ActiveWorkset?.InputMatrix is null
            && _workspace.ActiveWorkset is not { IsIntuitionistic: true })
        {
            StatusMessage = "Open a data file or a tolerance matrix first.";
            return;
        }

        // the kind of data and the kind of algorithm must agree; said inline, it is not an algorithm failure
        bool intuitionisticData = _workspace.ActiveWorkset is { IsIntuitionistic: true };
        if (AlgorithmPanel.IsIntuitionistic != intuitionisticData)
        {
            StatusMessage = AlgorithmPanel.IsIntuitionistic
                ? "This algorithm needs intuitionistic fuzzy data: Open data → Intuitionistic tolerance (μ and ν matrices) or Intuitionistic attribute data."
                : "The active workset holds intuitionistic fuzzy data. Choose an algorithm of the Intuitionistic category, or select a workset with ordinary data.";
            return;
        }

        if (AlgorithmPanel.ValidationError is { } invalid)
        {
            StatusMessage = invalid;   // parameter problems are reported inline, not as an error dialog
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        _runAlgorithm = AlgorithmPanel.ChipText;
        _runStarted   = DateTime.Now;
        UpdateRunningText();
        _runClock.Start();
        ProgressPhaseText     = "Starting…";
        ProgressValue         = 0;
        ProgressIndeterminate = true;
        if (ActiveWorkset is { } aw) aw.Status = WorksetStatus.Running;

        try
        {
            var options  = AlgorithmPanel.BuildOptions();
            var progress = new Progress<RunProgress>(p =>
            {
                StatusMessage     = p.Phase;
                ProgressPhaseText = p.Phase;
                if (p.Fraction is { } f)
                {
                    ProgressIndeterminate = false;
                    ProgressValue = f * 100;
                }
            });
            var outcome = await _runner.RunAsync(options, progress, _cts.Token);
            var results = outcome.Results;

            _workspace.SetResults(results);
            PushResultsToTabs(results);
            if (ActiveWorkset is { } ws) ws.Status = WorksetStatus.Done;

            if (results.Count > 0)
                UpdateBestBlock(results, ActiveWorkset?.Model);
            else
                HasBestResult = false;   // do not leave the previous run's summary on screen

            UpdateDendrogram(options.Algorithm, results);
            await RefreshToleranceTabAsync(options);

            if (_workspace.Labels is { } lbl && results.Count > 0)
            {
                var ext = MathFuzzy.Clustering.Validation.ExternalValidator
                    .Compute(results[0], lbl.TrueLabels, lbl.ClassNames);
                _resultsPanel.ValidityTab.UpdateExternal(ext);
                BestPurity = ext.Purity.ToString("F2");
                BestAri    = ext.AdjustedRandIndex.ToString("F2");
                StatusMessage = $"Done — {results.Count} result(s). Purity={ext.Purity:F3} ARI={ext.AdjustedRandIndex:F3} NMI={ext.NormalizedMutualInfo:F3}";
            }
            else
            {
                _resultsPanel.ValidityTab.ClearExternal();
                BestPurity = "—";
                BestAri    = "—";
                StatusMessage = results.Count > 0
                    ? results[0].Beta is double bestBeta
                        ? $"Done — {results.Count} solution(s). Best: c={results[0].ClusterCount}, (α, β)=({results[0].Alpha:F4}, {bestBeta:F4}), F={results[0].Score:F4}"
                          + (results[0].IntersectionBound is int bound and > 0 ? $", w={bound}" : "")
                          + (ClusterResult.DescribeTie(results) is { } tie ? $". {tie}" : "")
                        : $"Done — {results.Count} solution(s). Best: c={results[0].ClusterCount}, F₁={results[0].Score:F4}"
                    : options.UserAlpha is { } ua && options.Algorithm == "dafc"
                        ? $"No valid clustering at α* = {ua.ToString(CultureInfo.InvariantCulture)}. Try α levels = All levels."
                        : "No valid clustering found for these parameters.";
            }

            if (outcome.SearchIncomplete)
                StatusMessage += $"  ⚠ The search stopped at its node budget ({outcome.NodesVisited:N0} nodes): better allotments may exist.";

            SelectedTab = options.Algorithm.StartsWith("hafc-", StringComparison.Ordinal) && _resultsPanel.DendrogramTab.HasResult
                ? _resultsPanel.DendrogramTab
                : _resultsPanel.ClusterTableTab;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Stopped after {DateTime.Now - _runStarted:m\\:ss}.";
            // the previous run's results are still shown, so the card keeps their status
            if (ActiveWorkset is { } ws)
                ws.Status = ws.Model?.Results.Count > 0 ? WorksetStatus.Done : WorksetStatus.Idle;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            if (ActiveWorkset is { } ws) ws.Status = WorksetStatus.Error;
            MessageBox.Show(ex.Message, "Algorithm error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _runClock.Stop();
            IsBusy = false;
            ProgressPhaseText     = null;
            ProgressIndeterminate = false;
            ProgressValue         = 0;
            _cts = null;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
        ProgressPhaseText = "Stopping…";
    }

    private void UpdateRunningText() =>
        RunningText = $"{_runAlgorithm} · {DateTime.Now - _runStarted:m\\:ss}";

    /// <summary>Export is possible once the active workset has results and no run is in progress.</summary>
    public bool CanExportResults => _workspace.Results.Count > 0 && !IsBusy;

    private bool CanExport(string? format) => CanExportResults;

    public string ExportToolTip => CanExportResults
        ? "Save the results of the active workset to JSON or CSV (Ctrl+E)"
        : IsBusy ? "Export is available when the run finishes" : "Nothing to export yet: run an algorithm on the dashboard";

    private void RefreshExportState()
    {
        ExportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanExportResults));
        OnPropertyChanged(nameof(ExportToolTip));
    }

    partial void OnIsBusyChanged(bool value) => RefreshExportState();

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export(string? format)
    {
        var results = _workspace.Results;
        if (results.Count == 0)
        {
            StatusMessage = "Nothing to export — run an algorithm first.";
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = format == "csv" ? "CSV (*.csv)|*.csv" : "JSON (*.json)|*.json",
            FileName = $"{Path.GetFileNameWithoutExtension(ActiveWorkset?.Name ?? "results")}-results",
            Title    = "Export results"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(dlg.FileName, ResultsToCsv(results), Encoding.UTF8);
            else
                File.WriteAllText(dlg.FileName, ResultsToJson(results), Encoding.UTF8);

            StatusMessage = $"Exported {results.Count} solution(s) → {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private string ResultsToJson(IReadOnlyList<ClusterResult> results)
    {
        var payload = new
        {
            dataset   = ActiveWorkset?.Name,
            algorithm = AlgorithmPanel.ChipText,
            exported  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            solutions = results.Select((r, i) => new
            {
                rank      = i + 1,
                c         = r.ClusterCount,
                alpha     = r.Alpha,
                beta      = r.Beta,
                intersectionBound = r.IntersectionBound,
                score     = r.Score,
                criterion = r.CriterionName,
                tiebreaker = r.TiebreakerScore,
                clusters  = r.MemberSets.Select((s, k) => new
                {
                    centre  = r.Centers[k] + 1,
                    size    = s.Length,
                    members = s.Select(o => o + 1).ToArray()
                })
            })
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ResultsToCsv(IReadOnlyList<ClusterResult> results)
    {
        var sb = new StringBuilder();
        bool hasBeta = results.Any(r => r.Beta.HasValue);
        sb.AppendLine(hasBeta ? "rank,c,alpha,beta,score,tiebreaker,centres,sizes" : "rank,c,alpha,score,tiebreaker,centres,sizes");
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.Append(i + 1).Append(',')
              .Append(r.ClusterCount).Append(',')
              .Append(r.Alpha.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
            if (hasBeta) sb.Append(r.Beta?.ToString("F6", CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.Append(r.Score.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.TiebreakerScore?.ToString("F6", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append('"').Append(string.Join(" ", r.Centers.Select(c => c + 1))).Append('"').Append(',')
              .Append('"').Append(string.Join(" ", r.MemberSets.Select(s => s.Length))).Append('"')
              .AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>First rows of intuitionistic attribute data: a pair (μ, ν) per attribute.</summary>
    private static string BuildIntuitionisticPreview(MathFuzzy.Core.Intuitionistic.IntuitionisticDataMatrix data)
    {
        var sb = new StringBuilder();
        sb.Append("row".PadRight(6));
        for (int attribute = 0; attribute < data.Attributes; attribute++)
            sb.Append($"(μ, ν) {data.AttributeNames[attribute]}".PadLeft(16));
        sb.AppendLine();
        int rows = System.Math.Min(data.Objects, 12);
        for (int row = 0; row < rows; row++)
        {
            sb.Append((row + 1).ToString(CultureInfo.InvariantCulture).PadRight(6));
            for (int attribute = 0; attribute < data.Attributes; attribute++)
            {
                string pair = string.Create(CultureInfo.InvariantCulture,
                    $"({data.Membership(row, attribute):0.##}, {data.NonMembership(row, attribute):0.##})");
                sb.Append(pair.PadLeft(16));
            }
            sb.AppendLine();
        }
        if (data.Objects > rows) sb.AppendLine($"… {data.Objects - rows} more rows");
        return sb.ToString();
    }

    /// <summary>First rows of the raw data as fixed-width text (Data page preview).</summary>
    private static string BuildPreview(Workset ws)
    {
        if (ws.IntuitionisticData is { } ifs) return BuildIntuitionisticPreview(ifs);
        if (ws.IsIntuitionistic) return "Intuitionistic fuzzy tolerance: a membership and a non-membership matrix — no attributes.";
        if (ws.RawData is not { } data) return ws.IsMatrixInput ? "Matrix input — no raw features." : "";

        var sb = new StringBuilder();
        sb.Append("row".PadRight(6));
        for (int j = 0; j < data.Cols; j++) sb.Append($"x{j + 1}".PadLeft(9));
        if (ws.Labels is not null) sb.Append("  class");
        sb.AppendLine();

        int rows = System.Math.Min(data.Rows, 12);
        for (int i = 0; i < rows; i++)
        {
            sb.Append((i + 1).ToString().PadRight(6));
            for (int j = 0; j < data.Cols; j++)
                sb.Append(data[i, j].ToString("0.###", CultureInfo.InvariantCulture).PadLeft(9));
            if (ws.Labels is { } lbl)
                sb.Append("  ").Append(lbl.ClassNames[lbl.TrueLabels[i]]);
            sb.AppendLine();
        }
        if (data.Rows > rows) sb.Append($"… {data.Rows - rows} more rows");
        return sb.ToString();
    }

    private void UpdateBestBlock(IReadOnlyList<ClusterResult> results, Workset? ws)
    {
        var best = results[0];
        HasBestResult = true;
        BestC = best.ClusterCount.ToString();
        int n = ws?.RawData?.Rows ?? ws?.ToleranceMatrix?.Size ?? 0;
        string name = Path.GetFileNameWithoutExtension(ws?.Name ?? "");
        BestResultSummary =
            best.Beta is double beta
                ? $"{AlgorithmPanel.ChipText} · c={best.ClusterCount} · (α, β)=({best.Alpha:F2}, {beta:F2}) · F={best.Score:F3} · {name} n={n}"
                : $"{AlgorithmPanel.ChipText} · c={best.ClusterCount} · α*={best.Alpha:F2} · F₁={best.Score:F3} · {name} n={n}";
        BestCentres = best.Centers
            .Select((c, i) =>
                $"C{ResultTabViewModel.Sub(i + 1)} obj {c + 1} · n={best.MemberSets[i].Length}")
            .ToList();
    }

    /// <summary>Largest n for which T and T̃ are rendered as heatmaps (closure is O(n³)).</summary>
    private const int MaxHeatmapSize = 1200;

    /// <summary>Shows the T and T̃ actually used by the run (same distance, normalisation, ε).</summary>
    private async Task RefreshToleranceTabAsync(AlgorithmRunOptions options)
    {
        if (_workspace.ActiveWorkset is not { RawData: { } raw } ws || raw.Rows > MaxHeatmapSize) return;
        ProgressPhaseText = "Building T and T̃ for the heatmap…";
        var (t, closure) = await Task.Run(() =>
        {
            var builder = new ToleranceMatrixBuilder(options.Distance,
                WorkspaceService.NormalizationFor(options.Normalization), options.Accuracy);
            var tm = builder.Build(raw);
            return (tm, new MaxMinTransitiveClosure().Compute(tm));
        });
        ws.ToleranceMatrix = t;
        ws.ClosureApplied  = false;
        var tab = _resultsPanel.ToleranceMatrixTab;
        tab.Clear();
        tab.Update(MatrixTo2D(t));
        tab.UpdateClosure(MatrixTo2D(closure));
    }

    private void UpdateDendrogram(string algorithm, IReadOnlyList<ClusterResult> results)
    {
        if (!algorithm.StartsWith("hafc-", StringComparison.Ordinal) || results.Count == 0)
        {
            _resultsPanel.DendrogramTab.Clear();
            return;
        }
        int n = results[0].Membership.GetLength(0);
        var root = new DendrogramNode(results.Min(r => r.Alpha), Enumerable.Range(0, n).ToArray(), []);
        _resultsPanel.DendrogramTab.Update(new HierarchicalResult(root, results));
    }

    private void PushResultsToTabs(IReadOnlyList<ClusterResult> results)
    {
        _resultsPanel.ClusterTableTab.Update(results);
        _resultsPanel.ValidityTab.Update(results);
        _resultsPanel.MembershipTab.Update(results);
    }

    private static double[,] MatrixTo2D(MathFuzzy.Core.Matrix.FuzzyToleranceMatrix matrix)
    {
        int size = matrix.Size;
        var data = new double[size, size];
        for (int i = 0; i < size; i++)
        for (int j = 0; j < size; j++)
            data[i, j] = matrix[i, j];
        return data;
    }

    /// <summary>Sample datasets shipped in the repository's datasets/ folder.</summary>
    private static readonly string[] SampleDatasets = ["iris_labeled.csv", "yang_wu.csv"];

    /// <summary>
    /// Opens the repository sample datasets as idle worksets, the first one active.
    /// The folder is looked up from the executable upwards; outside a checkout nothing is loaded.
    /// </summary>
    private void LoadSampleDatasets()
    {
        if (FindDatasetsFolder() is not { } dir) return;
        foreach (var file in SampleDatasets)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            try { LoadCsvFromPath(path); }
            catch (Exception ex) { StatusMessage = $"Could not load sample {file}: {ex.Message}"; return; }
        }
        if (Worksets.Count == 0) return;
        ActiveWorkset = Worksets[0];
        // the folder name only: the full path would expose the local directory layout in screenshots
        StatusMessage = $"Sample datasets loaded from the {Path.GetFileName(dir)} folder. Choose an algorithm and press Run.";
    }

    private static string? FindDatasetsFolder()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "datasets");
            if (File.Exists(Path.Combine(candidate, SampleDatasets[0]))) return candidate;
        }
        return null;
    }
}
