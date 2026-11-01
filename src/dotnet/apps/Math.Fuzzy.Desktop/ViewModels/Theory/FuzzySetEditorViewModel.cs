using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.ViewModels.Inference;
using MathFuzzy.Inference.MembershipFunctions;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace MathFuzzy.Desktop.ViewModels.Theory;

/// <summary>
/// One fuzzy set on the real line: shape picker plus parameter boxes. Each shape keeps its
/// own last parameters, so switching back and forth does not lose edits.
/// </summary>
public sealed partial class FuzzySetEditorViewModel : ObservableObject
{
    public static IReadOnlyList<string> ShapeNames { get; } = ["Triangular", "Trapezoidal", "Gaussian"];

    private readonly MembershipFunction[] _functionPerShape;
    private readonly Action _changed;

    public string Name { get; }
    public Brush Accent { get; }
    public ObservableCollection<MfParameterViewModel> Parameters { get; } = [];

    [ObservableProperty] private int _selectedShape;

    public FuzzySetEditorViewModel(string name, Brush accent, MembershipFunction triangle, MembershipFunction trapezoid,
                                   MembershipFunction gaussian, int initialShape, Action changed)
    {
        Name = name;
        Accent = accent;
        _functionPerShape = [triangle, trapezoid, gaussian];
        _selectedShape = initialShape;
        _changed = changed;
        RebuildParameters();
    }

    public MembershipFunction Mf => _functionPerShape[SelectedShape];

    public string AutomationName => $"Set {Name} shape";

    public string Formula => Mf.Shape switch
    {
        MfShape.Triangular  => "μ(x) = max(0, min((x − a)/(b − a), (c − x)/(c − b)))",
        MfShape.Trapezoidal => "μ(x) = max(0, min((x − a)/(b − a), 1, (d − x)/(d − c)))",
        _                   => "μ(x) = exp(−(x − c)² / 2σ²)",
    };

    partial void OnSelectedShapeChanged(int value)
    {
        RebuildParameters();
        OnPropertyChanged(nameof(Formula));
        _changed();
    }

    /// <summary>Sets one parameter; the shape repairs its order, so every box is refreshed.</summary>
    public void SetParameter(int parameterIndex, double value)
    {
        _functionPerShape[SelectedShape] = Mf.WithParameter(parameterIndex, value);
        var parameters = Mf.Parameters;
        foreach (var editor in Parameters) editor.Value = parameters[editor.Index];
        _changed();
    }

    private void RebuildParameters()
    {
        Parameters.Clear();
        var mf = Mf;
        for (int parameter = 0; parameter < mf.Parameters.Count; parameter++)
            Parameters.Add(new MfParameterViewModel($"Set {Name}", parameter, mf.ParameterNames[parameter],
                mf.Parameters[parameter], SetParameter));
    }
}
