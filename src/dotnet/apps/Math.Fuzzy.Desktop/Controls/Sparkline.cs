using System.Windows;
using System.Windows.Media;

namespace MathFuzzy.Desktop.Controls;

/// <summary>
/// Task-Manager-style utilization thumbnail: a light grid, a translucent area and a line
/// for the last N samples (0–100), newest sample on the right.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Number of sample slots across the width (older samples scroll off the left).</summary>
    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    private static readonly Pen GridPen = CreateGridPen();

    private static Pen CreateGridPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)), 1);
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        for (int k = 1; k < 4; k++)
        {
            double y = System.Math.Round(h * k / 4) + 0.5;
            dc.DrawLine(GridPen, new Point(0, y), new Point(w, y));
        }

        if (Values is not { Count: > 0 } values) return;

        int cap = System.Math.Max(2, Capacity);
        int count = System.Math.Min(values.Count, cap);
        double step = w / (cap - 1);
        double x0 = w - (count - 1) * step;
        double Y(double v) => h - System.Math.Clamp(v, 0, 100) / 100.0 * (h - 1);

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lc = line.Open())
        using (var ac = area.Open())
        {
            var first = new Point(x0, Y(values[values.Count - count]));
            lc.BeginFigure(first, false, false);
            ac.BeginFigure(new Point(x0, h), true, true);
            ac.LineTo(first, false, false);
            for (int i = 1; i < count; i++)
            {
                var p = new Point(x0 + i * step, Y(values[values.Count - count + i]));
                lc.LineTo(p, true, true);
                ac.LineTo(p, false, false);
            }
            ac.LineTo(new Point(w, h), false, false);
        }
        line.Freeze();
        area.Freeze();

        Brush fill = Stroke.CloneCurrentValue();
        fill.Opacity = 0.18;
        fill.Freeze();
        var pen = new Pen(Stroke, 1.2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();

        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, pen, line);
    }
}
