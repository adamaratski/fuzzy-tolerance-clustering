using MathFuzzy.Clustering.Results;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace MathFuzzy.Desktop.Controls;

/// <summary>
/// Draws a merge dendrogram from <see cref="HierarchicalResult.Levels"/>:
/// leaves (finest partition) at the bottom, merges at decreasing α going up.
/// Colors follow the design mockup: blue tree, dashed coral α* line.
/// </summary>
public sealed class DendrogramCanvas : FrameworkElement
{
    private static readonly Brush TreeBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0x37, 0x8A, 0xDD)));
    private static readonly Brush RootBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0x18, 0x5F, 0xA5)));
    private static readonly Brush CoralBrush     = Freeze(new SolidColorBrush(Color.FromRgb(0xD8, 0x5A, 0x30)));
    private static readonly Brush LabelBrush     = Freeze(new SolidColorBrush(Color.FromRgb(0x5F, 0x5E, 0x5A)));
    private static readonly Brush AxisBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0xC9, 0xC7, 0xBE)));
    private static readonly Brush HighlightBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x28, 0x37, 0x8A, 0xDD)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result), typeof(HierarchicalResult), typeof(DendrogramCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AlphaStarProperty = DependencyProperty.Register(
        nameof(AlphaStar), typeof(double), typeof(DendrogramCanvas),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedAlphaProperty = DependencyProperty.Register(
        nameof(SelectedAlpha), typeof(double), typeof(DendrogramCanvas),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public HierarchicalResult? Result
    {
        get => (HierarchicalResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public double AlphaStar
    {
        get => (double)GetValue(AlphaStarProperty);
        set => SetValue(AlphaStarProperty, value);
    }

    public double SelectedAlpha
    {
        get => (double)GetValue(SelectedAlphaProperty);
        set => SetValue(SelectedAlphaProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Result is not { Levels.Count: > 0 } result) return;

        double w = ActualWidth, h = ActualHeight;
        if (w < 60 || h < 60) return;

        var levels = result.Levels.OrderByDescending(l => l.ClusterCount).ToList();
        var finest = levels[0];

        // Leaf labels (few leaves only) go above the leaves, which sit at the top.
        bool labelLeaves = finest.MemberSets.Length <= 12;
        const double marginL = 42, marginR = 12, marginB = 14;
        double marginT = labelLeaves ? 28 : 14;
        double plotW = w - marginL - marginR;
        double plotH = h - marginT - marginB;

        double aMin = levels.Min(l => l.Alpha);
        double aMax = levels.Max(l => l.Alpha);
        double aPad = System.Math.Max((aMax - aMin) * 0.12, 0.01);
        // Finest level (αmax) at the top, coarsest (αmin) at the bottom.
        double YOf(double alpha) =>
            marginT + (aMax + aPad - alpha) / (aMax + aPad - (aMin - aPad)) * plotH;

        var treePen = new Pen(TreeBrush, 1.6);
        var rootPen = new Pen(RootBrush, 1.6);
        var axisPen = new Pen(AxisBrush, 1.0);
        var starPen = new Pen(CoralBrush, 1.2) { DashStyle = new DashStyle([4, 3], 0) };
        treePen.Freeze(); rootPen.Freeze(); axisPen.Freeze(); starPen.Freeze();

        // α axis
        dc.DrawLine(axisPen, new Point(marginL, marginT), new Point(marginL, marginT + plotH));
        // A tick for every level, but a label only where it does not collide with the
        // previous one; the precision follows the α range so neighbouring labels differ.
        string fmt = aMax - aMin < 0.05 ? "0.0000" : aMax - aMin < 0.3 ? "0.000" : "0.00";
        const double minLabelGap = 12;
        double lastLabelY = double.NegativeInfinity;
        foreach (var lv in levels.OrderByDescending(l => l.Alpha))   // high α is drawn at the top
        {
            double y = YOf(lv.Alpha);
            dc.DrawLine(axisPen, new Point(marginL - 3, y), new Point(marginL, y));
            if (y - lastLabelY < minLabelGap) continue;
            DrawText(dc, lv.Alpha.ToString(fmt, CultureInfo.InvariantCulture),
                     new Point(2, y - 6), 9, LabelBrush);
            lastLabelY = y;
        }

        // Selected level highlight band
        if (!double.IsNaN(SelectedAlpha))
        {
            double y = YOf(SelectedAlpha);
            dc.DrawRectangle(HighlightBrush, null, new Rect(marginL, y - 8, plotW, 16));
        }

        // Leaves: finest partition clusters, ordered by smallest member index
        var leafSets = finest.MemberSets
            .Select((s, i) => (Set: s.ToHashSet(), Min: s.Min(), Index: i))
            .OrderBy(t => t.Min)
            .ToList();

        int leafCount = leafSets.Count;
        var nodes = new List<(HashSet<int> Set, double X, double Y)>();
        double leafY = YOf(finest.Alpha);

        for (int i = 0; i < leafCount; i++)
        {
            double x = marginL + plotW * (i + 0.5) / leafCount;
            nodes.Add((leafSets[i].Set, x, leafY));

            // leaves (finest level, highest α) sit at the top: a short stem from the top edge
            dc.DrawLine(treePen, new Point(x, marginT), new Point(x, leafY));
            if (labelLeaves)
                DrawText(dc,
                    $"C{i + 1} ({leafSets[i].Set.Count})",
                    new Point(x - 16, 4), 9, LabelBrush);
        }

        // Merge upward through coarser levels
        foreach (var level in levels.Skip(1))
        {
            bool isRoot = level == levels[^1];
            var pen = isRoot ? rootPen : treePen;
            double y = YOf(level.Alpha);
            var merged = new List<(HashSet<int> Set, double X, double Y)>();

            foreach (var cluster in level.MemberSets)
            {
                var cset = cluster.ToHashSet();
                var children = nodes.Where(nd => nd.Set.IsSubsetOf(cset)).ToList();
                if (children.Count == 0) continue;

                if (children.Count == 1)
                {
                    merged.Add((cset, children[0].X, children[0].Y));
                    continue;
                }

                double x1 = children.Min(ch => ch.X);
                double x2 = children.Max(ch => ch.X);
                foreach (var ch in children)
                    dc.DrawLine(pen, new Point(ch.X, ch.Y), new Point(ch.X, y));
                dc.DrawLine(pen, new Point(x1, y), new Point(x2, y));

                merged.Add((cset, (x1 + x2) / 2, y));
            }

            nodes = merged;
        }

        // Clusters of the coarsest level continue down to the bottom edge.
        foreach (var nd in nodes)
            dc.DrawLine(rootPen, new Point(nd.X, nd.Y), new Point(nd.X, marginT + plotH));

        // α* leap line
        if (!double.IsNaN(AlphaStar))
        {
            double y = YOf(AlphaStar);
            dc.DrawLine(starPen, new Point(marginL, y), new Point(marginL + plotW, y));
            DrawText(dc,
                $"α* = {AlphaStar.ToString("0.00", CultureInfo.InvariantCulture)} (leap)",
                new Point(marginL + plotW - 92, y - 14), 9.5, CoralBrush);
        }
    }

    private void DrawText(DrawingContext dc, string text, Point at, double size, Brush brush)
    {
        var ft = new FormattedText(
            text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, at);
    }
}
