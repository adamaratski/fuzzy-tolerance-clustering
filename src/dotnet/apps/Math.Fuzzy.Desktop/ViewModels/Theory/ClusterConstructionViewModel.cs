using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Desktop.Services;
using MathFuzzy.Desktop.ViewModels.Inference;
using MathFuzzy.Desktop.ViewModels.Results;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.MembershipFunctions;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Globalization;

namespace MathFuzzy.Desktop.ViewModels.Theory;

/// <summary>One quantity of the antecedent construction with the equation it comes from.</summary>
public sealed record ConstructionRow(string Symbol, string Value, string Meaning, string Equation);

/// <summary>
/// "From cluster" tab: how the antecedent term of one fuzzy cluster on one attribute is built
/// from the α-level set and the kernel (Viattchenin 2013, sec. 4.5.2, eqs. (4.19)–(4.26)),
/// drawn over the objects of the current clustering solution.
/// </summary>
public sealed partial class ClusterConstructionViewModel : ObservableObject
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly IWorkspaceService _workspace;
    private bool _loading;

    public IReadOnlyList<string> ShapeNames { get; } = ["Trapezoidal (monograph)", "Triangular", "Gaussian"];

    [ObservableProperty] private IReadOnlyList<string> _solutions = [];
    [ObservableProperty] private int _selectedSolution = -1;
    [ObservableProperty] private IReadOnlyList<string> _clusters = [];
    [ObservableProperty] private int _selectedCluster = -1;
    [ObservableProperty] private IReadOnlyList<string> _features = [];
    [ObservableProperty] private int _selectedFeature = -1;
    [ObservableProperty] private int _selectedShape;

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _emptyMessage = "";
    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private IReadOnlyList<ConstructionRow> _rows = [];
    [ObservableProperty] private string _note = "";

    public ClusterConstructionViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.DataChanged    += (_, _) => Refresh();
        _workspace.ResultsChanged += (_, _) => Refresh();
        Refresh();
    }

    partial void OnSelectedSolutionChanged(int value)
    {
        if (_loading) return;
        RefreshClusters();
        Rebuild();
    }

    partial void OnSelectedClusterChanged(int value) { if (!_loading) Rebuild(); }
    partial void OnSelectedFeatureChanged(int value) { if (!_loading) Rebuild(); }
    partial void OnSelectedShapeChanged(int value)   { if (!_loading) Rebuild(); }

    private void Refresh()
    {
        var workset = _workspace.ActiveWorkset;
        _loading = true;
        IReadOnlyList<string> features = workset?.RawData is null ? [] : InferencePageViewModel.FeatureNames(workset);
        if (!features.SequenceEqual(Features))
        {
            Features = features;
            SelectedFeature = features.Count > 0 ? 0 : -1;
        }
        Solutions = (workset?.Results ?? []).Select((result, rank) =>
            $"#{rank + 1} · c = {result.ClusterCount} · α = {result.Alpha.ToString("0.#####", Invariant)}").ToList();
        SelectedSolution = Solutions.Count > 0 ? 0 : -1;
        RefreshClusters();
        _loading = false;
        Rebuild();
    }

    private void RefreshClusters()
    {
        bool wasLoading = _loading;
        _loading = true;
        var result = CurrentResult();
        Clusters = result is null ? [] : Enumerable.Range(1, result.ClusterCount).Select(number => $"C{number}").ToList();
        SelectedCluster = Clusters.Count > 0 ? 0 : -1;
        _loading = wasLoading;
    }

    private ClusterResult? CurrentResult()
    {
        var results = _workspace.ActiveWorkset?.Results ?? [];
        return SelectedSolution >= 0 && SelectedSolution < results.Count ? results[SelectedSolution] : null;
    }

    private void Rebuild()
    {
        var data = _workspace.RawData;
        var result = CurrentResult();
        if (data is null || result is null || SelectedCluster < 0 || SelectedFeature < 0)
        {
            Show(data is null
                ? "Open a dataset with raw features: terms are built in attribute space."
                : "Run a clustering on the dashboard to see how a fuzzy cluster becomes an antecedent term.");
            return;
        }
        if (result.Membership.GetLength(0) != data.Rows)
        {
            Show("The selected solution clusters attributes (dimension reduction), not objects.");
            return;
        }
        if (!(result.Alpha > 0 && result.Alpha <= 1))
        {
            Show($"The solution has α = {result.Alpha}; the construction needs α ∈ (0, 1].");
            return;
        }

        int cluster = SelectedCluster, feature = SelectedFeature;
        var sets = ClusterRuleExtractor.Describe(result, cluster);
        if (sets.Support.Length == 0)
        {
            Show($"The α-level set of C{cluster + 1} is empty.");
            return;
        }
        HasData = true;

        double alpha = result.Alpha;
        var shape = SelectedShape switch { 1 => MfShape.Triangular, 2 => MfShape.Gaussian, _ => MfShape.Trapezoidal };
        var term = ClusterRuleExtractor.FitTerm(data, feature, sets, result.Centers[cluster], alpha, shape);
        var trapezoid = (TrapezoidalMf)ClusterRuleExtractor.FitTerm(data, feature, sets, result.Centers[cluster], alpha, MfShape.Trapezoidal);

        double supportMin = sets.Support.Min(objectIndex => data[objectIndex, feature]);
        double supportMax = sets.Support.Max(objectIndex => data[objectIndex, feature]);
        double coreMin = trapezoid.LeftShoulder, coreMax = trapezoid.RightShoulder;
        double footLeft = trapezoid.LeftFoot, footRight = trapezoid.RightFoot;

        var model = new PlotModel { Background = DesignPalette.PlotBackground };
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightMiddle, LegendPlacement = LegendPlacement.Inside,
            LegendOrientation = LegendOrientation.Vertical, LegendFontSize = 10, LegendTextColor = DesignPalette.AxisText,
            LegendBackground = OxyColor.FromAColor(0xE0, OxyColors.White), LegendBorder = DesignPalette.AxisLine,
        });

        // objects: membership in the cluster (whole attribute space) against this attribute
        var clusterColor = MfPlotting.ColorFor(cluster);
        var others  = new ScatterSeries { Title = "other objects", MarkerType = MarkerType.Circle, MarkerSize = 2.5, MarkerFill = OxyColor.Parse("#B4B2A9") };
        var members = new ScatterSeries { Title = "members, μ ≥ α", MarkerType = MarkerType.Circle, MarkerSize = 3.5, MarkerFill = clusterColor };
        var kernel  = new ScatterSeries
        {
            Title = "kernel (typical points)", MarkerType = MarkerType.Diamond, MarkerSize = 7,
            MarkerFill = OxyColor.Parse("#2C2C2A"), MarkerStroke = OxyColors.White, MarkerStrokeThickness = 1.2,
        };
        var supportSet = sets.Support.ToHashSet();
        var kernelSet = sets.Kernel.ToHashSet();
        for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
        {
            var point = new ScatterPoint(data[objectIndex, feature], result.Membership[objectIndex, cluster]);
            if (kernelSet.Contains(objectIndex)) kernel.Points.Add(point);
            else if (supportSet.Contains(objectIndex)) members.Points.Add(point);
            else others.Points.Add(point);
        }

        // construction: lines through (core, 1) and (support bound, 1 − α) extended to μ = 0
        var leftLine  = new LineSeries { Title = "(4.21)–(4.22) construction", Color = DesignPalette.Coral, LineStyle = LineStyle.Dash, StrokeThickness = 1.2 };
        leftLine.Points.Add(new DataPoint(footLeft, 0));
        leftLine.Points.Add(new DataPoint(coreMin, 1));
        var rightLine = new LineSeries { Color = DesignPalette.Coral, LineStyle = LineStyle.Dash, StrokeThickness = 1.2 };
        rightLine.Points.Add(new DataPoint(coreMax, 1));
        rightLine.Points.Add(new DataPoint(footRight, 0));
        var anchors = new ScatterSeries
        {
            Title = "(x̂min, 1 − α), (x̂max, 1 − α)", MarkerType = MarkerType.Circle, MarkerSize = 4.5,
            MarkerFill = OxyColors.White, MarkerStroke = DesignPalette.Coral, MarkerStrokeThickness = 1.5,
        };
        anchors.Points.Add(new ScatterPoint(supportMin, 1 - alpha));
        anchors.Points.Add(new ScatterPoint(supportMax, 1 - alpha));

        double lower = System.Math.Min(term.Support.Min, footLeft), upper = System.Math.Max(term.Support.Max, footRight);
        for (int row = 0; row < data.Rows; row++)
        {
            lower = System.Math.Min(lower, data[row, feature]);
            upper = System.Math.Max(upper, data[row, feature]);
        }
        double padding = (upper - lower) * 0.03 + 1e-9;
        var curve = new LineSeries { Title = $"term C{cluster + 1} ({shape.ToString().ToLowerInvariant()})", Color = clusterColor, StrokeThickness = 2.5 };
        foreach (double position in MfPlotting.SamplePoints(term, lower - padding, upper + padding))
            curve.Points.Add(new DataPoint(position, term.Evaluate(position)));

        model.Series.Add(others);
        model.Series.Add(curve);
        model.Series.Add(leftLine);
        model.Series.Add(rightLine);
        model.Series.Add(members);
        model.Series.Add(kernel);
        model.Series.Add(anchors);

        AddLevel(model, alpha, $"α = {alpha.ToString("0.####", Invariant)}", DesignPalette.Blue600);
        AddLevel(model, 1 - alpha, $"1 − α = {(1 - alpha).ToString("0.####", Invariant)}", DesignPalette.Coral);
        AddBound(model, supportMin, "x̂min");
        AddBound(model, supportMax, "x̂max");

        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Bottom, Features[feature], lower - padding, upper + padding));
        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Left, "μ", -0.03, 1.08));
        Plot = model;

        string Number(double value) => value.ToString("0.####", Invariant);
        var rows = new List<ConstructionRow>
        {
            new("α", alpha.ToString("0.#####", Invariant), "tolerance threshold of the allotment", ""),
            new("h(A)", Number(sets.Height), "height: largest membership in the cluster", ""),
            new("|A_α|", sets.Support.Length.ToString(Invariant), "α-level set: objects of the cluster with μ ≥ α", "Def. 1.5"),
            new("|K|", sets.Kernel.Length.ToString(Invariant), "kernel: typical points, μ = h", ""),
            new("x̂min", Number(supportMin), "least value of the attribute over A_α", "(4.19)"),
            new("x̂max", Number(supportMax), "largest value of the attribute over A_α", "(4.20)"),
            new("m̲", Number(coreMin), "least value over the kernel; μ(m̲) = 1", "(4.23), (4.25)"),
            new("m̄", Number(coreMax), "largest value over the kernel; μ(m̄) = 1", "(4.24), (4.26)"),
            new("a̲", Number(footLeft), "left foot: μ(x̂min) = 1 − α, μ(a̲) = 0", "(4.21)"),
            new("ā", Number(footRight), "right foot: μ(x̂max) = 1 − α, μ(ā) = 0", "(4.22)"),
        };
        if (shape != MfShape.Trapezoidal)
            rows.Add(new("term", string.Join(", ", term.Parameters.Select(Number)),
                shape == MfShape.Triangular
                    ? "triangle (a, b, c), apex at the typical point — extension"
                    : "Gaussian (c, σ), μ = 1 − α at the farther support bound — extension", ""));
        Rows = rows;

        var notes = new List<string>
        {
            "Points: membership μₗ(xᵢ) of every object in the cluster (computed over all attributes), plotted against this attribute. "
            + "Curve: the antecedent term of the rule on this attribute.",
        };
        if (sets.Kernel.Length == 1) notes.Add("One typical point: m̲ = m̄, the trapezoid degenerates to a triangle.");
        if (sets.Height < 1 - 1e-12)
            notes.Add($"Subnormal cluster (h = {Number(sets.Height)} < 1): the monograph requires the height to be taken into account "
                      + "but gives no formula, so the term keeps height 1.");
        Note = string.Join(" ", notes);
    }

    private void Show(string message)
    {
        HasData = false;
        EmptyMessage = message;
        Plot = null;
        Rows = [];
        Note = "";
    }

    private static void AddLevel(PlotModel model, double level, string text, OxyColor color) =>
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal, Y = level, Color = color, LineStyle = LineStyle.Dot,
            Text = text, TextColor = color, TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
            TextLinePosition = 1,
        });

    private static void AddBound(PlotModel model, double position, string text) =>
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical, X = position, Color = DesignPalette.AxisLine, LineStyle = LineStyle.Dash,
            Text = text, TextColor = DesignPalette.AxisText, TextOrientation = AnnotationTextOrientation.Horizontal,
            TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left, TextLinePosition = 0.5,
        });
}
