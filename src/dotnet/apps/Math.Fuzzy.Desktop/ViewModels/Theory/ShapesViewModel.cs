using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.ViewModels.Results;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Globalization;

namespace MathFuzzy.Desktop.ViewModels.Theory;

/// <summary>Name / value / explanation of one characteristic of a fuzzy set.</summary>
public sealed record SetPropertyRow(string Name, string Value, string Note);

/// <summary>
/// "Shapes" tab: one membership function from the inference library, its α-cut and the
/// characteristics used throughout the book (height, support, core, α-cut, cardinality).
/// </summary>
public sealed partial class ShapesViewModel : ObservableObject, IMfHandleDragTarget
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public FuzzySetEditorViewModel Set { get; }

    [ObservableProperty] private double _alpha = 0.5;
    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private IReadOnlyList<SetPropertyRow> _properties = [];

    public IReadOnlyList<MfHandle> Handles { get; private set; } = [];

    public ShapesViewModel()
    {
        Set = new FuzzySetEditorViewModel("A", MfPlotting.BrushFor(0),
            new TriangularMf(2, 5, 8), new TrapezoidalMf(2, 4, 6, 9), new GaussianMf(5, 1.5), initialShape: 1, Rebuild);
        Rebuild();
    }

    public string AlphaText => Alpha.ToString("0.00", Invariant);

    partial void OnAlphaChanged(double value)
    {
        OnPropertyChanged(nameof(AlphaText));
        Rebuild();
    }

    public void DragHandle(MfHandle handle, double position) =>
        Set.SetParameter(handle.Parameter, MfPlotting.DraggedValue(Set.Mf, handle, position));

    public void EndDrag() { }

    private void Rebuild()
    {
        var mf = Set.Mf;
        double alpha = System.Math.Clamp(Alpha, 0.01, 1);
        var (supportMin, supportMax) = mf.Support;
        double padding = System.Math.Max((supportMax - supportMin) * 0.15, 0.5);
        double lower = supportMin - padding, upper = supportMax + padding;
        var (cutMin, cutMax) = mf.AlphaCut(alpha);

        var model = new PlotModel { Background = DesignPalette.PlotBackground, IsLegendVisible = false };
        // α-cut: the band under the level α across the interval where μ ≥ α
        model.Annotations.Add(new RectangleAnnotation
        {
            MinimumX = cutMin, MaximumX = cutMax, MinimumY = 0, MaximumY = alpha,
            Fill = OxyColor.FromAColor(0x40, DesignPalette.Blue100), Layer = AnnotationLayer.BelowSeries,
        });
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal, Y = alpha, Color = DesignPalette.Coral, LineStyle = LineStyle.Dash,
            Text = $"α = {alpha.ToString("0.00", Invariant)}", TextColor = DesignPalette.Coral,
        });
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical, X = mf.CoreCenter, Color = DesignPalette.AxisLine, LineStyle = LineStyle.Dot,
        });

        var curve = new AreaSeries
        {
            Color = MfPlotting.ColorFor(0), StrokeThickness = 2, Fill = OxyColor.FromAColor(0x30, MfPlotting.ColorFor(0)),
        };
        foreach (double position in MfPlotting.SamplePoints(mf, lower, upper))
        {
            curve.Points.Add(new DataPoint(position, mf.Evaluate(position)));
            curve.Points2.Add(new DataPoint(position, 0));
        }
        model.Series.Add(curve);

        var handles = MfPlotting.HandlesOf(0, mf).ToList();
        var handleSeries = new ScatterSeries
        {
            MarkerType = MarkerType.Square, MarkerSize = 4,
            MarkerFill = OxyColors.White, MarkerStroke = DesignPalette.AxisText, MarkerStrokeThickness = 1.2,
        };
        foreach (var handle in handles) handleSeries.Points.Add(new ScatterPoint(handle.X, handle.Y));
        model.Series.Add(handleSeries);

        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Bottom, "x", lower, upper));
        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Left, "", -0.03, 1.1));

        Handles = handles;
        Plot = model;
        Properties = Describe(mf, alpha, lower, upper);
    }

    private static IReadOnlyList<SetPropertyRow> Describe(MembershipFunction mf, double alpha, double lower, double upper)
    {
        string Number(double value) => value.ToString("0.####", Invariant);
        var (cutMin, cutMax) = mf.AlphaCut(alpha);
        var (crossLeft, crossRight) = mf.AlphaCut(0.5);
        var grid = MfPlotting.Grid(lower, upper, 4001);
        double centroid = Defuzzification.Centroid.Defuzzify(grid, grid.Select(mf.Evaluate).ToArray());

        string support = mf switch
        {
            GaussianMf => "ℝ",
            _          => $"({Number(mf.Support.Min)}, {Number(mf.Support.Max)})",
        };
        string core = mf switch
        {
            TrapezoidalMf trapezoid => $"[{Number(trapezoid.LeftShoulder)}, {Number(trapezoid.RightShoulder)}]",
            _                       => $"{{{Number(mf.CoreCenter)}}}",
        };

        return
        [
            new("height h(A)", "1", "sup μ; the set is normal"),
            new("support", support, "{x : μ(x) > 0}" + (mf is GaussianMf ? "; μ > 0 everywhere" : "")),
            new("core", core, "{x : μ(x) = 1}"),
            new($"α-cut, α = {alpha.ToString("0.00", Invariant)}", $"[{Number(cutMin)}, {Number(cutMax)}]", "{x : μ(x) ≥ α}; shaded on the plot"),
            new("crossover points", $"{Number(crossLeft)}, {Number(crossRight)}", "μ(x) = 0.5"),
            new("cardinality", Number(mf.Area), "∫μ(x)dx, the continuous Σ-count"),
            new("centroid", Number(centroid), "∫xμ(x)dx / ∫μ(x)dx"),
        ];
    }
}
