using MathFuzzy.Desktop.ViewModels.Workspace;
using MathFuzzy.Inference.MembershipFunctions;
using OxyPlot;
using OxyPlot.Axes;
using System.Windows.Media;

namespace MathFuzzy.Desktop.ViewModels.Results;

/// <summary>A draggable break point of a membership function on a plot (data coordinates).</summary>
/// <param name="Term">Index of the set / term the handle belongs to.</param>
/// <param name="Parameter">Index into <see cref="MembershipFunction.Parameters"/>.</param>
public sealed record MfHandle(int Term, int Parameter, double X, double Y);

/// <summary>View model whose membership functions can be reshaped by dragging handles on a plot.</summary>
public interface IMfHandleDragTarget
{
    IReadOnlyList<MfHandle> Handles { get; }
    /// <summary>Moves a handle horizontally to <paramref name="position"/> (data units).</summary>
    void DragHandle(MfHandle handle, double position);
    /// <summary>Called once when the drag ends (e.g. to refresh numeric editors).</summary>
    void EndDrag();
}

/// <summary>Shared drawing helpers for membership-function plots (Inference and Fuzzy sets pages).</summary>
public static class MfPlotting
{
    public static OxyColor ColorFor(int index) =>
        VisualizePanelViewModel.ClusterColors[index % VisualizePanelViewModel.ClusterColors.Length];

    public static Brush BrushFor(int index)
    {
        var color = ColorFor(index);
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    public static LinearAxis FixedAxis(AxisPosition position, string title, double minimum, double maximum) => new()
    {
        Position = position, Title = title, Minimum = minimum, Maximum = maximum,
        TextColor = DesignPalette.AxisText, AxislineColor = DesignPalette.AxisLine, TicklineColor = DesignPalette.AxisLine,
        IsZoomEnabled = false, IsPanEnabled = false,
    };

    /// <summary>Break points of a membership function as draggable handles.</summary>
    public static IEnumerable<MfHandle> HandlesOf(int term, MembershipFunction mf)
    {
        switch (mf)
        {
            case TriangularMf triangle:
                yield return new MfHandle(term, 0, triangle.LeftFoot, 0);
                yield return new MfHandle(term, 1, triangle.Apex, 1);
                yield return new MfHandle(term, 2, triangle.RightFoot, 0);
                break;
            case TrapezoidalMf trapezoid:
                yield return new MfHandle(term, 0, trapezoid.LeftFoot, 0);
                yield return new MfHandle(term, 1, trapezoid.LeftShoulder, 1);
                yield return new MfHandle(term, 2, trapezoid.RightShoulder, 1);
                yield return new MfHandle(term, 3, trapezoid.RightFoot, 0);
                break;
            case GaussianMf gaussian:
                yield return new MfHandle(term, 0, gaussian.Center, 1);
                yield return new MfHandle(term, 1, gaussian.Center + gaussian.Sigma, System.Math.Exp(-0.5));
                break;
        }
    }

    /// <summary>
    /// Value a dragged handle gives to its parameter: σ = |position − c| for the Gaussian spread
    /// handle; otherwise the position, kept between the neighbouring break points so the handle
    /// keeps its identity.
    /// </summary>
    public static double DraggedValue(MembershipFunction mf, MfHandle handle, double position)
    {
        if (mf is GaussianMf gaussian)
            return handle.Parameter == 1 ? System.Math.Abs(position - gaussian.Center) : position;
        var parameters = mf.Parameters;
        double lowerBound = handle.Parameter > 0 ? parameters[handle.Parameter - 1] : double.NegativeInfinity;
        double upperBound = handle.Parameter < parameters.Count - 1 ? parameters[handle.Parameter + 1] : double.PositiveInfinity;
        return System.Math.Clamp(position, lowerBound, upperBound);
    }

    /// <summary>Break points (exact for piecewise-linear shapes) plus a uniform grid for curved ones.</summary>
    public static IEnumerable<double> SamplePoints(MembershipFunction mf, double lower, double upper)
    {
        if (mf is GaussianMf)
            return Enumerable.Range(0, 301).Select(point => lower + (upper - lower) * point / 300);
        return mf.Parameters.Append(lower).Append(upper).Where(position => position >= lower && position <= upper).Order()
            // a vertical side needs both of its ends: sample just left and right of every break point
            .SelectMany(position =>
            {
                double nudge = 1e-9 * (1 + System.Math.Abs(position));
                return new[] { position - nudge, position, position + nudge };
            })
            .Where(position => position >= lower && position <= upper);
    }

    /// <summary>Uniform grid of <paramref name="pointCount"/> points on [lower, upper].</summary>
    public static double[] Grid(double lower, double upper, int pointCount = 1001) =>
        Enumerable.Range(0, pointCount).Select(point => lower + (upper - lower) * point / (pointCount - 1)).ToArray();
}
