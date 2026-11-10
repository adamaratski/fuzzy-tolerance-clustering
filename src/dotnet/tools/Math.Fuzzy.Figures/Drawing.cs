using System.Text.Json;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Systems;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace MathFuzzy.Figures;

/// <summary>Plot models of the figures. All colours come from one colour-blind-safe palette.</summary>
internal static class Drawing
{
    // Okabe–Ito palette
    private static readonly OxyColor[] Palette =
    [
        OxyColor.FromRgb(0, 114, 178), OxyColor.FromRgb(213, 94, 0), OxyColor.FromRgb(0, 158, 115),
        OxyColor.FromRgb(204, 121, 167), OxyColor.FromRgb(230, 159, 0), OxyColor.FromRgb(86, 180, 233),
        OxyColor.FromRgb(120, 120, 120), OxyColor.FromRgb(0, 0, 0),
    ];

    private static readonly MarkerType[] Markers =
        [MarkerType.Circle, MarkerType.Square, MarkerType.Triangle, MarkerType.Diamond, MarkerType.Cross, MarkerType.Plus, MarkerType.Star];

    private static readonly OxyColor Grid = OxyColor.FromRgb(225, 225, 225);
    private static readonly OxyColor Ink = OxyColor.FromRgb(40, 40, 40);

    // The figures are drawn at the size at which they are printed (1 pixel = 0.75 pt at 96 dpi), so
    // a font of N pixels prints as 0.75 N pt. No text in any figure is smaller than 13 pixels (9.75 pt), apart from the
    // exponents and subscripts of the typeset notation.
    private const string FontName = "Times New Roman";
    private const double TextSize = 13.5;
    private const double SmallestTextSize = 13;

    /// <summary>Dashes that stay visible at print size (the plain dash style is almost a solid line).</summary>
    private const LineStyle ThresholdStyle = LineStyle.LongDash;

    private static PlotModel NewModel() => new()
    {
        Background = OxyColors.White,
        DefaultFont = FontName,
        DefaultFontSize = TextSize,
        PlotAreaBorderColor = OxyColor.FromRgb(90, 90, 90),
        PlotAreaBorderThickness = new OxyThickness(1),
        Padding = new OxyThickness(4, 6, 12, 4),
    };

    private static LinearAxis Axis(AxisPosition position, string title, bool grid = true,
                                   double minimum = double.NaN, double maximum = double.NaN) => new()
    {
        Position = position,
        Title = title,
        AxisTitleDistance = 6,
        MajorGridlineStyle = grid ? LineStyle.Solid : LineStyle.None,
        MajorGridlineColor = Grid,
        TicklineColor = OxyColor.FromRgb(90, 90, 90),
        MinimumPadding = double.IsNaN(minimum) ? 0.09 : 0,
        MaximumPadding = double.IsNaN(maximum) ? 0.09 : 0,
        Minimum = minimum,
        Maximum = maximum,
    };

    private static LogarithmicAxis LogAxis(AxisPosition position, string title,
                                           double minimum = double.NaN, double maximum = double.NaN, bool powersOfTen = true) => new()
    {
        Position = position,
        Title = title,
        AxisTitleDistance = 6,
        MajorGridlineStyle = LineStyle.Solid,
        MajorGridlineColor = Grid,
        TicklineColor = OxyColor.FromRgb(90, 90, 90),
        Minimum = minimum,
        Maximum = maximum,
        LabelFormatter = powersOfTen ? FormatLogarithmicTick : value => value.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Plain numbers below 1000 and powers of ten (10³, 10⁴, …) from 1000 on; no exponent notation.</summary>
    private static string FormatLogarithmicTick(double value)
    {
        double exponent = Math.Log10(value);
        bool isPowerOfTen = Math.Abs(exponent - Math.Round(exponent)) < 1e-9;
        if (isPowerOfTen && Math.Round(exponent) >= 3) return "10" + Superscript((int)Math.Round(exponent));
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddLegend(PlotModel model, LegendPosition position = LegendPosition.RightTop) =>
        model.Legends.Add(new Legend
        {
            LegendPosition = position,
            LegendPlacement = LegendPlacement.Inside,
            LegendBackground = OxyColor.FromAColor(235, OxyColors.White),
            LegendBorder = Grid,
            LegendFontSize = SmallestTextSize,
            LegendSymbolLength = 30,
            LegendSymbolMargin = 6,
            LegendItemSpacing = 10,
            LegendLineSpacing = 2,
            LegendPadding = 6,
            LegendMargin = 6,
        });

    private static OxyColor Colour(int index) => Palette[index % Palette.Length];

    /// <summary>The α-cut graphs of a small tolerance matrix at several levels, side by side.</summary>
    public static PlotModel CutGraphs(FuzzyToleranceMatrix tolerance, double[] levels, int[][] typicalPoints)
    {
        var model = NewModel();
        model.PlotAreaBorderThickness = new OxyThickness(0);
        model.Padding = new OxyThickness(0);
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = false, Minimum = -1.55, Maximum = 3.4 * levels.Length - 1.85 });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, IsAxisVisible = false, Minimum = -1.95, Maximum = 1.55 });

        int objectCount = tolerance.Size;
        for (int panel = 0; panel < levels.Length; panel++)
        {
            double offset = 3.4 * panel;
            var positions = Enumerable.Range(0, objectCount)
                .Select(index => (X: offset + Math.Sin(2 * Math.PI * index / objectCount), Y: Math.Cos(2 * Math.PI * index / objectCount)))
                .ToArray();

            for (int first = 0; first < objectCount; first++)
            for (int second = first + 1; second < objectCount; second++)
            {
                if (tolerance[first, second] < levels[panel]) continue;
                var edge = new LineSeries { Color = OxyColor.FromRgb(110, 110, 110), StrokeThickness = 1.8 };
                edge.Points.Add(new DataPoint(positions[first].X, positions[first].Y));
                edge.Points.Add(new DataPoint(positions[second].X, positions[second].Y));
                model.Series.Add(edge);
                model.Annotations.Add(new TextAnnotation
                {
                    Text = tolerance[first, second].ToString("0.0"),
                    TextPosition = new DataPoint((positions[first].X + positions[second].X) / 2, (positions[first].Y + positions[second].Y) / 2),
                    Stroke = OxyColors.Transparent, FontSize = SmallestTextSize, TextColor = OxyColors.Black,
                    Background = OxyColors.White, Padding = new OxyThickness(1),
                    TextHorizontalAlignment = HorizontalAlignment.Center, TextVerticalAlignment = VerticalAlignment.Middle,
                });
            }

            var ordinary = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 7, MarkerFill = OxyColors.White, MarkerStroke = Colour(0), MarkerStrokeThickness = 2 };
            var typical = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 7, MarkerFill = Colour(0), MarkerStroke = Colour(0), MarkerStrokeThickness = 2 };
            for (int index = 0; index < objectCount; index++)
            {
                bool isTypical = typicalPoints[panel].Contains(index);
                (isTypical ? typical : ordinary).Points.Add(new ScatterPoint(positions[index].X, positions[index].Y));
                model.Annotations.Add(new TextAnnotation
                {
                    Text = $"x{Subscript(index + 1)}",
                    TextPosition = new DataPoint(offset + 1.34 * Math.Sin(2 * Math.PI * index / objectCount), 1.34 * Math.Cos(2 * Math.PI * index / objectCount)),
                    Stroke = OxyColors.Transparent, FontSize = 16,
                    TextHorizontalAlignment = HorizontalAlignment.Center, TextVerticalAlignment = VerticalAlignment.Middle,
                });
            }
            model.Series.Add(ordinary);
            model.Series.Add(typical);
            model.Annotations.Add(new TextAnnotation
            {
                Text = $"α = {levels[panel]:0.0}", TextPosition = new DataPoint(offset, -1.72), Stroke = OxyColors.Transparent, FontSize = 14,
                TextHorizontalAlignment = HorizontalAlignment.Center, TextVerticalAlignment = VerticalAlignment.Middle,
            });
        }
        return model;
    }

    /// <summary>Two attributes of the data with the clusters of an allotment; typical points are outlined.
    /// The clusters differ in marker shape as well as in colour, so that they can be told apart in greyscale.</summary>
    public static PlotModel Scatter(RawDataMatrix data, int columnX, int columnY, ClusterResult allotment,
                                    string titleX, string titleY, bool labelObjects, bool legendBelow = false)
    {
        var model = NewModel();
        model.Axes.Add(Axis(AxisPosition.Bottom, titleX));
        model.Axes.Add(Axis(AxisPosition.Left, titleY));

        var coverage = new int[data.Rows];
        foreach (var members in allotment.MemberSets)
            foreach (int member in members) coverage[member]++;

        // the marker size is the radius in pixels; the data sets of the shapes have about 300 points on a small canvas
        double markerSize = labelObjects ? 4 : legendBelow ? 2.6 : 3.4;
        for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
        {
            var members = allotment.MemberSets[cluster];
            var series = new ScatterSeries
            {
                Title = $"cluster {cluster + 1}: {members.Length} {(members.Length == 1 ? "object" : "objects")}",
                MarkerType = Markers[cluster % Markers.Length], MarkerSize = markerSize,
                MarkerFill = Colour(cluster), MarkerStroke = Colour(cluster), MarkerStrokeThickness = 1.4,
            };
            foreach (int member in members)
                if (coverage[member] == 1) series.Points.Add(new ScatterPoint(data[member, columnX], data[member, columnY]));
            model.Series.Add(series);
        }

        // objects in intersection areas
        var shared = new ScatterSeries
        {
            Title = "in several clusters", MarkerType = MarkerType.Circle, MarkerSize = markerSize,
            MarkerFill = OxyColors.White, MarkerStroke = OxyColors.Black, MarkerStrokeThickness = 1.3,
        };
        for (int index = 0; index < data.Rows; index++)
            if (coverage[index] > 1) shared.Points.Add(new ScatterPoint(data[index, columnX], data[index, columnY]));
        if (shared.Points.Count > 0) model.Series.Add(shared);

        var typical = new ScatterSeries
        {
            MarkerType = MarkerType.Circle, MarkerSize = markerSize + 4,
            MarkerFill = OxyColors.Transparent, MarkerStroke = OxyColors.Black, MarkerStrokeThickness = 1.8,
            RenderInLegend = false,
        };
        foreach (int center in allotment.Centers)
            typical.Points.Add(new ScatterPoint(data[center, columnX], data[center, columnY]));
        model.Series.Add(typical);

        if (labelObjects)
        {
            for (int index = 0; index < data.Rows; index++)
                model.Annotations.Add(new TextAnnotation
                {
                    Text = (index + 1).ToString(), TextPosition = new DataPoint(data[index, columnX], data[index, columnY]),
                    Offset = new ScreenVector(10, -10), Stroke = OxyColors.Transparent, FontSize = SmallestTextSize, TextColor = OxyColors.Black,
                });
        }
        if (allotment.ClusterCount <= 7)
        {
            // legend entry of the typical points: a ring of the size of the other legend symbols
            // (the rings in the plot are larger, so that they surround the cluster markers)
            model.Series.Add(new ScatterSeries
            {
                Title = "typical point", MarkerType = MarkerType.Circle, MarkerSize = Math.Max(markerSize, 3.5),
                MarkerFill = OxyColors.Transparent, MarkerStroke = OxyColors.Black, MarkerStrokeThickness = 1.6,
            });
            AddLegend(model, legendBelow ? LegendPosition.BottomLeft : LegendPosition.RightTop);
            var legend = model.Legends[0];
            legend.LegendPlacement = LegendPlacement.Outside;
            legend.LegendSymbolLength = 14;
            if (legendBelow)
            {
                legend.LegendOrientation = LegendOrientation.Horizontal;
                legend.LegendItemSpacing = 8;
                legend.LegendSymbolMargin = 3;
            }
        }
        return model;
    }

    /// <summary>Single-linkage tree of a transitive closure; the dashed line is the selected threshold.</summary>
    public static PlotModel Dendrogram(FuzzyToleranceMatrix closure, double threshold)
    {
        var model = NewModel();
        int objectCount = closure.Size;
        var order = new List<int>();
        OrderLeaves([.. Enumerable.Range(0, objectCount)], closure, order);
        var position = new double[objectCount];
        for (int rank = 0; rank < objectCount; rank++) position[order[rank]] = rank + 1;

        double lowest = closure.GetDistinctValues().Min();
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom, Minimum = 0.3, Maximum = objectCount + 0.7, MajorStep = 1, MinorStep = 1,
            MinorTickSize = 0, Title = "object", AxisTitleDistance = 6,
            LabelFormatter = value => value >= 1 && value <= objectCount ? (order[(int)Math.Round(value) - 1] + 1).ToString() : "",
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left, Title = "level α of the closure", AxisTitleDistance = 6, Minimum = lowest - 0.004, Maximum = 1.0005,
            StartPosition = 1, EndPosition = 0, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = Grid,
        });

        DrawSubtree([.. Enumerable.Range(0, objectCount)], closure, position, model);
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal, Y = threshold, Color = OxyColors.Black, LineStyle = ThresholdStyle, StrokeThickness = 1.6,
            Text = $"α* = {threshold:0.#####}", TextColor = OxyColors.Black, TextHorizontalAlignment = HorizontalAlignment.Left,
            TextLinePosition = 0.02, TextVerticalAlignment = VerticalAlignment.Bottom,
        });
        return model;
    }

    private static List<int[]> Split(int[] members, FuzzyToleranceMatrix closure, out double mergeLevel)
    {
        mergeLevel = double.MaxValue;
        foreach (int first in members)
            foreach (int second in members)
                if (closure[first, second] < mergeLevel) mergeLevel = closure[first, second];

        var classes = new List<int[]>();
        var assigned = new HashSet<int>();
        foreach (int seed in members)
        {
            if (assigned.Contains(seed)) continue;
            double level = mergeLevel;
            var members1 = members.Where(other => closure[seed, other] > level).ToArray();
            foreach (int member in members1) assigned.Add(member);
            classes.Add(members1);
        }
        return classes;
    }

    private static void OrderLeaves(int[] members, FuzzyToleranceMatrix closure, List<int> order)
    {
        if (members.Length == 1) { order.Add(members[0]); return; }
        foreach (var part in Split(members, closure, out _)) OrderLeaves(part, closure, order);
    }

    private static (double X, double Y) DrawSubtree(int[] members, FuzzyToleranceMatrix closure, double[] position, PlotModel model)
    {
        if (members.Length == 1) return (position[members[0]], 1.0);

        var parts = Split(members, closure, out double mergeLevel);
        var children = parts.Select(part => DrawSubtree(part, closure, position, model)).ToList();
        var line = new LineSeries { Color = Ink, StrokeThickness = 1.6 };
        foreach (var child in children)
        {
            line.Points.Add(new DataPoint(child.X, child.Y));
            line.Points.Add(new DataPoint(child.X, mergeLevel));
            line.Points.Add(DataPoint.Undefined);
        }
        line.Points.Add(new DataPoint(children.Min(child => child.X), mergeLevel));
        line.Points.Add(new DataPoint(children.Max(child => child.X), mergeLevel));
        model.Series.Add(line);
        return (children.Average(child => child.X), mergeLevel);
    }

    /// <summary>Membership of every object in every cluster of an allotment.</summary>
    public static PlotModel MembershipProfiles(ClusterResult allotment)
    {
        var model = NewModel();
        model.Axes.Add(Axis(AxisPosition.Bottom, "object", grid: false));
        model.Axes.Add(Axis(AxisPosition.Left, "membership", minimum: Math.Floor(allotment.Alpha * 50) / 50 - 0.004, maximum: 1.002));
        for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
        {
            var series = new ScatterSeries
            {
                Title = $"cluster {cluster + 1}, typical point x{Subscript(allotment.Centers[cluster] + 1)}",
                MarkerType = Markers[cluster % Markers.Length], MarkerSize = 4.5, MarkerFill = Colour(cluster),
                MarkerStroke = Colour(cluster), MarkerStrokeThickness = 1.2,
            };
            foreach (int member in allotment.MemberSets[cluster])
                series.Points.Add(new ScatterPoint(member + 1, allotment.Membership[member, cluster]));
            model.Series.Add(series);
        }
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal, Y = allotment.Alpha, Color = OxyColors.Black, LineStyle = ThresholdStyle, StrokeThickness = 1.5,
            Text = $"α* = {allotment.Alpha:0.#####}", TextColor = OxyColors.Black, TextHorizontalAlignment = HorizontalAlignment.Left,
            TextLinePosition = 0.02, TextVerticalAlignment = VerticalAlignment.Bottom,
        });
        AddLegend(model, LegendPosition.BottomCenter);
        var legend = model.Legends[0];
        legend.LegendOrientation = LegendOrientation.Horizontal;
        legend.LegendPlacement = LegendPlacement.Outside;
        legend.LegendSymbolLength = 14;
        legend.LegendSymbolMargin = 3;
        legend.LegendItemSpacing = 12;
        return model;
    }

    /// <summary>The ascending levels of a closure with the leap that the heuristic selects.</summary>
    public static PlotModel LevelSequence(FuzzyToleranceMatrix closure)
    {
        var model = NewModel();
        var descending = closure.GetDistinctValues();
        var ascending = descending.Reverse().ToArray();
        double threshold = LeapHeuristicAlphaSelector.SelectFrom(descending)!.Value;
        int thresholdIndex = Array.IndexOf(ascending, threshold);

        model.Axes.Add(Axis(AxisPosition.Bottom, "index ℓ of the level"));
        model.Axes.Add(Axis(AxisPosition.Left, "level α_{ℓ} of the closure"));
        var series = new LineSeries { Color = Colour(0), StrokeThickness = 1.4, MarkerType = MarkerType.Circle, MarkerSize = 2.6, MarkerFill = Colour(0) };
        for (int index = 0; index < ascending.Length; index++) series.Points.Add(new DataPoint(index, ascending[index]));
        model.Series.Add(series);

        // the leap is drawn thick and black, so it stands out in greyscale as well
        var leap = new LineSeries { Color = OxyColors.Black, StrokeThickness = 3.5 };
        leap.Points.Add(new DataPoint(thresholdIndex - 1, ascending[thresholdIndex - 1]));
        leap.Points.Add(new DataPoint(thresholdIndex, ascending[thresholdIndex]));
        model.Series.Add(leap);
        model.Annotations.Add(new TextAnnotation
        {
            Text = $"largest leap: α* = {threshold:0.#####}", TextPosition = new DataPoint(thresholdIndex, ascending[thresholdIndex]),
            Offset = new ScreenVector(14, 6), Stroke = OxyColors.Transparent, TextColor = OxyColors.Black,
            TextHorizontalAlignment = HorizontalAlignment.Left, TextVerticalAlignment = VerticalAlignment.Middle,
        });
        return model;
    }

    /// <summary>Number of classes of a closure as a function of the level (the profile of the hierarchy).</summary>
    public static PlotModel ClassCountProfile(FuzzyToleranceMatrix closure)
    {
        var model = NewModel();
        var descending = closure.GetDistinctValues();
        double threshold = LeapHeuristicAlphaSelector.SelectFrom(descending)!.Value;
        model.Axes.Add(Axis(AxisPosition.Bottom, "level α of the closure"));
        var countAxis = LogAxis(AxisPosition.Left, "number of classes c(α)", minimum: 1, maximum: closure.Size * 1.2);
        model.Axes.Add(countAxis);

        var series = new StairStepSeries { Color = Colour(0), StrokeThickness = 2, VerticalStrokeThickness = 2 };
        double previous = descending[^1] - 0.02;
        series.Points.Add(new DataPoint(previous, 1));
        foreach (double level in descending.Reverse())
            series.Points.Add(new DataPoint(level, CountClasses(closure, level)));
        series.Points.Add(new DataPoint(1.0, closure.Size));
        model.Series.Add(series);
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical, X = threshold, Color = OxyColors.Black, LineStyle = ThresholdStyle, StrokeThickness = 1.6,
            Text = $"α* = {threshold:0.#####}", TextColor = OxyColors.Black, TextOrientation = AnnotationTextOrientation.Horizontal,
            TextHorizontalAlignment = HorizontalAlignment.Left, TextLinePosition = 0.5, TextPadding = 6,
        });
        return model;
    }

    private static int CountClasses(FuzzyToleranceMatrix closure, double level)
    {
        var assigned = new bool[closure.Size];
        int classes = 0;
        for (int seed = 0; seed < closure.Size; seed++)
        {
            if (assigned[seed]) continue;
            classes++;
            for (int member = 0; member < closure.Size; member++)
                if (closure[seed, member] >= level) assigned[member] = true;
        }
        return classes;
    }

    /// <summary>The three validity measures of D-AFC(u) against the maximal number of objects u.
    /// The lines differ in marker (filled or open), line style and colour.</summary>
    public static PlotModel SizeMeasures(ClusterSizeSelectionResult selection)
    {
        var model = NewModel();
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "maximal number of objects u", AxisTitleDistance = 6, Minimum = 2.6, Maximum = 8.4, MajorStep = 1, MinorStep = 1, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = Grid });
        model.Axes.Add(Axis(AxisPosition.Left, "value of the measure"));
        (string Title, Func<AllotmentMeasures, double> Value, LineStyle Style, bool OpenMarker)[] measures =
        [
            ("V_{LMF}, linear fuzziness", measure => measure.LinearFuzziness, LineStyle.Solid, false),
            ("V_{QMF}, quadratic fuzziness", measure => measure.QuadraticFuzziness, LineStyle.Dot, true),
            ("V_{MSC}, separation and compactness", measure => measure.SeparationCompactness, LineStyle.Solid, false),
        ];
        for (int index = 0; index < measures.Length; index++)
        {
            var series = new LineSeries
            {
                Title = measures[index].Title, Color = Colour(index), StrokeThickness = 2, LineStyle = measures[index].Style,
                MarkerType = Markers[index], MarkerSize = 5.5, MarkerStroke = Colour(index), MarkerStrokeThickness = 1.5,
                MarkerFill = measures[index].OpenMarker ? OxyColors.White : Colour(index),
            };
            foreach (var step in selection.Steps)
                if (step.Measures is not null) series.Points.Add(new DataPoint(step.MaxClusterPower, measures[index].Value(step.Measures)));
            model.Series.Add(series);
        }
        if (selection.OptimalMaxClusterPower is int optimal)
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical, X = optimal, Color = OxyColors.Black, LineStyle = ThresholdStyle, StrokeThickness = 1.4,
                Text = $"u* = {optimal}", TextColor = OxyColors.Black, TextOrientation = AnnotationTextOrientation.Horizontal,
                TextHorizontalAlignment = HorizontalAlignment.Left, TextLinePosition = 0.45, TextPadding = 6,
            });
        AddLegend(model, LegendPosition.BottomCenter);
        var legend = model.Legends[0];
        legend.LegendPlacement = LegendPlacement.Outside;
        legend.LegendSymbolLength = 34;
        return model;
    }

    /// <summary>Possibility distributions of the number of clusters and their α-level.</summary>
    public static PlotModel Bounds(int objectCount, int clusterCount, double alpha)
    {
        var model = NewModel();
        int shown = 45;
        model.Axes.Add(Axis(AxisPosition.Bottom, "number of clusters", minimum: 1, maximum: shown));
        model.Axes.Add(Axis(AxisPosition.Left, "possibility degree", minimum: 0.6, maximum: 1.02));
        foreach (var (shape, title, index, style, openMarker) in new[]
                 {
                     (ClusterCountShape.Triangular, "triangular", 0, LineStyle.Solid, false),
                     (ClusterCountShape.Gaussian, "Gaussian", 1, LineStyle.Dot, true),
                 })
        {
            var bounds = ClusterCountBounds.Compute(objectCount, clusterCount, alpha, shape);
            var series = new LineSeries
            {
                Title = $"{title}: {{{bounds.Least}, …, {bounds.Largest}}}", Color = Colour(index), StrokeThickness = 2, LineStyle = style,
                MarkerType = Markers[index], MarkerSize = 4.5, MarkerStroke = Colour(index), MarkerStrokeThickness = 1.4,
                MarkerFill = openMarker ? OxyColors.White : Colour(index),
            };
            for (int count = 1; count <= shown; count++)
                series.Points.Add(new DataPoint(count, ClusterCountBounds.Possibility(objectCount, clusterCount, alpha, shape, count)));
            model.Series.Add(series);
        }
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal, Y = alpha, Color = OxyColors.Black, LineStyle = ThresholdStyle, StrokeThickness = 1.5,
            Text = $"α = {alpha:0.#####}", TextColor = OxyColors.Black, TextHorizontalAlignment = HorizontalAlignment.Right,
        });
        AddLegend(model);
        return model;
    }

    /// <summary>The antecedent fuzzy sets of one input variable of a rule base; the sets differ in line style as well as in colour.</summary>
    public static PlotModel Antecedents(FuzzyInferenceSystem system, int inputIndex, string title)
    {
        var model = NewModel();
        var variable = system.Inputs[inputIndex];
        model.Axes.Add(Axis(AxisPosition.Bottom, title, minimum: variable.Min, maximum: variable.Max));
        model.Axes.Add(Axis(AxisPosition.Left, "membership", minimum: 0, maximum: 1.05));
        LineStyle[] styles = [LineStyle.Solid, LineStyle.LongDash, LineStyle.Dot, LineStyle.DashDot];
        for (int term = 0; term < variable.Terms.Count; term++)
        {
            var series = new LineSeries { Title = variable.Terms[term].Name, Color = Colour(term), StrokeThickness = 2.4, LineStyle = styles[term % styles.Length] };
            const int samples = 600;
            for (int sample = 0; sample <= samples; sample++)
            {
                double value = variable.Min + (variable.Max - variable.Min) * sample / samples;
                series.Points.Add(new DataPoint(value, variable.Terms[term].Mf.Evaluate(value)));
            }
            model.Series.Add(series);
        }
        AddLegend(model);
        model.Legends[0].LegendPlacement = LegendPlacement.Outside;
        model.Legends[0].LegendSymbolLength = 40;
        return model;
    }

    /// <summary>Time of the transitive closure against the number of objects, per technique (log–log).</summary>
    public static PlotModel ClosureTimes(IReadOnlyList<JsonElement> measurements)
    {
        var model = NewModel();
        model.Axes.Add(LogAxis(AxisPosition.Bottom, "number of objects n", minimum: 120, maximum: 2500, powersOfTen: false));
        model.Axes.Add(LogAxis(AxisPosition.Left, "median time, ms", maximum: 1e5));

        var closure = measurements
            .Where(record => record.GetProperty("experiment").GetString() == "closure" && record.GetProperty("cores").GetInt32() > 1)
            .GroupBy(record => record.GetProperty("variant").GetString()!)
            .ToList();
        int index = 0;
        foreach (var variant in closure)
        {
            bool isSpanningTree = variant.Key.StartsWith("Spanning", StringComparison.Ordinal);
            bool lastOfAll = index == closure.Count - 1;
            // every series has its own marker; the last one (a circle again) is open to tell it from the first
            var series = new LineSeries
            {
                Title = variant.Key.Replace(" (library)", "").Replace(" (former library)", ""),
                Color = Colour(index), StrokeThickness = 1.8, MarkerType = Markers[index % Markers.Length], MarkerSize = 5.5,
                MarkerStroke = Colour(index), MarkerStrokeThickness = 1.5, MarkerFill = lastOfAll ? OxyColors.White : Colour(index),
                LineStyle = isSpanningTree ? LineStyle.Solid : LineStyle.Dash,
            };
            foreach (var record in variant.OrderBy(record => record.GetProperty("objectCount").GetInt32()))
                series.Points.Add(new DataPoint(record.GetProperty("objectCount").GetInt32(), record.GetProperty("medianMilliseconds").GetDouble()));
            model.Series.Add(series);
            index++;
        }
        AddLegend(model, LegendPosition.LeftTop);
        model.Legends[0].LegendSymbolLength = 36;
        model.Legends[0].LegendItemSpacing = 6;
        return model;
    }

    /// <summary>
    /// Time of the D-AFC(3) search on Iris in the configurations of the ablation table of the book, one bar per row of the
    /// table and in the same order (logarithmic scale). The rows are the measurements of the search experiment.
    /// </summary>
    public static PlotModel SearchTimes(IReadOnlyList<JsonElement> measurements)
    {
        var model = NewModel();
        // (label of the table row, variant of the measurement, number of cores)
        (string Label, string Variant, int Cores)[] rows =
        [
            ("Naïve enumeration", "Plain enumeration of all combinations", 1),
            ("+ level pre-filter", "Pre-filter + plain enumeration", 1),
            ("+ bit-set engine, pruning,\ndeferred scoring", "Bit-set engine + pre-filter", 1),
            ("+ exact-cover bound", "Bit-set engine + pre-filter + exact-cover bound (library)", 1),
            ("+ parallel levels", "Bit-set engine + pre-filter + exact-cover bound (library)", 24),
        ];
        var search = measurements.Where(record => record.GetProperty("experiment").GetString() == "search").ToList();
        var times = rows.Select(row => search.Single(record =>
                record.GetProperty("variant").GetString() == row.Variant && record.GetProperty("cores").GetInt32() == row.Cores))
            .ToList();

        var categories = new CategoryAxis { Position = AxisPosition.Left, GapWidth = 0.45, StartPosition = 1, EndPosition = 0, TicklineColor = OxyColor.FromRgb(90, 90, 90) };
        foreach (var row in rows) categories.Labels.Add(row.Label);
        var bars = new BarSeries { FillColor = Colour(0), BaseValue = 1, StrokeThickness = 0 };
        for (int row = 0; row < rows.Length; row++)
        {
            double milliseconds = times[row].GetProperty("medianMilliseconds").GetDouble();
            bool extrapolated = times[row].GetProperty("extrapolated").GetBoolean();
            bars.Items.Add(new BarItem(milliseconds));
            model.Annotations.Add(new TextAnnotation
            {
                Text = FormatDuration(milliseconds) + (extrapolated ? "†" : ""), TextPosition = new DataPoint(milliseconds, row),
                Offset = new ScreenVector(5, 0), Stroke = OxyColors.Transparent, TextColor = OxyColors.Black,
                TextHorizontalAlignment = HorizontalAlignment.Left, TextVerticalAlignment = VerticalAlignment.Middle,
            });
        }
        model.Axes.Add(categories);
        model.Axes.Add(LogAxis(AxisPosition.Bottom, "time, ms", minimum: 1, maximum: 6e6));
        model.Series.Add(bars);
        return model;
    }

    /// <summary>A duration as in the tables of the book: seconds above one second (one decimal below 100 s), else milliseconds.</summary>
    private static string FormatDuration(double milliseconds)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (milliseconds >= 100_000) return (milliseconds / 1000).ToString("0", culture) + " s";
        if (milliseconds >= 1000) return (milliseconds / 1000).ToString("0.0", culture) + " s";
        return milliseconds.ToString("0", culture) + " ms";
    }

    private static string Subscript(int number) =>
        string.Concat(number.ToString().Select(digit => (char)('₀' + (digit - '0'))));

    private static string Superscript(int number)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        return string.Concat(number.ToString().Select(character => character == '-' ? '⁻' : digits[character - '0']));
    }
}
