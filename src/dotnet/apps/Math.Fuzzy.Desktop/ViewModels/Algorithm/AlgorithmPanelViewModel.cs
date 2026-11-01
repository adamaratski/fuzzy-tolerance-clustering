using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.Services;

namespace MathFuzzy.Desktop.ViewModels.Algorithm;

public sealed partial class AlgorithmPanelViewModel : ObservableObject
{
    [ObservableProperty] private string _selectedAlgorithm = "D-AFC-TC";
    [ObservableProperty] private DistanceInfo _selectedDistance = DistanceParameterFactory.Items[0];
    [ObservableProperty] private string _selectedNormalization = "MinMax";
    [ObservableProperty] private string _selectedTagaOperator = "Mean";
    [ObservableProperty] private string _selectedCriterion = "F₁";
    [ObservableProperty] private string _clusterCountText = "";
    [ObservableProperty] private string _userAlphaText = "0.5";
    /// <summary>Accuracy threshold ε (empty = full precision), e.g. 0.0001.</summary>
    [ObservableProperty] private string _accuracyText = "";
    /// <summary>D-PAFC: admissible number of objects in intersection areas (0 = fully separated).</summary>
    [ObservableProperty] private string _intersectionsText = "0";
    [ObservableProperty] private IDistanceParameterViewModel _distanceParameters = DistanceParameterFactory.Create(DistanceParameterFactory.Items[0]);
    [ObservableProperty] private int _minClusterPower = 1;
    [ObservableProperty] private string _selectedAlphaSelector = "All levels";
    [ObservableProperty] private int _maxResults = 200;

    public IReadOnlyList<string> Algorithms { get; } =
    [
        "D-AFC", "D-PAFC",
        "D-AFC-TC", "D-AFC-TC(α)", "D-PAFC-TC",
        "D-AFC-TAGA", "D-PAFC-TAGA",
        "H-AFC-TC",
        "D-PAFC-DR", "D-PAFC-TC-DR", "D-PAFC-TAGA-DR"
    ];

    public IReadOnlyList<DistanceInfo> Distances      { get; } = DistanceParameterFactory.Items;
    public IReadOnlyList<string> Normalizations { get; } = ["MinMax", "Max", "None"];
    public IReadOnlyList<string> Criteria        { get; } = ["F₁", "F₂"];
    public IReadOnlyList<string> TagaOperators   { get; } = ["Maximum", "Minimum", "Mean", "Median", "UpperMedian", "LowerMedian"];
    /// <summary>D-AFC level search: every distinct α of T, or the single level α* entered by the user.</summary>
    public IReadOnlyList<string> AlphaSelectors  { get; } = ["All levels", "Manual"];

    public string CriterionFormula => SelectedCriterion switch
    {
        "F₂" or "F2" => "Σᵢ[Σⱼ∈Cᵢ μ(cᵢ,xⱼ)/|Cᵢ|] / c",
        _            => "Σᵢ[Σⱼ∈Cᵢ μ(cᵢ,xⱼ)/|Cᵢ|] − α·c"
    };

    /// <summary>Relational variants: D-AFC(c) for a given c, D-PAFC searches the minimal c.</summary>
    public IReadOnlyList<string> RelationalVariants { get; } = ["D-AFC", "D-PAFC"];

    public bool IsRelational => SelectedAlgorithm is "D-AFC" or "D-PAFC";

    public string VariantDescription => SelectedAlgorithm switch
    {
        "D-PAFC" => "c is searched upwards: the first α-level and the least c with at most w shared objects.",
        _        => "c is given; the best allotment among c clusters over the α-levels."
    };

    public string ChipText     => SelectedAlgorithm;
    public string CategoryText => SelectedAlgorithm switch
    {
        "D-AFC" or "D-PAFC"                                                           => "Relational",
        "D-AFC-TC" or "D-AFC-TC(α)" or "D-PAFC-TC" or "D-AFC-TAGA" or "D-PAFC-TAGA" => "Prototype-based",
        "H-AFC-TC"                                                                    => "Hierarchical",
        _                                                                             => "Dim. reduction"
    };

    public bool ShowClusterCount  => SelectedAlgorithm is "D-AFC";
    public bool ShowAlphaSelector => SelectedAlgorithm is "D-AFC";
    public bool ShowIntersections => SelectedAlgorithm is "D-PAFC";

    /// <summary>Validation message for the current parameters, or null if they are valid.</summary>
    public string? ValidationError
    {
        get
        {
            if (ShowClusterCount && (!int.TryParse(ClusterCountText, out int c) || c < 2))
                return "D-AFC needs the number of clusters c (an integer ≥ 2). To search c, choose D-PAFC.";
            if (ShowIntersections && (!int.TryParse(IntersectionsText, out int w) || w < 0))
                return "Intersections w must be an integer ≥ 0 (0 = fully separated clusters).";
            if (ShowUserAlpha &&
                (!double.TryParse(UserAlphaText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double a) || a <= 0 || a >= 1))
                return "α* must be a number in (0, 1).";
            if (!string.IsNullOrWhiteSpace(AccuracyText) &&
                (!double.TryParse(AccuracyText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double e) || e <= 0 || e >= 1))
                return "Accuracy ε must be empty or a number in (0, 1), e.g. 0.0001.";
            return null;
        }
    }
    public bool ShowTagaOperator => SelectedAlgorithm.Contains("TAGA");
    public bool ShowUserAlpha    =>
        SelectedAlgorithm is "D-AFC-TC(α)" || (ShowAlphaSelector && SelectedAlphaSelector == "Manual");

    partial void OnSelectedAlgorithmChanged(string value)
    {
        OnPropertyChanged(nameof(ShowClusterCount));
        OnPropertyChanged(nameof(ShowAlphaSelector));
        OnPropertyChanged(nameof(ShowIntersections));
        OnPropertyChanged(nameof(IsRelational));
        OnPropertyChanged(nameof(VariantDescription));
        OnPropertyChanged(nameof(ShowTagaOperator));
        OnPropertyChanged(nameof(ShowUserAlpha));
        OnPropertyChanged(nameof(ChipText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(ValidationError));
    }

    partial void OnSelectedAlphaSelectorChanged(string value)
    {
        OnPropertyChanged(nameof(ShowUserAlpha));
        OnPropertyChanged(nameof(ValidationError));
    }

    partial void OnClusterCountTextChanged(string value) => OnPropertyChanged(nameof(ValidationError));

    partial void OnIntersectionsTextChanged(string value) => OnPropertyChanged(nameof(ValidationError));
    partial void OnUserAlphaTextChanged(string value)    => OnPropertyChanged(nameof(ValidationError));
    partial void OnAccuracyTextChanged(string value)     => OnPropertyChanged(nameof(ValidationError));

    partial void OnSelectedCriterionChanged(string value)
    {
        OnPropertyChanged(nameof(CriterionFormula));
    }

    partial void OnSelectedDistanceChanged(DistanceInfo value)
    {
        DistanceParameters = DistanceParameterFactory.Create(value);
    }

    public AlgorithmRunOptions BuildOptions()
    {
        int? clusterCount = int.TryParse(ClusterCountText, out int parsed) ? parsed : null;
        double? userAlpha = double.TryParse(UserAlphaText,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double ai) ? ai : null;

        double? accuracy = double.TryParse(AccuracyText,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double eps) && eps > 0 && eps < 1 ? eps : null;

        return new AlgorithmRunOptions(
            Algorithm:     AlgorithmKey(SelectedAlgorithm),
            ClusterCount:  clusterCount,
            Distance:      DistanceParameters.Build(),
            Normalization: SelectedNormalization.ToLower(),
            TagaOperator:  ShowTagaOperator ? SelectedTagaOperator : null,
            UserAlpha:     ShowUserAlpha ? userAlpha : null,
            Criterion:     SelectedCriterion is "F₂" or "F2" ? "f2" : "f1",
            MaxResults:    MaxResults,
            Accuracy:      accuracy,
            MaxIntersections: ShowIntersections && int.TryParse(IntersectionsText, out int w) ? System.Math.Max(0, w) : 0
        );
    }

    private static string AlgorithmKey(string displayName) => displayName switch
    {
        "D-AFC"          => "dafc",
        "D-PAFC"         => "dpafc",
        "D-AFC-TC"       => "dafc-tc",
        "D-AFC-TC(α)"    => "dafc-tc-alpha",
        "D-PAFC-TC"      => "dpafc-tc",
        "D-AFC-TAGA"     => "dafc-taga",
        "D-PAFC-TAGA"    => "dpafc-taga",
        "H-AFC-TC"       => "hafc-tc",
        "D-PAFC-DR"      => "dpafc-dr",
        "D-PAFC-TC-DR"   => "dpafc-tc-dr",
        "D-PAFC-TAGA-DR" => "dpafc-taga-dr",
        _                => displayName.ToLowerInvariant()
    };
}
