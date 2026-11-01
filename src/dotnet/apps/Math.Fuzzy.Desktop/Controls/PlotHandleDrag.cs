using MathFuzzy.Desktop.ViewModels.Results;
using OxyPlot.Wpf;
using System.Windows;
using System.Windows.Input;

namespace MathFuzzy.Desktop.Controls;

/// <summary>
/// Attached behaviour that turns left-button drags on a <see cref="PlotView"/> into
/// <see cref="IMfHandleDragTarget.DragHandle"/> calls. Hit-testing needs the plot's axes,
/// so it lives in the view layer; the view model only sees data coordinates.
/// Usage: <c>ctrl:PlotHandleDrag.Target="{Binding}"</c>.
/// </summary>
public static class PlotHandleDrag
{
    /// <summary>A handle is grabbed when the pointer is within this many pixels of it.</summary>
    private const double GrabRadius = 9;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(IMfHandleDragTarget), typeof(PlotHandleDrag), new PropertyMetadata(null, OnTargetChanged));

    private static readonly DependencyProperty DraggedProperty = DependencyProperty.RegisterAttached(
        "Dragged", typeof(MfHandle), typeof(PlotHandleDrag), new PropertyMetadata(null));

    public static IMfHandleDragTarget? GetTarget(DependencyObject element) => (IMfHandleDragTarget?)element.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject element, IMfHandleDragTarget? value) => element.SetValue(TargetProperty, value);

    private static void OnTargetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not PlotView plot) return;
        plot.PreviewMouseLeftButtonDown -= OnMouseDown;
        plot.PreviewMouseMove           -= OnMouseMove;
        plot.PreviewMouseLeftButtonUp   -= OnMouseUp;
        if (e.NewValue is null) return;
        plot.PreviewMouseLeftButtonDown += OnMouseDown;
        plot.PreviewMouseMove           += OnMouseMove;
        plot.PreviewMouseLeftButtonUp   += OnMouseUp;
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var plot = (PlotView)sender;
        if (GetTarget(plot) is not { } target || plot.ActualModel is not { } model) return;
        if (model.DefaultXAxis is not { } xAxis || model.DefaultYAxis is not { } yAxis) return;

        var pointer = e.GetPosition(plot);
        MfHandle? nearest = null;
        double nearestDistance = GrabRadius;
        foreach (var handle in target.Handles)
        {
            double horizontal = xAxis.Transform(handle.X) - pointer.X;
            double vertical   = yAxis.Transform(handle.Y) - pointer.Y;
            double distance = System.Math.Sqrt(horizontal * horizontal + vertical * vertical);
            if (distance > nearestDistance) continue;
            nearestDistance = distance;
            nearest = handle;
        }
        if (nearest is null) return;

        plot.SetValue(DraggedProperty, nearest);
        plot.CaptureMouse();
        plot.Cursor = Cursors.SizeWE;
        e.Handled = true;   // keep OxyPlot's tracker from taking the gesture
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        var plot = (PlotView)sender;
        if (plot.GetValue(DraggedProperty) is not MfHandle handle || GetTarget(plot) is not { } target
            || plot.ActualModel?.DefaultXAxis is not { } xAxis) return;
        target.DragHandle(handle, xAxis.InverseTransform(e.GetPosition(plot).X));
        e.Handled = true;
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var plot = (PlotView)sender;
        if (plot.GetValue(DraggedProperty) is null) return;
        plot.ClearValue(DraggedProperty);
        plot.ReleaseMouseCapture();
        plot.Cursor = null;
        GetTarget(plot)?.EndDrag();
        e.Handled = true;
    }
}
