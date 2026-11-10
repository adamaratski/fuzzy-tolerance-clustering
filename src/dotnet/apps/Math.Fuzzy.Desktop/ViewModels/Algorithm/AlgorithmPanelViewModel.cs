using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Clustering;
using MathFuzzy.Desktop.Services;

namespace MathFuzzy.Desktop.ViewModels.Algorithm;

public sealed partial class AlgorithmPanelViewModel : ObservableObject
{
    [ObservableProperty] private string _selectedAlgorithm = "D-AFC-TC";
    [ObservableProperty] private DistanceInfo _selectedDistance = DistanceParameterFactory.Items[0];
    [ObservableProperty] private string _selectedNormalization = "MinMax";
    [ObservableProperty] private string _selectedTagaOperator = "Automatic";
    [ObservableProperty] private string _selectedCriterion = "F₁";
    [ObservableProperty] private string _clusterCountText = "";
    [ObservableProperty] private string _userAlphaText = "0.5";
    /// <summary>Accuracy threshold ε (empty = full precision), e.g. 0.0001.</summary>
    [ObservableProperty] private string _accuracyText = "";
    /// <summary>D-PAFC: admissible number of objects in intersection areas (0 = fully separated).</summary>
    [ObservableProperty] private string _intersectionsText = "0";
    /// <summary>D-AFC(u): the maximal number of objects in a cluster.</summary>
    [ObservableProperty] private string _maxClusterSizeText = "";
    [ObservableProperty] private IDistanceParameterViewModel _distanceParameters = DistanceParameterFactory.Create(DistanceParameterFactory.Items[0]);
    [ObservableProperty] private int _minClusterPower = 1;
    [ObservableProperty] private string _selectedAlphaSelector = "All levels";
    [ObservableProperty] private int _maxResults = 200;
    /// <summary>Intuitionistic algorithms: order of the scan over the pairs of thresholds (α, β).</summary>
    [ObservableProperty] private string _selectedScanOrder = "β inner";
    /// <summary>Intuitionistic algorithms on attribute data: the similarity measure that builds the tolerance.</summary>
    [ObservableProperty] private string _selectedIfsMeasure = "Hamming";
    /// <summary>
    /// Algorithms with partial supervision: the labelled objects, written as object:y (for
    /// intuitionistic data object:y:f), separated by commas or spaces; objects are numbered from 1.
    /// </summary>
    [ObservableProperty] private string _labelsText = "";
    /// <summary>TO algorithms: how the transitive opening is computed.</summary>
    [ObservableProperty] private string _selectedOpening = "Complete linkage";

    public IReadOnlyList<string> Algorithms { get; } =
    [
        "D-AFC", "D-AFC(u)", "D-PAFC", "D-AFC-PS(c)",
        "D-AFC-TC", "D-AFC-TC(α)", "D-PAFC-TC",
        "D-AFC-TO", "D-AFC-TO(α)", "D-PAFC-TO",
        "D-AFC-TAGA", "D-AFC-TAGA(α)", "D-PAFC-TAGA",
        "H-AFC-TC", "H-AFC-TAGA",
        "D-PAFC-DR", "D-PAFC-TC-DR", "D-PAFC-TAGA-DR",
        "D-PAIFC", "D-AIFC(c)", "D-PAIFC-TC", "D-AIFC-PS(c)"
    ];

    public IReadOnlyList<string> OpeningMethods { get; } = ["Complete linkage", "Single-linkage order"];

    public IReadOnlyList<string> ScanOrders  { get; } = ["β inner", "β outer", "order-free"];
    public IReadOnlyList<string> IfsMeasures { get; } = ["Hamming", "Euclidean"];

    public IReadOnlyList<DistanceInfo> Distances      { get; } = DistanceParameterFactory.Items;
    public IReadOnlyList<string> Normalizations { get; } = ["MinMax", "Max", "None"];
    public IReadOnlyList<string> Criteria        { get; } = ["F₁", "F₂"];
    public IReadOnlyList<string> TagaOperators   { get; } = ["Automatic", "Maximum", "Minimum", "Mean", "Median", "UpperMedian", "LowerMedian"];
    /// <summary>D-AFC level search: every distinct α of T, or the single level α* entered by the user.</summary>
    public IReadOnlyList<string> AlphaSelectors  { get; } = ["All levels", "Manual"];

    public string CriterionFormula => SelectedCriterion switch
    {
        "F₂" or "F2" => "Σᵢ[Σⱼ∈Cᵢ μ(cᵢ,xⱼ)/|Cᵢ|] / c",
        _            => "Σᵢ[Σⱼ∈Cᵢ μ(cᵢ,xⱼ)/|Cᵢ|] − α·c"
    };

    // One fixed list and one combo box per category: a combo box whose items never
    // change keeps its selection when the category changes.
    public IReadOnlyList<string> RelationalVariants   { get; } = ["D-AFC", "D-AFC(u)", "D-PAFC", "D-AFC-PS(c)"];
    public IReadOnlyList<string> PrototypeVariants    { get; } =
        ["D-AFC-TC", "D-AFC-TC(α)", "D-PAFC-TC", "D-AFC-TO", "D-AFC-TO(α)", "D-PAFC-TO", "D-AFC-TAGA", "D-AFC-TAGA(α)", "D-PAFC-TAGA"];
    public IReadOnlyList<string> HierarchicalVariants { get; } = ["H-AFC-TC", "H-AFC-TAGA"];
    public IReadOnlyList<string> ReductionVariants    { get; } = ["D-PAFC-DR", "D-PAFC-TC-DR", "D-PAFC-TAGA-DR"];
    public IReadOnlyList<string> IntuitionisticVariants { get; } = ["D-PAIFC", "D-AIFC(c)", "D-PAIFC-TC", "D-AIFC-PS(c)"];

    public bool IsRelational => CategoryText == "Relational";
    public bool IsPrototype  => CategoryText == "Prototype-based";
    public bool IsReduction  => CategoryText == "Dim. reduction";
    public bool IsHierarchical => CategoryText == "Hierarchical";
    public bool IsIntuitionistic => CategoryText == "Intuitionistic";
    /// <summary>Distance, normalisation, criterion and accuracy apply to ordinary attribute data only.</summary>
    public bool UsesAttributeDistance => !IsIntuitionistic;

    /// <summary>The algorithms of the selected category; the chip shows the selected one.</summary>
    public IReadOnlyList<string> Variants => CategoryText switch
    {
        "Relational"      => RelationalVariants,
        "Prototype-based" => PrototypeVariants,
        "Hierarchical"    => HierarchicalVariants,
        "Intuitionistic"  => IntuitionisticVariants,
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
        "D-AFC(u)"       => "The largest cluster size u is given; c and α follow. Separation is relaxed only if no fully separated allotment exists.",
        "D-AFC-TAGA"     => "Transitive approximation with an aggregation operator instead of the closure; the level is chosen by the leap heuristic.",
        "D-AFC-TAGA(α)"  => "As D-AFC-TAGA, but the leap is searched only at levels not below the threshold α*.",
        "D-PAFC-TAGA"    => "Principal allotment on the transitive approximation: its first splitting level.",
        "H-AFC-TC"       => "Every level of T̃: the whole hierarchy of allotments, shown as a dendrogram.",
        "H-AFC-TAGA"     => "Every level of the transitive approximation: a hierarchy that depends on the aggregation operator.",
        "D-AFC-PS(c)"    => "c is given, and some objects are labelled with a least membership: only allotments that keep the labelled objects in different clusters with at least these memberships are admitted.",
        "D-AFC-TO"       => "Transitive opening of T (a fuzzy equivalence below T) instead of the closure; the level is chosen by the leap heuristic.",
        "D-AFC-TO(α)"    => "As D-AFC-TO, but the leap is searched only at levels not below the threshold α*.",
        "D-PAFC-TO"      => "Principal allotment on the transitive opening: its first splitting level.",
        "D-AIFC-PS(c)"   => "Intuitionistic fuzzy data, c is given, labelled objects with a least membership and a largest non-membership restrict the allotments.",
        "D-PAIFC"        => "Intuitionistic fuzzy data: the first pair of thresholds (α, β) with fully separated clusters, the least c.",
        "D-AIFC(c)"      => "Intuitionistic fuzzy data, c is given; clusters may share objects only if no fully separated allotment exists.",
        "D-PAIFC-TC"     => "Intuitionistic fuzzy data: transitive closure of the intuitionistic tolerance, then D-PAIFC; prototypes resolve ties.",
        "D-PAFC-DR"      => "Attribute selection: D-PAFC on a tolerance relation between the attributes.",
        "D-PAFC-TC-DR"   => "Attribute selection with the transitive closure of the attribute relation.",
        _                => "Attribute selection with a transitive approximation of the attribute relation."
    };

    public string ChipText     => SelectedAlgorithm;
    public string CategoryText => SelectedAlgorithm switch
    {
        "D-AFC" or "D-AFC(u)" or "D-PAFC" or "D-AFC-PS(c)"                            => "Relational",
        "D-AFC-TC" or "D-AFC-TC(α)" or "D-PAFC-TC" or "D-AFC-TAGA" or "D-AFC-TAGA(α)" or "D-PAFC-TAGA"
            or "D-AFC-TO" or "D-AFC-TO(α)" or "D-PAFC-TO"                             => "Prototype-based",
        "H-AFC-TC" or "H-AFC-TAGA"                                                    => "Hierarchical",
        "D-PAIFC" or "D-AIFC(c)" or "D-PAIFC-TC" or "D-AIFC-PS(c)"                    => "Intuitionistic",
        _                                                                             => "Dim. reduction"
    };

    public bool ShowClusterCount  => SelectedAlgorithm is "D-AFC" or "D-AIFC(c)" or "D-AFC-PS(c)" or "D-AIFC-PS(c)";
    /// <summary>The algorithms with partial supervision take labelled objects.</summary>
    public bool ShowLabels        => SelectedAlgorithm is "D-AFC-PS(c)" or "D-AIFC-PS(c)";
    /// <summary>The TO algorithms offer two constructions of the transitive opening.</summary>
    public bool ShowOpening       => SelectedAlgorithm is "D-AFC-TO" or "D-AFC-TO(α)" or "D-PAFC-TO";
    public bool ShowAlphaSelector => SelectedAlgorithm is "D-AFC";
    public bool ShowIntersections => SelectedAlgorithm is "D-PAFC";
    public bool ShowMaxClusterSize => SelectedAlgorithm is "D-AFC(u)";

    private bool _runAttempted;

    /// <summary>
    /// True when Run was pressed with parameters that are missing or out of range: the message is
    /// then shown as an error. Before that it is a plain hint on what the algorithm needs.
    /// </summary>
    public bool ProblemIsError => _runAttempted && ValidationError is not null;

    /// <summary>Called when Run is pressed while the parameters are not valid.</summary>
    public void MarkRunAttempt()
    {
        _runAttempted = true;
        OnPropertyChanged(nameof(ProblemIsError));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ValidationError))
        {
            // once the parameters are valid again, the next problem starts as a hint
            if (ValidationError is null) _runAttempted = false;
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(ProblemIsError)));
        }
    }

    /// <summary>Validation message for the current parameters, or null if they are valid.</summary>
    public string? ValidationError
    {
        get
        {
            if (ShowClusterCount && (!int.TryParse(ClusterCountText, out int c) || c < 2))
                return IsIntuitionistic
                    ? $"{SelectedAlgorithm} needs the number of clusters c (an integer ≥ 2). To search c, choose D-PAIFC."
                    : $"{SelectedAlgorithm} needs the number of clusters c (an integer ≥ 2). To search c, choose D-PAFC.";
            if (ShowLabels && LabelsProblem() is { } labelsProblem)
                return labelsProblem;
            if (ShowMaxClusterSize && (!int.TryParse(MaxClusterSizeText, out int u) || u < 1))
                return "D-AFC(u) needs the maximal number of objects in a cluster u (an integer ≥ 1 and below n).";
            if (ShowIntersections && (!int.TryParse(IntersectionsText, out int w) || w < 0))
                return "Intersections w must be an integer ≥ 0 (0 = fully separated clusters).";
            if (ShowUserAlpha &&
                (!double.TryParse(UserAlphaText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double a) || a <= 0 || a >= 1))
                return "α* must be a number in (0, 1).";
            if (SelectedAlgorithm is "D-AIFC(c)" or "D-AIFC-PS(c)" && SelectedScanOrder == "order-free")
                return $"The order-free definition is available for D-PAIFC and D-PAIFC-TC. Choose β inner or β outer for {SelectedAlgorithm}.";
            if (!IsIntuitionistic && !string.IsNullOrWhiteSpace(AccuracyText) &&
                (!double.TryParse(AccuracyText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double e) || e <= 0 || e >= 1))
                return "Accuracy ε must be empty or a number in (0, 1), e.g. 0.0001.";
            return null;
        }
    }
    public bool ShowTagaOperator => SelectedAlgorithm.Contains("TAGA");
    public bool ShowUserAlpha    =>
        SelectedAlgorithm is "D-AFC-TC(α)" or "D-AFC-TAGA(α)" or "D-AFC-TO(α)" || (ShowAlphaSelector && SelectedAlphaSelector == "Manual");

    /// <summary>The labelled objects of the text field; an empty list when the field cannot be read.</summary>
    private IReadOnlyList<LabelSpec> ParseLabels()
    {
        var parts = LabelsText.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        try { return [.. parts.Select(AlgorithmFactory.ParseLabel)]; }
        catch (ArgumentException) { return []; }
    }

    /// <summary>What is wrong with the labelled objects of the text field, or null.</summary>
    private string? LabelsProblem()
    {
        var parts = LabelsText.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        string form = IsIntuitionistic ? "object:y:f, for example 1:1.0:0.0, 5:0.9:0.1" : "object:y, for example 1:1.0, 9:0.9";
        if (parts.Length == 0)
            return $"{SelectedAlgorithm} needs labelled objects, written as {form} (objects are numbered from 1).";
        try
        {
            foreach (string part in parts)
            {
                var label = AlgorithmFactory.ParseLabel(part);
                if (IsIntuitionistic && label.NonMembership is null)
                    return $"Label '{part}': intuitionistic data need a non-membership grade as well ({form}).";
                if (!IsIntuitionistic && label.NonMembership is not null)
                    return $"Label '{part}': D-AFC-PS(c) takes a membership grade only ({form}).";
            }
        }
        catch (ArgumentException problem)
        {
            return problem.Message;
        }
        return null;
    }

    partial void OnSelectedAlgorithmChanged(string value)
    {
        _runAttempted = false;   // another algorithm: its requirements are first shown as a hint
        OnPropertyChanged(nameof(ShowClusterCount));
        OnPropertyChanged(nameof(ShowAlphaSelector));
        OnPropertyChanged(nameof(ShowIntersections));
        OnPropertyChanged(nameof(ShowMaxClusterSize));
        OnPropertyChanged(nameof(ShowLabels));
        OnPropertyChanged(nameof(ShowOpening));
        OnPropertyChanged(nameof(IsHierarchical));
        OnPropertyChanged(nameof(IsIntuitionistic));
        OnPropertyChanged(nameof(UsesAttributeDistance));
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
    partial void OnLabelsTextChanged(string value)       => OnPropertyChanged(nameof(ValidationError));

    partial void OnIntersectionsTextChanged(string value) => OnPropertyChanged(nameof(ValidationError));
    partial void OnMaxClusterSizeTextChanged(string value) => OnPropertyChanged(nameof(ValidationError));
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
            MinClusterPower:  System.Math.Max(1, MinClusterPower),   // the Settings page edits this value
            MaxClusterPower:  ShowMaxClusterSize && int.TryParse(MaxClusterSizeText, out int u) ? System.Math.Max(0, u) : 0,
            ThresholdScanOrder:    !IsIntuitionistic ? null : SelectedScanOrder switch
            {
                "β outer"    => "beta-outer",
                "order-free" => "order-free",
                _            => "beta-inner"
            },
            IntuitionisticMeasure: IsIntuitionistic ? SelectedIfsMeasure.ToLowerInvariant() : null,
            Labels:        ShowLabels ? ParseLabels() : null,
            OpeningMethod: !ShowOpening ? null : SelectedOpening == "Single-linkage order" ? "single-linkage-order" : "complete-linkage"
        );
    }

    private static string AlgorithmKey(string displayName) => displayName switch
    {
        "D-AFC"          => "dafc",
        "D-PAFC"         => "dpafc",
        "D-AFC-TC"       => "dafc-tc",
        "D-AFC-TC(α)"    => "dafc-tc-alpha",
        "D-PAFC-TC"      => "dpafc-tc",
        "D-AFC(u)"       => "dafc-u",
        "D-AFC-TAGA"     => "dafc-taga",
        "D-AFC-TAGA(α)"  => "dafc-taga-alpha",
        "H-AFC-TAGA"     => "hafc-taga",
        "D-AFC-PS(c)"    => "dafc-ps",
        "D-AFC-TO"       => "dafc-to",
        "D-AFC-TO(α)"    => "dafc-to-alpha",
        "D-PAFC-TO"      => "dpafc-to",
        "D-AIFC-PS(c)"   => "daifc-ps",
        "D-PAIFC"        => "dpaifc",
        "D-AIFC(c)"      => "daifc",
        "D-PAIFC-TC"     => "dpaifc-tc",
        "D-PAFC-TAGA"    => "dpafc-taga",
        "H-AFC-TC"       => "hafc-tc",
        "D-PAFC-DR"      => "dpafc-dr",
        "D-PAFC-TC-DR"   => "dpafc-tc-dr",
        "D-PAFC-TAGA-DR" => "dpafc-taga-dr",
        _                => displayName.ToLowerInvariant()
    };
}
