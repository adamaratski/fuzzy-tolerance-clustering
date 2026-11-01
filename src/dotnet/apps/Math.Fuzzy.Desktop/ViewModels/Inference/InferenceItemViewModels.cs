using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Inference.Systems;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;

namespace MathFuzzy.Desktop.ViewModels.Inference;

/// <summary>One "x is A" clause of a rule, edited with a term picker (first option = clause off).</summary>
public sealed partial class ClauseViewModel : ObservableObject
{
    private readonly Action<int> _applyTerm;

    public string Variable { get; }
    /// <summary>"any" / "—" first, then the variable's terms.</summary>
    public IReadOnlyList<string> Options { get; }

    [ObservableProperty] private int _selectedIndex;

    public ClauseViewModel(string variable, IReadOnlyList<string> termNames, string offText, int termIndex, Action<int> applyTerm)
    {
        Variable = variable;
        Options = [offText, .. termNames];
        _selectedIndex = termIndex + 1;
        _applyTerm = applyTerm;
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0) _applyTerm(value - 1);
    }

    public string AutomationName => $"{Variable} term";
}

/// <summary>One row of the rule list: on/off, antecedent and consequent pickers, weight, live firing.</summary>
public sealed partial class RuleRowViewModel : ObservableObject
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly FuzzyInferenceSystem _fis;
    private readonly Action _changed;

    public FuzzyRule Rule { get; }
    public int Number { get; }
    public string Header => $"R{Number}" + (Rule.Label is { } label ? $" · {label}" : "");
    public Brush Accent { get; }

    public ObservableCollection<ClauseViewModel> Antecedent { get; } = [];
    public ObservableCollection<ClauseViewModel> Consequent { get; } = [];

    public bool IsClassifier => _fis.Kind == FisKind.Classifier;
    public bool IsSugeno     => _fis.Kind == FisKind.Sugeno;
    public bool IsMamdani    => _fis.Kind == FisKind.Mamdani;
    public IReadOnlyList<string> ClassOptions => _fis.Classes;

    [ObservableProperty] private double _firing;
    [ObservableProperty] private string _ruleOutputText = "";

    public RuleRowViewModel(FuzzyInferenceSystem fis, FuzzyRule rule, int number, Brush accent, Action changed)
    {
        _fis = fis;
        Rule = rule;
        Number = number;
        Accent = accent;
        _changed = changed;

        for (int inputIndex = 0; inputIndex < fis.Inputs.Count; inputIndex++)
        {
            int input = inputIndex;
            var variable = fis.Inputs[input];
            Antecedent.Add(new ClauseViewModel(variable.Name, variable.Terms.Select(term => term.Name).ToList(),
                "any", rule.Antecedent[input], term => { Rule.Antecedent[input] = term; Changed(); }));
        }
        if (fis.Kind == FisKind.Mamdani)
            for (int outputIndex = 0; outputIndex < fis.Outputs.Count; outputIndex++)
            {
                int output = outputIndex;
                var variable = fis.Outputs[output];
                Consequent.Add(new ClauseViewModel(variable.Name, variable.Terms.Select(term => term.Name).ToList(),
                    "—", rule.Consequent[output], term => { Rule.Consequent[output] = term; Changed(); }));
            }
    }

    public bool IsEnabled
    {
        get => Rule.Enabled;
        set { if (Rule.Enabled == value) return; Rule.Enabled = value; OnPropertyChanged(); Changed(); }
    }

    public string WeightText
    {
        get => Rule.Weight.ToString("0.###", Invariant);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float, Invariant, out var weight)) return;
            Rule.Weight = System.Math.Clamp(weight, 0, 1);
            OnPropertyChanged();
            Changed();
        }
    }

    public int ClassIndex
    {
        get => Rule.Class;
        set { if (value < 0 || Rule.Class == value) return; Rule.Class = value; OnPropertyChanged(); Changed(); }
    }

    /// <summary>Sugeno coefficients p₀ p₁ … pₘ, space-separated.</summary>
    public string CoefficientsText
    {
        get => string.Join(" ", Rule.Coefficients.Select(coefficient => coefficient.ToString("0.####", Invariant)));
        set
        {
            var parts = (value ?? "").Split([' ', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var coefficients = new double[parts.Length];
            for (int index = 0; index < parts.Length; index++)
                if (!double.TryParse(parts[index], NumberStyles.Float, Invariant, out coefficients[index])) return;
            if (coefficients.Length != Rule.Coefficients.Length) return;
            Rule.Coefficients = coefficients;
            OnPropertyChanged();
            Changed();
        }
    }

    public string Text => _fis.Describe(Rule);
    public string FiringText => Firing.ToString("0.000", Invariant);
    public string AutomationName => $"Rule {Number}";

    partial void OnFiringChanged(double value) => OnPropertyChanged(nameof(FiringText));

    /// <summary>Re-reads values changed outside the row (e.g. a consequent refit).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(CoefficientsText));
        OnPropertyChanged(nameof(Text));
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(Text));
        _changed();
    }
}

/// <summary>Slider + value box for one input of the system.</summary>
public sealed partial class InputSliderViewModel : ObservableObject
{
    private readonly Action _changed;

    public string Name { get; }
    public double Min { get; }
    public double Max { get; }
    public double SmallChange => (Max - Min) / 200;

    [ObservableProperty] private double _value;

    public InputSliderViewModel(string name, double min, double max, double initialValue, Action changed)
    {
        Name = name;
        Min = min;
        Max = max;
        _value = initialValue;
        _changed = changed;
    }

    public string ValueText
    {
        get => Value.ToString("0.####", CultureInfo.InvariantCulture);
        set { if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) Value = parsed; }
    }

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(ValueText));
        _changed();
    }

    /// <summary>Sets the value without triggering a re-evaluation per input (bulk load of an object).</summary>
    public void SetSilently(double newValue)
    {
#pragma warning disable MVVMTK0034 // direct field write is the point: one evaluation after all inputs are set
        _value = newValue;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ValueText));
    }

    public string AutomationName => $"Input {Name}";
}

/// <summary>One editable parameter of a membership function.</summary>
public sealed partial class MfParameterViewModel : ObservableObject
{
    private readonly Action<int, double> _apply;
    public int Index { get; }
    public string Name { get; }
    public string AutomationName { get; }
    [ObservableProperty] private double _value;

    public MfParameterViewModel(string termName, int index, string name, double value, Action<int, double> apply)
    {
        Index = index;
        Name = name;
        _value = value;
        _apply = apply;
        AutomationName = $"{termName} parameter {name}";
    }

    public string ValueText
    {
        get => Value.ToString("0.####", CultureInfo.InvariantCulture);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed)) return;
            _apply(Index, parsed);
        }
    }

    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(ValueText));
}

/// <summary>Parameter editor of one term of the selected variable.</summary>
public sealed class TermEditorViewModel
{
    public required string Name { get; init; }
    public required string Shape { get; init; }
    public required Brush Accent { get; init; }
    public ObservableCollection<MfParameterViewModel> Parameters { get; } = [];
}

/// <summary>Bar of a class score (classifier firing or allotment output).</summary>
public sealed record ClassScoreViewModel(string Name, double Score, string ScoreText, bool IsWinner, Brush Accent);

/// <summary>Row of the confusion matrix: true class, then counts per predicted class / ambiguous / rejected.</summary>
public sealed record ConfusionRowViewModel(string Label, IReadOnlyList<ConfusionCell> Cells);
public sealed record ConfusionCell(string Text, bool IsDiagonal, bool IsZero);
