using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Desktop.Services;
using MathFuzzy.Desktop.ViewModels.Results;
using MathFuzzy.Desktop.ViewModels.Workspace;
using MathFuzzy.Inference.Evaluation;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;
using MathFuzzy.Inference.Serialization;
using MathFuzzy.Inference.Systems;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MathFuzzy.Desktop.ViewModels.Inference;

/// <summary>
/// Inference (FIS) page: generates a rule base from a clustering solution of the active
/// workset (one rule per fuzzy cluster, Viattchenin 2013, sec. 4.5), lets the user edit
/// rules and membership functions, evaluates an input vector live and the whole workset.
/// </summary>
public sealed partial class InferencePageViewModel : ObservableObject, IMfHandleDragTarget
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly IWorkspaceService _workspace;
    private readonly DispatcherTimer _evaluateTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    private Workset? _workset;
    private FuzzyInferenceSystem? _fis;
    private ClusterResult? _solution;
    /// <summary>Data column of every system input; null when the system does not match the workset.</summary>
    private int[]? _inputColumns;
    /// <summary>Target column for regression systems.</summary>
    private int _targetColumn = -1;
    private bool _loading;

    // ── option lists ─────────────────────────────────────────────────────
    public IReadOnlyList<string> RuleBaseKinds { get; } =
        ["Allotment (Mamdani MIMO)", "Classifier", "Mamdani regression", "Sugeno order 0", "Sugeno order 1"];
    public IReadOnlyList<string> Shapes { get; } = ["Trapezoidal (monograph)", "Triangular", "Gaussian"];
    public IReadOnlyList<TNorm> TNorms { get; } = Enum.GetValues<TNorm>();
    public IReadOnlyList<SNorm> SNorms { get; } = Enum.GetValues<SNorm>();
    public IReadOnlyList<Implication> Implications { get; } = Enum.GetValues<Implication>();
    public IReadOnlyList<Defuzzification> Defuzzifications { get; } = Enum.GetValues<Defuzzification>();

    // ── generation ───────────────────────────────────────────────────────
    [ObservableProperty] private IReadOnlyList<string> _solutions = [];
    [ObservableProperty] private int _selectedSolution = -1;
    [ObservableProperty] private int _selectedKind;
    [ObservableProperty] private int _selectedShape;
    [ObservableProperty] private IReadOnlyList<string> _features = [];
    [ObservableProperty] private int _targetIndex = -1;

    [ObservableProperty] private bool _hasSystem;
    [ObservableProperty] private string _emptyMessage = "Run a clustering on the dashboard; the rule base is generated from its solution.";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsError;

    public bool IsRegressionKind => SelectedKind >= 2;

    // ── rules, inputs, MF editor ─────────────────────────────────────────
    public ObservableCollection<RuleRowViewModel> Rules { get; } = [];
    public ObservableCollection<InputSliderViewModel> Inputs { get; } = [];
    public ObservableCollection<TermEditorViewModel> TermEditors { get; } = [];

    [ObservableProperty] private IReadOnlyList<string> _variables = [];
    [ObservableProperty] private int _selectedVariable = -1;
    [ObservableProperty] private PlotModel? _mfPlot;
    public IReadOnlyList<MfHandle> Handles { get; private set; } = [];

    [ObservableProperty] private bool _isSugeno;
    [ObservableProperty] private bool _isClassifying;

    // ── live output ──────────────────────────────────────────────────────
    [ObservableProperty] private IReadOnlyList<ClassScoreViewModel> _classScores = [];
    [ObservableProperty] private string _decisionText = "";
    [ObservableProperty] private string _crispText = "";
    [ObservableProperty] private IReadOnlyList<string> _outputNames = [];
    [ObservableProperty] private int _selectedOutput;
    [ObservableProperty] private PlotModel? _outputPlot;
    [ObservableProperty] private bool _showOutputPlot;
    [ObservableProperty] private string _objectNumberText = "1";
    [ObservableProperty] private string _objectInfo = "";

    // ── operators ────────────────────────────────────────────────────────
    [ObservableProperty] private TNorm _andMethod;
    [ObservableProperty] private SNorm _orMethod;
    [ObservableProperty] private Implication _implicationMethod;
    [ObservableProperty] private SNorm _aggregationMethod;
    [ObservableProperty] private Defuzzification _defuzzificationMethod;

    // ── workset evaluation ───────────────────────────────────────────────
    [ObservableProperty] private bool _canEvaluate;
    [ObservableProperty] private string _evaluationTitle = "";
    [ObservableProperty] private string _accuracyText = "—";
    [ObservableProperty] private string _countsText = "";
    [ObservableProperty] private IReadOnlyList<string> _confusionHeader = [];
    [ObservableProperty] private IReadOnlyList<ConfusionRowViewModel> _confusionRows = [];
    [ObservableProperty] private bool _showConfusion;
    [ObservableProperty] private string _rmseText = "—";
    [ObservableProperty] private string _maeText = "—";
    [ObservableProperty] private bool _showRegression;
    [ObservableProperty] private string _evaluationNote = "";

    public InferencePageViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.DataChanged    += (_, _) => OnWorkspaceChanged(resultsChanged: false);
        _workspace.ResultsChanged += (_, _) => OnWorkspaceChanged(resultsChanged: true);
        _evaluateTimer.Tick += (_, _) => { _evaluateTimer.Stop(); EvaluateWorkset(); };
        OnWorkspaceChanged(resultsChanged: true);
    }

    // ─── Workspace → page ─────────────────────────────────────────────────

    private void OnWorkspaceChanged(bool resultsChanged)
    {
        var workset = _workspace.ActiveWorkset;
        bool switched = !ReferenceEquals(workset, _workset);
        if (!switched && !resultsChanged) return;   // e.g. T rebuilt: the rule base does not depend on it
        _workset = workset;

        _loading = true;
        Features = workset is null ? [] : FeatureNames(workset);
        // the last column is the usual response in tabular datasets; keep the user's choice within one workset
        if (switched || TargetIndex < 0 || TargetIndex >= Features.Count) TargetIndex = Features.Count - 1;
        Solutions = (workset?.Results ?? []).Select((result, rank) =>
                $"#{rank + 1} · c = {result.ClusterCount} · α = {result.Alpha.ToString("0.#####", Invariant)}").ToList();
        SelectedSolution = Solutions.Count > 0 ? 0 : -1;
        _loading = false;

        if (Solutions.Count > 0) Generate();
        else ClearSystem(workset?.RawData is null
            ? "Open a dataset with raw features; rules are built in feature space."
            : "Run a clustering on the dashboard; the rule base is generated from its solution.");
    }

    partial void OnSelectedKindChanged(int value)
    {
        OnPropertyChanged(nameof(IsRegressionKind));
        if (!_loading && Solutions.Count > 0) Generate();
    }

    partial void OnSelectedShapeChanged(int value)    { if (!_loading && Solutions.Count > 0) Generate(); }
    partial void OnSelectedSolutionChanged(int value) { if (!_loading && value >= 0) Generate(); }
    partial void OnTargetIndexChanged(int value)      { if (!_loading && IsRegressionKind && Solutions.Count > 0) Generate(); }

    // ─── Generation ───────────────────────────────────────────────────────

    [RelayCommand]
    private void Generate()
    {
        if (_workset is not { RawData: { } data } workset || SelectedSolution < 0 || SelectedSolution >= workset.Results.Count)
        {
            ClearSystem(EmptyMessage);
            return;
        }
        var result = workset.Results[SelectedSolution];
        var kind = (RuleBaseKind)SelectedKind;
        bool regression = kind is RuleBaseKind.Mamdani or RuleBaseKind.SugenoZero or RuleBaseKind.SugenoFirst;
        if (regression && data.Cols < 2)
        {
            ClearSystem("Regression needs at least two columns: one target and one input.");
            return;
        }

        try
        {
            var fis = ClusterRuleExtractor.Extract(data, result, new RuleExtractionOptions
            {
                Kind         = kind,
                Shape        = SelectedShape switch { 1 => MfShape.Triangular, 2 => MfShape.Gaussian, _ => MfShape.Trapezoidal },
                TargetColumn = regression ? TargetIndex : -1,
                FeatureNames = Features,
                Labels       = workset.Labels?.TrueLabels,
                ClassNames   = workset.Labels?.ClassNames,
                Name         = Path.GetFileNameWithoutExtension(workset.Name),
            });
            _solution = result;
            _targetColumn = regression ? TargetIndex : -1;
            _inputColumns = Enumerable.Range(0, data.Cols).Where(column => column != _targetColumn).ToArray();
            Load(fis);
            Summary = $"{fis.Rules.Count} rules from solution #{SelectedSolution + 1} (c = {result.ClusterCount}, α = {result.Alpha.ToString("0.#####", Invariant)}) · "
                    + $"{fis.Inputs.Count} inputs · {DescribeKind(fis)}";
            ShowStatus("");
        }
        catch (ArgumentException ex)
        {
            ClearSystem(result.Membership.GetLength(0) != data.Rows
                ? "The selected solution clusters attributes, not objects (dimension reduction); rules need clusters of objects."
                : ex.Message);
        }
    }

    private static string DescribeKind(FuzzyInferenceSystem fis) => fis.Kind switch
    {
        FisKind.Mamdani when fis.IsClassifying => $"Mamdani MIMO, {fis.Outputs.Count} outputs yₗ ∈ [0, 1]",
        FisKind.Mamdani    => $"Mamdani, output {fis.Outputs[0].Name}",
        FisKind.Sugeno     => $"Takagi–Sugeno order {fis.SugenoOrder}, output {fis.Outputs[0].Name}",
        _                  => $"classifier, {fis.Classes.Count} classes",
    };

    private void ClearSystem(string message)
    {
        _fis = null;
        _solution = null;
        _inputColumns = null;
        HasSystem = false;
        EmptyMessage = message;
        Rules.Clear();
        Inputs.Clear();
        TermEditors.Clear();
        Variables = [];
        MfPlot = null;
        OutputPlot = null;
        ClassScores = [];
        CanEvaluate = false;
    }

    /// <summary>Shows a system: rule rows, sliders, operator pickers, MF editor, first evaluation.</summary>
    private void Load(FuzzyInferenceSystem fis)
    {
        _loading = true;
        _fis = fis;
        HasSystem = true;
        IsSugeno = fis.Kind == FisKind.Sugeno;
        IsClassifying = fis.IsClassifying;

        AndMethod             = fis.Settings.And;
        OrMethod              = fis.Settings.Or;
        ImplicationMethod     = fis.Settings.Implication;
        AggregationMethod     = fis.Settings.Aggregation;
        DefuzzificationMethod = fis.Settings.Defuzzification;

        Rules.Clear();
        for (int ruleIndex = 0; ruleIndex < fis.Rules.Count; ruleIndex++)
            Rules.Add(new RuleRowViewModel(fis, fis.Rules[ruleIndex], ruleIndex + 1, MfPlotting.BrushFor(ruleIndex), OnSystemEdited));

        Inputs.Clear();
        foreach (var variable in fis.Inputs)
            Inputs.Add(new InputSliderViewModel(variable.Name, variable.Min, variable.Max, (variable.Min + variable.Max) / 2, OnInputChanged));

        Variables = fis.Inputs.Select(variable => $"in · {variable.Name}")
            .Concat(fis.Kind == FisKind.Mamdani ? fis.Outputs.Select(variable => $"out · {variable.Name}") : [])
            .ToList();
        SelectedVariable = 0;

        OutputNames = fis.Kind == FisKind.Mamdani
            ? fis.Outputs.Select((variable, output) => fis.IsClassifying ? $"{variable.Name} · {fis.Classes[output]}" : variable.Name).ToList()
            : [];
        SelectedOutput = 0;
        ShowOutputPlot = fis.Kind == FisKind.Mamdani;
        _loading = false;

        // start from the typical point of the first cluster: a point every user recognises
        int startObject = _solution is { Centers.Length: > 0 } solution ? solution.Centers[0] : 0;
        ObjectNumberText = (startObject + 1).ToString(Invariant);
        if (!LoadObjectRow(startObject)) EvaluateCurrent();
        RebuildMfPlot();
        ScheduleEvaluation();
    }

    // ─── Live evaluation ─────────────────────────────────────────────────

    private void OnInputChanged()
    {
        if (_loading) return;
        ObjectInfo = "";
        EvaluateCurrent();
        RebuildMfPlot(editors: false);
    }

    private void OnSystemEdited()
    {
        if (_loading || _fis is null) return;
        EvaluateCurrent();
        ScheduleEvaluation();
    }

    private void EvaluateCurrent()
    {
        if (_fis is not { } fis || Inputs.Count != fis.Inputs.Count) return;
        var inputValues = Inputs.Select(input => input.Value).ToArray();
        InferenceResult result;
        try { result = fis.Evaluate(inputValues, includeOutputSets: fis.Kind == FisKind.Mamdani); }
        catch (InvalidOperationException ex) { ShowStatus(ex.Message, isError: true); return; }
        if (StatusIsError) ShowStatus("");

        for (int ruleIndex = 0; ruleIndex < Rules.Count; ruleIndex++)
        {
            Rules[ruleIndex].Firing = result.Firing[ruleIndex];
            Rules[ruleIndex].RuleOutputText = fis.Kind == FisKind.Sugeno
                ? $"y = {result.RuleOutputs[ruleIndex].ToString("0.###", Invariant)}" : "";
        }

        if (fis.IsClassifying)
        {
            double topScore = result.ClassScores.Where(score => !double.IsNaN(score)).DefaultIfEmpty(0).Max();
            var scoreNames = fis.Kind == FisKind.Classifier ? fis.Classes : OutputNames;
            ClassScores = result.ClassScores.Select((score, classIndex) => new ClassScoreViewModel(
                scoreNames[classIndex],
                double.IsNaN(score) ? 0 : score,
                double.IsNaN(score) ? "no rule" : score.ToString("0.000", Invariant),
                !double.IsNaN(score) && score > 0 && score >= topScore - fis.Settings.TieTolerance,
                MfPlotting.BrushFor(classIndex))).ToList();
            var tiedClasses = ClassScores.Select((score, classIndex) => (score, classIndex))
                .Where(entry => entry.score.IsWinner).Select(entry => fis.Classes[entry.classIndex]).Distinct();
            DecisionText = result.Ambiguous ? $"double-classified: {string.Join(", ", tiedClasses)}"
                : result.PredictedClass < 0 ? "rejected — no rule fired"
                : $"→ {fis.Classes[result.PredictedClass]}";
        }
        else
        {
            ClassScores = [];
            DecisionText = "";
        }

        CrispText = fis.Kind == FisKind.Classifier || fis.IsClassifying ? ""
            : double.IsNaN(result.Output) ? "no rule fired"
            : $"{fis.Outputs[0].Name} = {result.Output.ToString("0.####", Invariant)}";

        if (fis.Kind == FisKind.Mamdani && result.OutputGrids is { } grids && result.OutputMemberships is { } aggregatedSets)
        {
            int output = System.Math.Clamp(SelectedOutput, 0, grids.Length - 1);
            OutputPlot = BuildOutputPlot(fis, output, grids[output], aggregatedSets[output], result.Firing, result.Outputs[output]);
        }
    }

    partial void OnSelectedOutputChanged(int value) { if (!_loading) EvaluateCurrent(); }

    // ─── Object loader ───────────────────────────────────────────────────

    [RelayCommand]
    private void LoadObject()
    {
        if (int.TryParse(ObjectNumberText, NumberStyles.Integer, Invariant, out int objectNumber) && LoadObjectRow(objectNumber - 1)) return;
        ObjectInfo = $"Enter an object number 1 … {_workset?.RawData?.Rows ?? 0}.";
    }

    [RelayCommand]
    private void StepObject(string delta)
    {
        if (_workset?.RawData is not { } data) return;
        int.TryParse(ObjectNumberText, NumberStyles.Integer, Invariant, out int objectNumber);
        objectNumber = (objectNumber - 1 + int.Parse(delta, Invariant) + data.Rows) % data.Rows + 1;
        ObjectNumberText = objectNumber.ToString(Invariant);
        LoadObjectRow(objectNumber - 1);
    }

    /// <summary>Sets the sliders to one object of the workset and describes its true class / target.</summary>
    private bool LoadObjectRow(int objectIndex)
    {
        if (_workset?.RawData is not { } data || _inputColumns is not { } columns || objectIndex < 0 || objectIndex >= data.Rows) return false;
        for (int input = 0; input < Inputs.Count; input++) Inputs[input].SetSilently(data[objectIndex, columns[input]]);

        var info = new StringBuilder($"object {objectIndex + 1}");
        if (_workset.Labels is { } labels) info.Append(" · class ").Append(labels.ClassNames[labels.TrueLabels[objectIndex]]);
        if (_targetColumn >= 0)
            info.Append(" · ").Append(Features[_targetColumn]).Append(" = ")
                .Append(data[objectIndex, _targetColumn].ToString("0.####", Invariant));
        if (_solution is { } solution && solution.Centers.Contains(objectIndex))
            info.Append(" · typical point of C").Append(Array.IndexOf(solution.Centers, objectIndex) + 1);
        ObjectInfo = info.ToString();
        EvaluateCurrent();
        // show the aggregated set of the winning output (allotment system: one output per cluster)
        if (_fis is { Kind: FisKind.Mamdani, IsClassifying: true } fis && Inputs.Count == fis.Inputs.Count
            && fis.Evaluate(Inputs.Select(input => input.Value).ToArray()).PredictedClass is var winner and >= 0
            && winner != SelectedOutput)
            SelectedOutput = winner;
        RebuildMfPlot(editors: false);
        return true;
    }

    // ─── Operators ───────────────────────────────────────────────────────

    partial void OnAndMethodChanged(TNorm value)                       { if (_fis is { } fis) { fis.Settings.And = value; OnSystemEdited(); } }
    partial void OnOrMethodChanged(SNorm value)                        { if (_fis is { } fis) { fis.Settings.Or = value; OnSystemEdited(); } }
    partial void OnImplicationMethodChanged(Implication value)         { if (_fis is { } fis) { fis.Settings.Implication = value; OnSystemEdited(); } }
    partial void OnAggregationMethodChanged(SNorm value)               { if (_fis is { } fis) { fis.Settings.Aggregation = value; OnSystemEdited(); } }
    partial void OnDefuzzificationMethodChanged(Defuzzification value) { if (_fis is { } fis) { fis.Settings.Defuzzification = value; OnSystemEdited(); } }

    [RelayCommand]
    private void RefitConsequents()
    {
        if (_fis is not { Kind: FisKind.Sugeno } fis || _workset?.RawData is not { } data || _inputColumns is not { } columns || _targetColumn < 0) return;
        var targets = Enumerable.Range(0, data.Rows).Select(row => data[row, _targetColumn]).ToArray();
        ConsequentFitter.FitSugeno(fis, data.SelectColumns(columns), targets);
        foreach (var row in Rules) row.Refresh();
        OnSystemEdited();
        ShowStatus("Consequents refitted by local weighted least squares.");
    }

    // ─── Membership-function editor ──────────────────────────────────────

    partial void OnSelectedVariableChanged(int value) { if (!_loading) RebuildMfPlot(); }

    private FuzzyVariable? SelectedFuzzyVariable(out bool isInput, out int index)
    {
        isInput = true;
        index = SelectedVariable;
        if (_fis is not { } fis || SelectedVariable < 0) return null;
        if (SelectedVariable < fis.Inputs.Count) return fis.Inputs[SelectedVariable];
        isInput = false;
        index = SelectedVariable - fis.Inputs.Count;
        return index < fis.Outputs.Count ? fis.Outputs[index] : null;
    }

    /// <summary>Moves a break point to <paramref name="position"/> (drag on the plot).</summary>
    public void DragHandle(MfHandle handle, double position)
    {
        if (SelectedFuzzyVariable(out _, out _) is not { } variable || handle.Term >= variable.Terms.Count) return;
        double value = MfPlotting.DraggedValue(variable.Terms[handle.Term].Mf, handle, position);
        SetParameter(handle.Term, handle.Parameter, value, rebuildEditors: false);
    }

    /// <summary>Ends a drag: refreshes the numeric editors once.</summary>
    public void EndDrag() => RebuildTermEditors();

    private void SetParameter(int termIndex, int parameterIndex, double value, bool rebuildEditors = true)
    {
        if (SelectedFuzzyVariable(out _, out _) is not { } variable) return;
        var term = variable.Terms[termIndex];
        term.Mf = term.Mf.WithParameter(parameterIndex, value);
        if (rebuildEditors) RebuildTermEditors();
        else if (termIndex < TermEditors.Count)
        {
            var parameters = term.Mf.Parameters;
            foreach (var editor in TermEditors[termIndex].Parameters) editor.Value = parameters[editor.Index];
        }
        foreach (var row in Rules) row.Refresh();
        RebuildMfPlot(editors: false);
        OnSystemEdited();
    }

    private void RebuildTermEditors()
    {
        TermEditors.Clear();
        if (SelectedFuzzyVariable(out _, out _) is not { } variable) return;
        for (int termIndex = 0; termIndex < variable.Terms.Count; termIndex++)
        {
            int term = termIndex;
            var mf = variable.Terms[term].Mf;
            var editor = new TermEditorViewModel
            {
                Name = variable.Terms[term].Name, Shape = mf.Shape.ToString().ToLowerInvariant(), Accent = MfPlotting.BrushFor(term),
            };
            for (int parameter = 0; parameter < mf.Parameters.Count; parameter++)
                editor.Parameters.Add(new MfParameterViewModel($"{variable.Name} {variable.Terms[term].Name}", parameter,
                    mf.ParameterNames[parameter], mf.Parameters[parameter],
                    (index, value) => SetParameter(term, index, value)));
            TermEditors.Add(editor);
        }
    }

    private void RebuildMfPlot(bool editors = true)
    {
        if (SelectedFuzzyVariable(out bool isInput, out int index) is not { } variable) { MfPlot = null; Handles = []; return; }
        if (editors) RebuildTermEditors();

        double lower = variable.Min, upper = variable.Max;
        foreach (var term in variable.Terms)
        {
            lower = System.Math.Min(lower, term.Mf.Support.Min);
            upper = System.Math.Max(upper, term.Mf.Support.Max);
        }
        if (upper - lower < 1e-9) upper = lower + 1;
        double padding = (upper - lower) * 0.03;

        var model = new PlotModel { Background = DesignPalette.PlotBackground, IsLegendVisible = false };
        var handles = new List<MfHandle>();
        var handleSeries = new ScatterSeries
        {
            MarkerType = MarkerType.Square, MarkerSize = 4,
            MarkerFill = OxyColors.White, MarkerStroke = DesignPalette.AxisText, MarkerStrokeThickness = 1.2,
        };

        for (int termIndex = 0; termIndex < variable.Terms.Count; termIndex++)
        {
            var mf = variable.Terms[termIndex].Mf;
            var color = MfPlotting.ColorFor(termIndex);
            var curve = new LineSeries { Color = color, StrokeThickness = 2, Title = variable.Terms[termIndex].Name };
            foreach (double position in MfPlotting.SamplePoints(mf, lower - padding, upper + padding))
                curve.Points.Add(new DataPoint(position, mf.Evaluate(position)));
            model.Series.Add(curve);

            foreach (var handle in MfPlotting.HandlesOf(termIndex, mf))
            {
                handles.Add(handle);
                handleSeries.Points.Add(new ScatterPoint(handle.X, handle.Y));
            }
            model.Annotations.Add(new TextAnnotation
            {
                Text = variable.Terms[termIndex].Name, TextPosition = new DataPoint(mf.CoreCenter, 1.0),
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom, Stroke = OxyColors.Transparent,
                TextColor = color, FontSize = 10,
            });
        }
        model.Series.Add(handleSeries);

        if (isInput && index < Inputs.Count)
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical, X = Inputs[index].Value,
                Color = DesignPalette.Coral, StrokeThickness = 1.5, LineStyle = LineStyle.Dash,
                Text = Inputs[index].Value.ToString("0.###", Invariant), TextColor = DesignPalette.Coral,
            });

        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Bottom, variable.Name, lower - padding, upper + padding));
        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Left, "", -0.03, 1.12));   // μ ∈ [0, 1] is evident; a title collides with the ticks

        Handles = handles;
        MfPlot = model;
    }

    private PlotModel BuildOutputPlot(FuzzyInferenceSystem fis, int outputIndex, double[] grid, double[] aggregated, double[] firing, double crisp)
    {
        var output = fis.Outputs[outputIndex];
        var model = new PlotModel { Background = DesignPalette.PlotBackground, IsLegendVisible = false };

        // consequent terms of the fired rules, thin and pale, under the aggregated set
        for (int ruleIndex = 0; ruleIndex < fis.Rules.Count; ruleIndex++)
        {
            int term = fis.Rules[ruleIndex].Consequent[outputIndex];
            if (term < 0 || firing[ruleIndex] <= 0) continue;
            var mf = output.Terms[term].Mf;
            var consequentCurve = new LineSeries { Color = OxyColor.FromAColor(0x90, MfPlotting.ColorFor(ruleIndex)), StrokeThickness = 1, LineStyle = LineStyle.Dot };
            foreach (double position in MfPlotting.SamplePoints(mf, output.Min, output.Max))
                consequentCurve.Points.Add(new DataPoint(position, mf.Evaluate(position)));
            model.Series.Add(consequentCurve);
        }

        var area = new AreaSeries
        {
            Color = DesignPalette.Blue400, Fill = OxyColor.FromAColor(0x60, DesignPalette.Blue100), StrokeThickness = 1.5,
        };
        for (int point = 0; point < grid.Length; point++)
        {
            area.Points.Add(new DataPoint(grid[point], aggregated[point]));
            area.Points2.Add(new DataPoint(grid[point], 0));
        }
        model.Series.Add(area);

        if (!double.IsNaN(crisp))
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical, X = crisp, Color = DesignPalette.Coral, StrokeThickness = 2,
                Text = $"{fis.Settings.Defuzzification.ToString().ToLowerInvariant()} = {crisp.ToString("0.###", Invariant)}",
                TextColor = DesignPalette.Coral,
            });
        else
            model.Annotations.Add(new TextAnnotation
            {
                Text = "empty output set — no fired rule mentions this output",
                TextPosition = new DataPoint((output.Min + output.Max) / 2, 0.5), Stroke = OxyColors.Transparent,
                TextColor = DesignPalette.AxisText, FontSize = 11,
            });

        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Bottom, output.Name, output.Min, output.Max));
        model.Axes.Add(MfPlotting.FixedAxis(AxisPosition.Left, "", -0.03, 1.08));   // no title: the small plot has no room for it
        return model;
    }

    // ─── Workset evaluation ──────────────────────────────────────────────

    private void ScheduleEvaluation()
    {
        _evaluateTimer.Stop();
        _evaluateTimer.Start();
    }

    private void EvaluateWorkset()
    {
        if (_fis is not { } fis || _workset?.RawData is not { } data || _inputColumns is not { } columns)
        {
            CanEvaluate = false;
            EvaluationNote = _fis is null ? "" : "The system's inputs do not match the columns of the active workset.";
            return;
        }
        CanEvaluate = true;
        var inputs = data.SelectColumns(columns);

        try
        {
            if (fis.IsClassifying)
            {
                if (TruthLabels(data) is not { } truth)
                {
                    ShowConfusion = false;
                    ShowRegression = false;
                    EvaluationNote = "No class labels: nothing to compare the decisions with.";
                    return;
                }
                var report = FisEvaluator.Classify(fis, inputs, truth.Labels);
                EvaluationTitle = truth.Title;
                AccuracyText = report.Accuracy.ToString("P1", Invariant);
                CountsText = $"{report.Correct} correct · {report.Misclassified} misclassified · {report.Ambiguous} double-classified · "
                           + $"{report.Rejected} rejected  (n = {report.Total})";
                int classCount = report.Classes.Count;
                ConfusionHeader = ["true \\ predicted", .. report.Classes, "tie", "none"];
                ConfusionRows = Enumerable.Range(0, classCount)
                    .Where(trueClass => Enumerable.Range(0, classCount + 2).Any(column => report.Confusion[trueClass, column] > 0))
                    .Select(trueClass => new ConfusionRowViewModel(report.Classes[trueClass],
                        Enumerable.Range(0, classCount + 2).Select(column => new ConfusionCell(
                            report.Confusion[trueClass, column].ToString(Invariant),
                            column == trueClass,
                            report.Confusion[trueClass, column] == 0)).ToList()))
                    .ToList();
                ShowConfusion = true;
                ShowRegression = false;
                EvaluationNote = "Double-classified: tied best score; rejected: no rule fired. Both count as errors.";
            }
            else if (_targetColumn >= 0)
            {
                var targets = Enumerable.Range(0, data.Rows).Select(row => data[row, _targetColumn]).ToArray();
                var report = FisEvaluator.Regress(fis, inputs, targets);
                EvaluationTitle = $"Prediction of {Features[_targetColumn]} on all {data.Rows} objects";
                RmseText = double.IsNaN(report.Rmse) ? "—" : report.Rmse.ToString("0.####", Invariant);
                MaeText  = double.IsNaN(report.Mae)  ? "—" : report.Mae.ToString("0.####", Invariant);
                CountsText = $"{report.Evaluated} evaluated · {report.NoFire} with no rule fired · "
                           + $"σ({Features[_targetColumn]}) = {StandardDeviation(targets).ToString("0.####", Invariant)}";
                ShowConfusion = false;
                ShowRegression = true;
                EvaluationNote = "RMSE and MAE are over the objects on which some rule fired; compare with σ of the target.";
            }
            else
            {
                ShowConfusion = false;
                ShowRegression = false;
                EvaluationNote = "Imported system without a target column: only the live evaluation is available.";
            }
        }
        catch (InvalidOperationException ex)
        {
            EvaluationNote = ex.Message;
        }
    }

    /// <summary>
    /// True class name of every object: the dataset labels when present; otherwise the cluster
    /// of largest membership in the solution the rules were generated from.
    /// </summary>
    private (string[] Labels, string Title)? TruthLabels(RawDataMatrix data)
    {
        if (_workset?.Labels is { } labels)
            return (labels.TrueLabels.Select(label => labels.ClassNames[label]).ToArray(),
                    $"Classification of all {data.Rows} objects against the class labels");
        if (_solution is not { } solution || solution.Membership.GetLength(0) != data.Rows) return null;
        var truth = new string[data.Rows];
        for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
        {
            int bestCluster = 0;
            for (int cluster = 1; cluster < solution.ClusterCount; cluster++)
                if (solution.Membership[objectIndex, cluster] > solution.Membership[objectIndex, bestCluster]) bestCluster = cluster;
            truth[objectIndex] = solution.Membership[objectIndex, bestCluster] > 0 ? $"C{bestCluster + 1}" : "unassigned";
        }
        return (truth, $"Agreement with the clustering (no class labels) on all {data.Rows} objects");
    }

    private static double StandardDeviation(double[] values)
    {
        double mean = values.Average();
        return System.Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Length);
    }

    // ─── Import / export ─────────────────────────────────────────────────

    [RelayCommand]
    private void ExportJson() => Export("JSON (*.json)|*.json", ".json", FisJson.Serialize);

    [RelayCommand]
    private void ExportFis() => Export("MATLAB FIS (*.fis)|*.fis", ".fis", FisFileWriter.Write);

    private void Export(string filter, string extension, Func<FuzzyInferenceSystem, string> write)
    {
        if (_fis is not { } fis) return;
        string text;
        try { text = write(fis); }
        catch (NotSupportedException ex)
        {
            ShowStatus(ex.Message, isError: true);
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter, FileName = $"{fis.Name}-{fis.Kind.ToString().ToLowerInvariant()}{extension}", Title = "Export rule base",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            ShowStatus($"Rule base exported → {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    [RelayCommand]
    private void ImportJson()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json|All files (*.*)|*.*", Title = "Import rule base" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var fis = FisJson.Deserialize(File.ReadAllText(dialog.FileName));
            // map inputs (and a regression target) to workset columns by name
            var featureList = Features.ToList();
            var columns = fis.Inputs.Select(variable => featureList.IndexOf(variable.Name)).ToArray();
            _inputColumns = columns.All(column => column >= 0) ? columns : null;
            _targetColumn = fis.Kind != FisKind.Classifier && !fis.IsClassifying && fis.Outputs.Count == 1
                ? featureList.IndexOf(fis.Outputs[0].Name) : -1;
            _solution = null;
            Load(fis);
            Summary = $"{fis.Rules.Count} rules imported from {Path.GetFileName(dialog.FileName)} · {fis.Inputs.Count} inputs · {DescribeKind(fis)}";
            ShowStatus(_inputColumns is null ? "Inputs do not match the workset columns: workset evaluation is off." : "",
                       isError: _inputColumns is null);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or IOException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, "Import error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>Message under the toolbar: problems in coral, confirmations in the success colour.</summary>
    private void ShowStatus(string text, bool isError = false)
    {
        StatusText = text;
        StatusIsError = isError;
    }

    private static readonly HashSet<string> LabelColumnNames =
        new(StringComparer.OrdinalIgnoreCase) { "class", "label", "labels", "type", "species", "variety", "target", "category" };

    /// <summary>
    /// Feature names of the workset's raw-data columns from the CSV header (the label column
    /// removed, as <see cref="LabeledDataset"/> does); x1 … xm when there is no usable header.
    /// </summary>
    internal static IReadOnlyList<string> FeatureNames(Workset workset)
    {
        int columnCount = workset.RawData?.Cols ?? 0;
        var fallback = Enumerable.Range(1, columnCount).Select(column => $"x{column}").ToList();
        if (workset.SourcePath is not { } path || !File.Exists(path) || columnCount == 0) return fallback;
        try
        {
            var lines = File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Take(2).ToArray();
            if (lines.Length < 2) return fallback;
            var header = lines[0].Split(',').Select(cell => cell.Trim().Trim('﻿', '"')).ToArray();
            if (double.TryParse(header[0], NumberStyles.Float, Invariant, out _)) return fallback;
            var firstRow = lines[1].Split(',');
            var numericColumns = Enumerable.Range(0, System.Math.Min(header.Length, firstRow.Length))
                .Where(column => double.TryParse(firstRow[column].Trim(), NumberStyles.Float, Invariant, out _))
                .ToList();
            if (numericColumns.Count == columnCount + 1 && workset.Labels is not null)   // integer label column named class/label/…
                numericColumns.RemoveAll(column => LabelColumnNames.Contains(header[column]));
            return numericColumns.Count == columnCount ? numericColumns.Select(column => header[column]).ToList() : fallback;
        }
        catch (IOException) { return fallback; }
    }
}
