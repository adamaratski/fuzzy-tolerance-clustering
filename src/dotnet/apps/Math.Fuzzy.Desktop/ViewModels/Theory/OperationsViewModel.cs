using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.ViewModels.Results;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Globalization;
using System.Windows.Media;

namespace MathFuzzy.Desktop.ViewModels.Theory;

/// <summary>Crisp value of the result set under one defuzzification method.</summary>
public sealed record DefuzzificationRow(string Method, string Value, Brush Accent);

/// <summary>
/// "Operations" tab: intersection, union, complement and implication of two fuzzy sets with the
/// same operators the inference page offers, and the five defuzzifications of the result.
/// </summary>
public sealed partial class OperationsViewModel : ObservableObject, IMfHandleDragTarget
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const int Intersection = 0, Union = 1, Complement = 2, ImplicationOperation = 3;

    /// <summary>Marker colours of the defuzzification methods, in <see cref="Defuzzification"/> order.</summary>
    private static readonly OxyColor[] MethodColors =
    [
        DesignPalette.Coral, OxyColor.Parse("#7F77DD"), OxyColor.Parse("#1D9E75"), OxyColor.Parse("#BA7517"), OxyColor.Parse("#D4537E"),
    ];

    public FuzzySetEditorViewModel SetA { get; }
    public FuzzySetEditorViewModel SetB { get; }

    public IReadOnlyList<string> Operations { get; } = ["A ∩ B  (t-norm)", "A ∪ B  (s-norm)", "¬A  (complement)", "w ⇒ B  (implication)"];
    public IReadOnlyList<TNorm> TNorms { get; } = Enum.GetValues<TNorm>();
    public IReadOnlyList<SNorm> SNorms { get; } = Enum.GetValues<SNorm>();
    public IReadOnlyList<Implication> Implications { get; } = Enum.GetValues<Implication>();

    [ObservableProperty] private int _selectedOperation;
    [ObservableProperty] private TNorm _selectedTNorm;
    [ObservableProperty] private SNorm _selectedSNorm;
    [ObservableProperty] private Implication _selectedImplication;
    [ObservableProperty] private double _firing = 0.6;
    [ObservableProperty] private bool _compareAll;
    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private IReadOnlyList<DefuzzificationRow> _defuzzifications = [];

    public IReadOnlyList<MfHandle> Handles { get; private set; } = [];

    public OperationsViewModel()
    {
        SetA = new FuzzySetEditorViewModel("A", MfPlotting.BrushFor(0),
            new TriangularMf(1, 4, 7), new TrapezoidalMf(1, 3, 4, 7), new GaussianMf(4, 1.4), initialShape: 0, Rebuild);
        SetB = new FuzzySetEditorViewModel("B", MfPlotting.BrushFor(1),
            new TriangularMf(3, 6.5, 9), new TrapezoidalMf(3, 6, 8, 10), new GaussianMf(6.5, 1.6), initialShape: 1, Rebuild);
        Rebuild();
    }

    public bool UsesTNorm       => SelectedOperation == Intersection;
    public bool UsesSNorm       => SelectedOperation == Union;
    public bool UsesImplication => SelectedOperation == ImplicationOperation;
    public bool UsesSetB        => SelectedOperation != Complement;
    public bool CanCompare      => SelectedOperation != Complement;
    public string FiringText    => Firing.ToString("0.00", Invariant);

    public string Formula => SelectedOperation switch
    {
        Intersection => "μ(x) = T(μA(x), μB(x)),  T = " + SelectedTNorm switch
        {
            TNorm.Minimum     => "min(a, b)",
            TNorm.Product     => "a·b",
            TNorm.Lukasiewicz => "max(0, a + b − 1)",
            TNorm.Einstein    => "ab / (2 − (a + b − ab))",
            _                 => "b if a = 1, a if b = 1, 0 otherwise",
        },
        Union => "μ(x) = S(μA(x), μB(x)),  S = " + SelectedSNorm switch
        {
            SNorm.Maximum          => "max(a, b)",
            SNorm.ProbabilisticSum => "a + b − ab",
            SNorm.BoundedSum       => "min(1, a + b)",
            SNorm.Einstein         => "(a + b) / (1 + ab)",
            _                      => "b if a = 0, a if b = 0, 1 otherwise",
        },
        Complement => "μ(x) = 1 − μA(x)",
        _ => SelectedImplication == Implication.Minimum
            ? "μ(y) = min(w, μB(y))  — clipping (Mamdani)"
            : "μ(y) = w·μB(y)  — scaling (Larsen)",
    };

    partial void OnSelectedOperationChanged(int value)
    {
        OnPropertyChanged(nameof(UsesTNorm));
        OnPropertyChanged(nameof(UsesSNorm));
        OnPropertyChanged(nameof(UsesImplication));
        OnPropertyChanged(nameof(UsesSetB));
        OnPropertyChanged(nameof(CanCompare));
        Rebuild();
    }

    partial void OnSelectedTNormChanged(TNorm value)             => Rebuild();
    partial void OnSelectedSNormChanged(SNorm value)             => Rebuild();
    partial void OnSelectedImplicationChanged(Implication value) => Rebuild();
    partial void OnCompareAllChanged(bool value)                 => Rebuild();
    partial void OnFiringChanged(double value)
    {
        OnPropertyChanged(nameof(FiringText));
        Rebuild();
    }

    public void DragHandle(MfHandle handle, double position)
    {
        var set = handle.Term == 0 ? SetA : SetB;
        set.SetParameter(handle.Parameter, MfPlotting.DraggedValue(set.Mf, handle, position));
    }

    public void EndDrag() { }

    /// <summary>Result membership on the grid for one operator choice.</summary>
    private double[] Result(double[] grid, Func<double, double, double> combine) =>
        grid.Select(position => combine(SetA.Mf.Evaluate(position), SetB.Mf.Evaluate(position))).ToArray();

    private Func<double, double, double> SelectedCombination() => SelectedOperation switch
    {
        Intersection => (left, right) => SelectedTNorm.Apply(left, right),
        Union        => (left, right) => SelectedSNorm.Apply(left, right),
        Complement   => (left, _) => 1 - left,
        _            => (_, right) => SelectedImplication.Apply(Firing, right),
    };

    /// <summary>Every operator of the selected family, for the overlay comparison.</summary>
    private IEnumerable<(string Name, Func<double, double, double> Combine)> AllCombinations() => SelectedOperation switch
    {
        Intersection => TNorms.Select(norm => (norm.ToString(), (Func<double, double, double>)((left, right) => norm.Apply(left, right)))),
        Union        => SNorms.Select(conorm => (conorm.ToString(), (Func<double, double, double>)((left, right) => conorm.Apply(left, right)))),
        _            => Implications.Select(implication => (implication.ToString(),
                            (Func<double, double, double>)((_, right) => implication.Apply(Firing, right)))),
    };

    private void Rebuild()
    {
        if (SetA is null || SetB is null) return;   // called back while the editors are being built
        OnPropertyChanged(nameof(Formula));

        double lower = System.Math.Min(0, System.Math.Min(SetA.Mf.Support.Min, SetB.Mf.Support.Min));
        double upper = System.Math.Max(10, System.Math.Max(SetA.Mf.Support.Max, SetB.Mf.Support.Max));
        var grid = MfPlotting.Grid(lower, upper);
        var result = Result(grid, SelectedCombination());

        bool overlay = CompareAll && CanCompare;
        var model = new PlotModel { Background = DesignPalette.PlotBackground, IsLegendVisible = overlay };
        if (overlay)
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopRight, LegendPlacement = LegendPlacement.Inside,
                LegendBackground = OxyColor.FromAColor(0xD0, OxyColors.White), LegendFontSize = 10,
                LegendTextColor = DesignPalette.AxisText,
            });

        var area = new AreaSeries
        {
            Color = DesignPalette.Blue600, StrokeThickness = 2, Fill = OxyColor.FromAColor(0x50, DesignPalette.Blue100),
            Title = overlay ? null : "result",
        };
        for (int point = 0; point < grid.Length; point++)
        {
            area.Points.Add(new DataPoint(grid[point], result[point]));
            area.Points2.Add(new DataPoint(grid[point], 0));
        }
        model.Series.Add(area);

        if (overlay)
        {
            int colorIndex = 2;
            foreach (var (name, combine) in AllCombinations())
            {
                var values = Result(grid, combine);
                var line = new LineSeries { Title = name, Color = MfPlotting.ColorFor(colorIndex++), StrokeThickness = 1.4 };
                for (int point = 0; point < grid.Length; point++) line.Points.Add(new DataPoint(grid[point], values[point]));
                model.Series.Add(line);
            }
        }

        var handles = new List<MfHandle>();
        AddSourceCurve(model, SetA, 0, lower, upper, handles);
        if (UsesSetB) AddSourceCurve(model, SetB, 1, lower, upper, handles);
        if (UsesImplication)
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal, Y = Firing, Color = DesignPalette.AxisText, LineStyle = LineStyle.Dot,
                Text = $"w = {FiringText}", TextColor = DesignPalette.AxisText,
            });

        var handleSeries = new ScatterSeries
        {
            MarkerType = MarkerType.Square, MarkerSize = 4,
            MarkerFill = OxyColors.White, MarkerStroke = DesignPalette.AxisText, MarkerStrokeThickness = 1.2,
        };
        foreach (var handle in handles) handleSeries.Points.Add(new ScatterPoint(handle.X, handle.Y));
        model.Series.Add(handleSeries);

        var rows = new List<DefuzzificationRow>();
        foreach (var method in Enum.GetValues<Defuzzification>())
        {
            double crisp = method.Defuzzify(grid, result);
            var color = MethodColors[(int)method];
            rows.Add(new DefuzzificationRow(method.ToString(), double.IsNaN(crisp) ? "empty set" : crisp.ToString("0.###", Invariant),
                ToBrush(color)));
            if (double.IsNaN(crisp)) continue;
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical, X = crisp, Color = color, StrokeThickness = 1.3,
                MaximumY = 1.0, Text = Abbreviation(method), TextColor = color, FontSize = 10,
            });
        }

        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Bottom, "x", lower, upper));
        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Left, "", -0.03, 1.1));

        Handles = handles;
        Defuzzifications = rows;
        Plot = model;
    }

    private static void AddSourceCurve(PlotModel model, FuzzySetEditorViewModel set, int term, double lower, double upper, List<MfHandle> handles)
    {
        var line = new LineSeries { Color = MfPlotting.ColorFor(term), StrokeThickness = 1.5, LineStyle = LineStyle.Dash, Title = set.Name };
        foreach (double position in MfPlotting.SamplePoints(set.Mf, lower, upper))
            line.Points.Add(new DataPoint(position, set.Mf.Evaluate(position)));
        model.Series.Add(line);
        handles.AddRange(MfPlotting.HandlesOf(term, set.Mf));
        model.Annotations.Add(new TextAnnotation
        {
            Text = set.Name, TextPosition = new DataPoint(set.Mf.CoreCenter, 1.0), Stroke = OxyColors.Transparent,
            TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom, TextColor = MfPlotting.ColorFor(term), FontSize = 11,
        });
    }

    private static string Abbreviation(Defuzzification method) => method switch
    {
        Defuzzification.Centroid          => "COG",
        Defuzzification.Bisector          => "BOA",
        Defuzzification.MeanOfMaximum     => "MOM",
        Defuzzification.SmallestOfMaximum => "SOM",
        _                                 => "LOM",
    };

    private static Brush ToBrush(OxyColor color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
