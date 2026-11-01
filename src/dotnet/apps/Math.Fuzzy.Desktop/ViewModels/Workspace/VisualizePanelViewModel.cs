using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Desktop.Services;
using MathFuzzy.Desktop.ViewModels.Results;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Globalization;

namespace MathFuzzy.Desktop.ViewModels.Workspace;

/// <summary>
/// Visualize page: 2-D feature scatter of the active workset, colored either by the
/// best clustering result of the last run or by the true class labels of the file.
/// </summary>
public sealed partial class VisualizePanelViewModel : ObservableObject
{
    /// <summary>Categorical cluster colors — mid stops of the design-system ramps.</summary>
    internal static readonly OxyColor[] ClusterColors =
    [
        OxyColor.Parse("#378ADD"), // blue
        OxyColor.Parse("#639922"), // green
        OxyColor.Parse("#D85A30"), // coral
        OxyColor.Parse("#7F77DD"), // purple
        OxyColor.Parse("#BA7517"), // amber
        OxyColor.Parse("#D4537E"), // pink
        OxyColor.Parse("#1D9E75"), // teal
        OxyColor.Parse("#E24B4A"), // red
    ];

    private static readonly OxyColor Uncolored = OxyColor.Parse("#B4B2A9");

    public const string ByClusters = "Clusters";
    public const string ByClasses  = "True classes";

    private readonly IWorkspaceService _workspace;
    private RawDataMatrix? _shownData;   // data the feature pickers were set up for
    private Workset?       _shownWorkset;

    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private IReadOnlyList<string> _features = [];
    [ObservableProperty] private int  _featureX;
    [ObservableProperty] private int  _featureY = 1;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private IReadOnlyList<string> _colorModes = [ByClusters];
    [ObservableProperty] private string _colorMode = ByClusters;
    /// <summary>What the colors mean for the current workset and mode.</summary>
    [ObservableProperty] private string _caption = "";

    public VisualizePanelViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.DataChanged    += (_, _) => Refresh(newResults: false);
        _workspace.ResultsChanged += (_, _) => Refresh(newResults: true);
        Refresh(newResults: false);
    }

    private bool _resetting;

    partial void OnFeatureXChanged(int value) { if (!_resetting) Rebuild(); }
    partial void OnFeatureYChanged(int value) { if (!_resetting) Rebuild(); }
    partial void OnColorModeChanged(string value) { if (!_resetting) Rebuild(); }

    private void Refresh(bool newResults)
    {
        var ws   = _workspace.ActiveWorkset;
        var data = ws?.RawData;
        HasData = data is not null;
        if (ws is null || data is null)
        {
            Plot = null; Features = []; Caption = ""; _shownData = null;
            return;
        }

        _resetting = true;
        if (!ReferenceEquals(_shownData, data))
        {
            // another workset: its own column names, first two features
            Features = ws.FeatureNames.Count == data.Cols
                ? ws.FeatureNames
                : Enumerable.Range(1, data.Cols).Select(i => $"x{i}").ToList();
            FeatureX   = 0;
            FeatureY   = data.Cols > 1 ? 1 : 0;
            _shownData = data;
        }
        bool switched = !ReferenceEquals(_shownWorkset, ws);
        _shownWorkset = ws;
        ColorModes = ws.Labels is not null ? [ByClusters, ByClasses] : [ByClusters];
        // A fresh run or another workset resets the coloring: clusters when there are
        // results, otherwise the labels; in between the user's choice is kept.
        if (newResults || switched || !ColorModes.Contains(ColorMode))
            ColorMode = ws.Results.Count == 0 && ws.Labels is not null ? ByClasses : ByClusters;
        _resetting = false;
        Rebuild();
    }

    private void Rebuild()
    {
        var ws   = _workspace.ActiveWorkset;
        var data = ws?.RawData;
        if (ws is null || data is null) { Plot = null; return; }

        int fx = System.Math.Clamp(FeatureX, 0, data.Cols - 1);
        int fy = System.Math.Clamp(FeatureY, 0, data.Cols - 1);

        var model = new PlotModel { Background = DesignPalette.PlotBackground };
        model.Legends.Add(new Legend
        {
            LegendPlacement  = LegendPlacement.Outside,
            LegendPosition   = LegendPosition.RightTop,
            LegendTextColor  = DesignPalette.AxisText,
            LegendFontSize   = 11,
            LegendBackground = OxyColors.Transparent,
            LegendBorder     = OxyColors.Transparent
        });

        var best = ws.Results.FirstOrDefault();
        if (ColorMode == ByClasses && ws.Labels is { } labels)
        {
            AddClassSeries(model, data, labels, fx, fy);
            Caption = $"Colored by the true class labels of {ws.Name} ({labels.ClassCount} classes).";
        }
        else if (best is { ClusterCount: > 0 })
        {
            AddClusterSeries(model, data, best, fx, fy);
            Caption = string.Create(CultureInfo.InvariantCulture,
                $"Colored by the best result of the last run on {ws.Name}: c = {best.ClusterCount}, α = {best.Alpha:0.####}, {best.CriterionName} = {best.Score:0.####}. Diamonds are typical points; ringed objects lie in more than one cluster.");
        }
        else
        {
            var all = Scatter("Objects", Uncolored);
            for (int i = 0; i < data.Rows; i++)
                all.Points.Add(new ScatterPoint(data[i, fx], data[i, fy]));
            model.Series.Add(all);
            Caption = $"No clustering result for {ws.Name} yet — run an algorithm on the Dashboard to color the objects by cluster.";
        }

        model.Axes.Add(Axis(AxisPosition.Bottom, Features.ElementAtOrDefault(fx) ?? $"x{fx + 1}"));
        model.Axes.Add(Axis(AxisPosition.Left,   Features.ElementAtOrDefault(fy) ?? $"x{fy + 1}"));
        Plot = model;
    }

    private static void AddClusterSeries(PlotModel model, RawDataMatrix data, ClusterResult best, int fx, int fy)
    {
        var coverage = new int[data.Rows];
        for (int k = 0; k < best.ClusterCount; k++)
        {
            var members = best.MemberSets[k];
            var series = Scatter($"C{k + 1} · n = {members.Length}", ClusterColors[k % ClusterColors.Length]);
            foreach (int obj in members)
            {
                series.Points.Add(new ScatterPoint(data[obj, fx], data[obj, fy]));
                coverage[obj]++;
            }
            model.Series.Add(series);
        }

        var unassigned = Scatter("Not in any cluster", Uncolored);
        var shared = new ScatterSeries
        {
            Title                 = "In ≥ 2 clusters",
            MarkerType            = MarkerType.Circle,
            MarkerSize            = 7,
            MarkerFill            = OxyColors.Transparent,
            MarkerStroke          = OxyColor.Parse("#2C2C2A"),
            MarkerStrokeThickness = 1.2
        };
        for (int i = 0; i < data.Rows; i++)
        {
            if (coverage[i] == 0) unassigned.Points.Add(new ScatterPoint(data[i, fx], data[i, fy]));
            if (coverage[i] > 1)  shared.Points.Add(new ScatterPoint(data[i, fx], data[i, fy]));
        }
        if (unassigned.Points.Count > 0) model.Series.Add(unassigned);
        if (shared.Points.Count > 0)     model.Series.Add(shared);

        // Typical points (cluster centres) as dark diamonds on top
        var centres = new ScatterSeries
        {
            Title                 = "Typical points",
            MarkerType            = MarkerType.Diamond,
            MarkerSize            = 7,
            MarkerFill            = OxyColor.Parse("#2C2C2A"),
            MarkerStroke          = OxyColors.White,
            MarkerStrokeThickness = 1.5
        };
        foreach (int c in best.Centers)
            centres.Points.Add(new ScatterPoint(data[c, fx], data[c, fy]));
        model.Series.Add(centres);
    }

    private static void AddClassSeries(PlotModel model, RawDataMatrix data, LabeledDataset labels, int fx, int fy)
    {
        for (int k = 0; k < labels.ClassCount; k++)
        {
            var series = Scatter("", ClusterColors[k % ClusterColors.Length]);
            for (int i = 0; i < data.Rows; i++)
                if (labels.TrueLabels[i] == k)
                    series.Points.Add(new ScatterPoint(data[i, fx], data[i, fy]));
            series.Title = $"{labels.ClassNames[k]} · n = {series.Points.Count}";
            model.Series.Add(series);
        }
    }

    private static ScatterSeries Scatter(string title, OxyColor fill) => new()
    {
        Title      = title,
        MarkerType = MarkerType.Circle,
        MarkerSize = 4,
        MarkerFill = fill
    };

    private static LinearAxis Axis(AxisPosition position, string title) => new()
    {
        Position       = position,
        Title          = title,
        MinimumPadding = 0.04,
        MaximumPadding = 0.04,
        TextColor      = DesignPalette.AxisText,
        TitleColor     = DesignPalette.AxisText,
        AxislineColor  = DesignPalette.AxisLine,
        TicklineColor  = DesignPalette.AxisLine
    };
}
