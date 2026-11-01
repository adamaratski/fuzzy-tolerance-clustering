using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Clustering.Results;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MathFuzzy.Desktop.ViewModels.Results;

public sealed class ResultsPanelViewModel
{
    public MatrixHeatmapTabViewModel ToleranceMatrixTab  { get; } = new("Tolerance");
    public ClusterTableTabViewModel  ClusterTableTab     { get; } = new();
    public MembershipHeatmapTabViewModel MembershipTab   { get; } = new();
    public DendrogramTabViewModel    DendrogramTab       { get; } = new();
    public ValidityIndicesTabViewModel ValidityTab       { get; } = new();

    public IReadOnlyList<ResultTabViewModel> Tabs => [
        ToleranceMatrixTab, MembershipTab, ClusterTableTab,
        DendrogramTab, ValidityTab
    ];
}

public abstract class ResultTabViewModel : ObservableObject
{
    public abstract string Title { get; }

    public override string ToString() => Title;

    internal static readonly string[] Subscripts =
        ["₁", "₂", "₃", "₄", "₅", "₆", "₇", "₈", "₉"];

    internal static string Sub(int oneBased) =>
        oneBased <= Subscripts.Length ? Subscripts[oneBased - 1] : oneBased.ToString();
}


/// <summary>Tolerance tab: raw T / closure T̃ toggle, α-slice threshold, level count.</summary>
public sealed partial class MatrixHeatmapTabViewModel(string title) : ResultTabViewModel
{
    public override string Title { get; } = title;

    private double[,]? _raw;
    private double[,]? _closure;

    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private int    _matrixSize;
    [ObservableProperty] private int    _levelCount;
    [ObservableProperty] private bool   _showClosure;
    [ObservableProperty] private bool   _hasClosure;
    [ObservableProperty] private double _alphaSlice;

    public void Update(double[,] data)
    {
        _raw = data;
        if (!HasClosure) ShowClosure = false;
        Rebuild();
    }

    public void UpdateClosure(double[,] data)
    {
        _closure   = data;
        HasClosure = true;
        Rebuild();
    }

    public void Clear()
    {
        _raw = _closure = null;
        HasClosure  = false;
        ShowClosure = false;
        AlphaSlice  = 0;
        MatrixSize  = 0;
        LevelCount  = 0;
        Plot = null;
    }

    partial void OnShowClosureChanged(bool value)  => Rebuild();
    partial void OnAlphaSliceChanged(double value) => Rebuild();

    private void Rebuild()
    {
        var src = ShowClosure && _closure is not null ? _closure : _raw;
        if (src is null) { Plot = null; return; }

        int size = src.GetLength(0);
        MatrixSize = size;
        LevelCount = CountLevels(src);

        var data = src;
        if (AlphaSlice > 0)
        {
            data = new double[size, size];
            for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                data[i, j] = src[i, j] >= AlphaSlice ? src[i, j] : 0;
        }

        var model = new PlotModel { Background = DesignPalette.PlotBackground };

        model.Series.Add(new HeatMapSeries
        {
            X0 = -0.5, X1 = size - 0.5,
            Y0 = -0.5, Y1 = size - 0.5,
            Interpolate = false,
            Data = data,
            TrackerFormatString = "T[{4:0}, {2:0}]\nμ = {6:0.0000}"
        });

        model.Axes.Add(new LinearColorAxis
        {
            Palette       = DesignPalette.BlueRamp(),
            Minimum       = 0,
            Maximum       = 1,
            Position      = AxisPosition.Right,
            Title         = "μ",
            TextColor     = DesignPalette.AxisText,
            AxislineColor = DesignPalette.AxisLine
        });

        AddMatrixAxes(model, size);
        Plot = model;
    }

    private static int CountLevels(double[,] m)
    {
        int size = m.GetLength(0);
        var distinct = new HashSet<double>();
        for (int i = 0; i < size; i++)
        for (int j = i + 1; j < size; j++)
            distinct.Add(System.Math.Round(m[i, j], 6));
        return distinct.Count;
    }

    private static void AddMatrixAxes(PlotModel model, int size)
    {
        model.Axes.Add(new LinearAxis
        {
            Position      = AxisPosition.Bottom,
            Title         = "Object j",
            Minimum       = -0.5,
            Maximum       = size - 0.5,
            MajorStep     = size > 20 ? 5 : 1,
            MinorTickSize = 0,
            TextColor     = DesignPalette.AxisText,
            AxislineColor = DesignPalette.AxisLine,
            TicklineColor = DesignPalette.AxisLine
        });
        model.Axes.Add(new LinearAxis
        {
            Position      = AxisPosition.Left,
            Title         = "Object i",
            Minimum       = -0.5,
            Maximum       = size - 0.5,
            MajorStep     = size > 20 ? 5 : 1,
            MinorTickSize = 0,
            TextColor     = DesignPalette.AxisText,
            AxislineColor = DesignPalette.AxisLine,
            TicklineColor = DesignPalette.AxisLine
        });
    }
}


public sealed class ClusterResultRow(int rank, ClusterResult r)
{
    public int    Rank           { get; } = rank;
    public int    C              { get; } = r.ClusterCount;
    public string Alpha          { get; } = r.Alpha.ToString("F4");
    public string Score          { get; } = r.Score.ToString("F4");
    /// <summary>Geometric tiebreaker score (D-AFC-TC only). Lower = more compact.</summary>
    public string TiebreakerDist { get; } = r.TiebreakerScore.HasValue
                                              ? r.TiebreakerScore.Value.ToString("F4")
                                              : "—";
    public string Centers        { get; } = string.Join(", ", r.Centers.Select(c => c + 1));
    public string ClusterSizes   { get; } = string.Join(" | ", r.MemberSets.Select(s => s.Length));

    public IReadOnlyList<string> ClusterPills { get; } = r.MemberSets
        .Select((s, i) =>
            $"C{ResultTabViewModel.Sub(i + 1)} · centre {r.Centers[i] + 1} · objects {RangeText(s)}")
        .ToList();

    public string IntersectionsText { get; } = $"intersections: {r.IntersectionCount}";

    public override string ToString() => $"#{Rank}: c={C}, α={Alpha}, score={Score}, sizes {ClusterSizes}";

    /// <summary>Compact 1-based range list: "1–50" or "1–3, 7, 9–12" (max 4 runs, then …).</summary>
    private static string RangeText(int[] objects)
    {
        if (objects.Length == 0) return "—";
        var sorted = objects.OrderBy(x => x).ToArray();
        var runs = new List<string>();
        int start = sorted[0], prev = sorted[0];
        for (int k = 1; k <= sorted.Length; k++)
        {
            if (k < sorted.Length && sorted[k] == prev + 1) { prev = sorted[k]; continue; }
            runs.Add(start == prev ? $"{start + 1}" : $"{start + 1}–{prev + 1}");
            if (k < sorted.Length) { start = prev = sorted[k]; }
        }
        return runs.Count <= 4
            ? string.Join(", ", runs)
            : string.Join(", ", runs.Take(4)) + ", …";
    }
}

public sealed partial class ClusterTableTabViewModel : ResultTabViewModel
{
    public override string Title => "Clusters";

    [ObservableProperty] private IReadOnlyList<ClusterResultRow> _rows = [];
    [ObservableProperty] private ClusterResultRow? _selectedRow;
    [ObservableProperty] private string _summary = "No results yet.";

    public void Update(IReadOnlyList<ClusterResult> results)
    {
        Rows = results.Select((r, i) => new ClusterResultRow(i + 1, r)).ToList();
        SelectedRow = Rows.FirstOrDefault();
        Summary = results.Count > 0
            ? $"Best: c={results[0].ClusterCount}, α={results[0].Alpha:F4}, F₁={results[0].Score:F4}"
            : "No valid clustering found.";
    }
}


public sealed record MembershipRow(string Label, string Value, bool IsMax);

/// <summary>Membership tab: solution picker, μ threshold, per-object detail panel.</summary>
public sealed partial class MembershipHeatmapTabViewModel : ResultTabViewModel
{
    public override string Title => "Membership U";

    private IReadOnlyList<ClusterResult> _results = [];
    private int[] _rowToObject = [];

    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private IReadOnlyList<string> _solutionItems = [];
    [ObservableProperty] private int    _selectedSolutionIndex = -1;
    [ObservableProperty] private double _threshold;
    [ObservableProperty] private bool   _sortByCluster = true;
    [ObservableProperty] private string _selectedObjectTitle = "";
    [ObservableProperty] private IReadOnlyList<MembershipRow> _objectRows = [];

    public void Update(IReadOnlyList<ClusterResult> results)
    {
        _results = results;
        SolutionItems = results
            .Select((r, i) => $"#{i + 1} · c={r.ClusterCount} · α={r.Alpha:F2} · F₁={r.Score:F3}")
            .ToList();

        if (results.Count == 0)
        {
            SelectedSolutionIndex = -1;
            Plot = null;
            SelectedObjectTitle = "";
            ObjectRows = [];
            return;
        }

        if (SelectedSolutionIndex == 0) Rebuild();
        else SelectedSolutionIndex = 0;
    }

    partial void OnSelectedSolutionIndexChanged(int value) => Rebuild();
    partial void OnThresholdChanged(double value)          => Rebuild();
    partial void OnSortByClusterChanged(bool value)        => Rebuild();

    private ClusterResult? Current =>
        SelectedSolutionIndex >= 0 && SelectedSolutionIndex < _results.Count
            ? _results[SelectedSolutionIndex]
            : null;

    private void Rebuild()
    {
        if (Current is not { } best) { Plot = null; return; }

        int n = best.Membership.GetLength(0);
        int c = best.ClusterCount;

        _rowToObject = Enumerable.Range(0, n).ToArray();
        if (SortByCluster)
        {
            _rowToObject = _rowToObject
                .OrderBy(obj => ArgMax(best, obj, c))
                .ThenBy(obj => obj)
                .ToArray();
        }

        var data = new double[n, c];
        for (int row = 0; row < n; row++)
        for (int k = 0; k < c; k++)
        {
            double mu = best.Membership[_rowToObject[row], k];
            data[row, k] = mu >= Threshold ? mu : 0;
        }

        var model = new PlotModel { Background = DesignPalette.PlotBackground };

        model.Series.Add(new HeatMapSeries
        {
            X0 = -0.5, X1 = c - 0.5,
            Y0 = -0.5, Y1 = n - 0.5,
            Interpolate = false,
            Data = Transpose(data, n, c),
            TrackerFormatString = "Object row {4:0}\nμ = {6:0.0000}"
        });

        model.Axes.Add(new LinearColorAxis
        {
            Palette   = DesignPalette.BlueRamp(),
            Minimum   = 0,
            Maximum   = 1,
            Position  = AxisPosition.Right,
            Title     = "μ",
            TextColor = DesignPalette.AxisText
        });
        model.Axes.Add(new LinearAxis
        {
            Position  = AxisPosition.Bottom,
            Title     = "Cluster",
            Minimum   = -0.5,
            Maximum   = c - 0.5,
            MajorStep = 1,
            TextColor = DesignPalette.AxisText
        });
        model.Axes.Add(new LinearAxis
        {
            Position  = AxisPosition.Left,
            Title     = SortByCluster ? "Object (sorted by cluster)" : "Object",
            Minimum   = -0.5,
            Maximum   = n - 0.5,
            MajorStep = n > 20 ? 5 : 1,
            TextColor = DesignPalette.AxisText
        });

        Plot = model;

        // Default detail: centre of the first cluster
        if (best.Centers.Length > 0)
            ShowObject(best.Centers[0]);
    }

    /// <summary>Display-row click from the view (row index in the sorted heatmap).</summary>
    public void SelectDisplayRow(int row)
    {
        if (row < 0 || row >= _rowToObject.Length) return;
        ShowObject(_rowToObject[row]);
    }

    private void ShowObject(int obj)
    {
        if (Current is not { } best) return;
        int c = best.ClusterCount;
        int maxK = ArgMax(best, obj, c);

        int centreOf = Array.IndexOf(best.Centers, obj);
        SelectedObjectTitle = centreOf >= 0
            ? $"Object {obj + 1} (centre C{Sub(centreOf + 1)})"
            : $"Object {obj + 1}";

        ObjectRows = Enumerable.Range(0, c)
            .Select(k => new MembershipRow(
                $"μ(C{Sub(k + 1)})",
                best.Membership[obj, k].ToString("F2"),
                k == maxK))
            .ToList();
    }

    private static int ArgMax(ClusterResult r, int obj, int c)
    {
        int best = 0;
        for (int k = 1; k < c; k++)
            if (r.Membership[obj, k] > r.Membership[obj, best]) best = k;
        return best;
    }

    private static double[,] Transpose(double[,] m, int rows, int cols)
    {
        // HeatMapSeries expects Data[x, y]; our logical layout is [row(y), col(x)].
        var t = new double[cols, rows];
        for (int r = 0; r < rows; r++)
        for (int k = 0; k < cols; k++)
            t[k, r] = m[r, k];
        return t;
    }
}


public sealed class DendrogramLevelRow(int level, ClusterResult r)
{
    public int    Level        { get; } = level;
    public string Alpha        { get; } = r.Alpha.ToString("F4");
    public double AlphaValue   { get; } = r.Alpha;
    public int    C            { get; } = r.ClusterCount;
    public string ClusterSizes { get; } = string.Join(" | ", r.MemberSets.Select(s => s.Length));
    public string Score        { get; } = r.Score.ToString("F4");
}

public sealed partial class DendrogramTabViewModel : ResultTabViewModel
{
    public override string Title => "Dendrogram";

    [ObservableProperty] private IReadOnlyList<DendrogramLevelRow> _levels = [];
    [ObservableProperty] private bool   _hasResult;
    [ObservableProperty] private HierarchicalResult? _result;
    [ObservableProperty] private double _alphaStar = double.NaN;
    [ObservableProperty] private DendrogramLevelRow? _selectedLevel;

    public void Clear()
    {
        Result        = null;
        HasResult     = false;
        AlphaStar     = double.NaN;
        SelectedLevel = null;
        Levels        = [];
    }

    public void Update(HierarchicalResult result, double alphaStar = double.NaN)
    {
        Result    = result;
        AlphaStar = alphaStar;
        HasResult = true;
        Levels = result.Levels
            .OrderBy(r => r.Alpha)
            .Select((r, i) => new DendrogramLevelRow(i + 1, r))
            .ToList();
        SelectedLevel = double.IsNaN(alphaStar)
            ? Levels.FirstOrDefault()
            : Levels.FirstOrDefault(l => System.Math.Abs(l.AlphaValue - alphaStar) < 1e-9) ?? Levels.FirstOrDefault();
    }
}


public sealed partial class ValidityIndicesTabViewModel : ResultTabViewModel
{
    public override string Title => "Validity";

    [ObservableProperty] private IReadOnlyList<ValidityRow> _rows = [];
    [ObservableProperty] private PlotModel? _plot;
    [ObservableProperty] private bool   _hasExternal;
    [ObservableProperty] private string _purity   = "";
    [ObservableProperty] private string _ari      = "";
    [ObservableProperty] private string _nmi      = "";
    [ObservableProperty] private string _fm       = "";
    [ObservableProperty] private string _ri       = "";
    [ObservableProperty] private IReadOnlyList<string> _majorityItems = [];

    public void Update(IReadOnlyList<ClusterResult> results)
    {
        Rows = results.Select((r, i) => new ValidityRow(i + 1, r)).ToList();
        BuildPlot(results);
    }

    public void UpdateExternal(MathFuzzy.Clustering.Validation.ExternalValidationResult ext)
    {
        HasExternal = true;
        Purity = ext.Purity.ToString("F4");
        Ari    = ext.AdjustedRandIndex.ToString("F4");
        Nmi    = ext.NormalizedMutualInfo.ToString("F4");
        Fm     = ext.FowlkesMallows.ToString("F4");
        Ri     = ext.RandIndex.ToString("F4");
        MajorityItems = ext.ClusterMajority
            .Select((m, i) => $"C{Sub(i + 1)} → {m}")
            .ToList();
    }

    public void ClearExternal() { HasExternal = false; }

    private static string CriterionLabel(IReadOnlyList<ClusterResult> results) =>
        results.Count > 0 && results[0].CriterionName.Equals("f2", StringComparison.OrdinalIgnoreCase) ? "F₂" : "F₁";

    private void BuildPlot(IReadOnlyList<ClusterResult> results)
    {
        var model = new PlotModel { Background = DesignPalette.PlotBackground };

        var f1Line = new LineSeries
        {
            Title           = "F₁",
            Color           = DesignPalette.Blue400,
            StrokeThickness = 2,
            MarkerType      = MarkerType.Circle,
            MarkerSize      = 4,
            MarkerFill      = DesignPalette.Blue200
        };

        foreach (var r in results.OrderBy(r => r.Alpha))
            f1Line.Points.Add(new DataPoint(r.Alpha, r.Score));

        model.Series.Add(f1Line);

        if (results.Count > 0)
        {
            var best = results[0];
            model.Annotations.Add(new LineAnnotation
            {
                Type            = LineAnnotationType.Vertical,
                X               = best.Alpha,
                Color           = DesignPalette.Coral,
                LineStyle       = LineStyle.Dash,
                StrokeThickness = 1,
                Text            = $"α* = {best.Alpha:F4} · {CriterionLabel(results)} = {best.Score:F3}",
                TextColor       = DesignPalette.Coral,
                FontSize        = 11,
                TextOrientation = AnnotationTextOrientation.Horizontal,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment   = OxyPlot.VerticalAlignment.Top,
                TextLinePosition = 1.0,
                TextMargin      = 6
            });
            model.Annotations.Add(new PointAnnotation
            {
                X      = best.Alpha,
                Y      = best.Score,
                Size   = 5,
                Fill   = DesignPalette.Coral,
                Stroke = DesignPalette.Coral
            });
        }

        // α lives in (0, 1]: pad the observed range a little but never leave [0, 1].
        double aMin = results.Count > 0 ? results.Min(r => r.Alpha) : 0;
        double aMax = results.Count > 0 ? results.Max(r => r.Alpha) : 1;
        double aPad = System.Math.Max((aMax - aMin) * 0.08, 0.02);
        string crit = CriterionLabel(results);
        f1Line.Title = crit;
        model.Axes.Add(new LinearAxis
        {
            Position  = AxisPosition.Bottom,
            Title     = "α",
            Minimum   = System.Math.Max(0, aMin - aPad),
            Maximum   = System.Math.Min(1, aMax + aPad),
            TextColor = DesignPalette.AxisText
        });
        model.Axes.Add(new LinearAxis
        {
            Position  = AxisPosition.Left,
            Title     = crit,
            MinimumPadding = 0.1,
            MaximumPadding = 0.1,
            TextColor = DesignPalette.AxisText
        });

        Plot = model;
    }
}

public sealed class ValidityRow(int rank, ClusterResult r)
{
    public int    Rank  { get; } = rank;
    public int    C     { get; } = r.ClusterCount;
    public string Alpha { get; } = r.Alpha.ToString("F4");
    public string F1    { get; } = r.Score.ToString("F6");
}
