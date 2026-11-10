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

    // One fixed list and one combo box per category: a combo box whose items never
    // change keeps its selection when the category changes.
    public IReadOnlyList<string> RelationalVariants   { get; } = ["D-AFC", "D-PAFC"];
    public IReadOnlyList<string> PrototypeVariants    { get; } = ["D-AFC-TC", "D-AFC-TC(α)", "D-PAFC-TC", "D-AFC-TAGA", "D-PAFC-TAGA"];
    public IReadOnlyList<string> HierarchicalVariants { get; } = ["H-AFC-TC"];
    public IReadOnlyList<string> ReductionVariants    { get; } = ["D-PAFC-DR", "D-PAFC-TC-DR", "D-PAFC-TAGA-DR"];

    public bool IsRelational => CategoryText == "Relational";
    public bool IsPrototype  => CategoryText == "Prototype-based";
    public bool IsReduction  => CategoryText == "Dim. reduction";

    /// <summary>The algorithms of the selected category; the chip shows the selected one.</summary>
    public IReadOnlyList<string> Variants => CategoryText switch
    {
        "Relational"      => RelationalVariants,
        "Prototype-based" => PrototypeVariants,
        "Hierarchical"    => HierarchicalVariants,
        _                 => ReductionVariants
    };

    public bool ShowVariants => Variants.Count > 1;

    /// <summary>
    /// What the variant combo boxes bind to. A hidden combo box of another category may
    /// push null or an algorithm of its own list; only values of the current category count.
    /// </summary>
    public string SelectedVariant
    {
        get => SelectedAlgorithm;
        set { if (value is not null && Variants.Contains(value)) SelectedAlgorithm = value; }
    }

    public string VariantDescription => SelectedAlgorithm switch
    {
        "D-AFC"          => "c is given; the best allotment among c clusters over the α-levels of T.",
        "D-PAFC"         => "c is searched upwards: the first α-level and the least c with at most w shared objects.",
        "D-AFC-TC"       => "Transitive closure T̃; the level is chosen by the leap heuristic, c by the classes of that level.",
        "D-AFC-TC(α)"    => "As D-AFC-TC, but the leap is searched only at levels not below the threshold α*.",
        "D-PAFC-TC"      => "Principal allotment: the first splitting level of T̃, i.e. the coarsest non-trivial partition.",
        "D-AFC-TAGA"     => "Transitive approximation built with the chosen aggregation operator instead of the closure (experimental).",
        "D-PAFC-TAGA"    => "Principal allotment on the transitive approximation (experimental).",
        "H-AFC-TC"       => "Every level of T̃: the whole hierarchy of allotments, shown as a dendrogram.",
        "D-PAFC-DR"      => "Attribute selection: D-PAFC on a tolerance relation between the attributes.",
        "D-PAFC-TC-DR"   => "Attribute selection with the transitive closure of the attribute relation.",
        _                => "Attribute selection with a transitive approximation of the attribute relation (experimental)."
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
        OnPropertyChanged(nameof(Variants));
        OnPropertyChanged(nameof(ShowVariants));
        OnPropertyChanged(nameof(IsRelational));
        OnPropertyChanged(nameof(IsPrototype));
        OnPropertyChanged(nameof(IsReduction));
        OnPropertyChanged(nameof(SelectedVariant));
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
            MaxIntersections: ShowIntersections && int.TryParse(IntersectionsText, out int w) ? System.Math.Max(0, w) : 0,
            MinClusterPower:  System.Math.Max(1, MinClusterPower)   // the Settings page edits this value
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
