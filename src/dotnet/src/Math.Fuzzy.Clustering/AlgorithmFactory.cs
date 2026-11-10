using MathFuzzy.Clustering.Algorithms.DimensionReduction;
using MathFuzzy.Clustering.Algorithms.Hierarchical;
using MathFuzzy.Clustering.Algorithms.Intuitionistic;
using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Algorithms.Supervision;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Intuitionistic;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering;

/// <summary>
/// Resolves clustering algorithm instances from string keys.
/// Accepted keys (case-insensitive): dafc, dafc-u, dpafc, dafc-tc, dafc-tc-alpha, dpafc-tc,
/// dafc-taga, dafc-taga-alpha, dpafc-taga, dafc-to, dafc-to-alpha, dpafc-to, hafc-tc, hafc-taga,
/// dpafc-dr, dpafc-tc-dr, dpafc-taga-dr;
/// for intuitionistic fuzzy data (see <see cref="RunIntuitionistic(string, IntuitionisticToleranceMatrix, AlgorithmOptions, CancellationToken)"/>):
/// dpaifc, daifc, dpaifc-tc.
/// </summary>
public static class AlgorithmFactory
{
    private const string ValidKeys =
        "dafc, dafc-u, dpafc, dafc-tc, dafc-tc-alpha, dpafc-tc, dafc-taga, dafc-taga-alpha, dpafc-taga, " +
        "dafc-to, dafc-to-alpha, dpafc-to, dafc-ps, " +
        "hafc-tc, hafc-taga, dpafc-dr, dpafc-tc-dr, dpafc-taga-dr, dpaifc, daifc, dpaifc-tc, daifc-ps";

    private static readonly string[] AllKeys = ValidKeys.Split(", ");

    /// <summary>
    /// Runs the specified algorithm on attribute data and returns flat cluster results.
    /// For hierarchical algorithms (<c>hafc-tc</c>), returns the levels list.
    /// For DR algorithms, clusters after dimension reduction.
    /// </summary>
    public static IReadOnlyList<ClusterResult> Run(
        string           algorithm,
        RawDataMatrix    data,
        AlgorithmOptions options,
        CancellationToken ct = default)
    {
        string key = NormalizeKey(algorithm);
        var builder = CreateToleranceBuilder(options);

        if (IsReduction(key))
        {
            var reduced = CreateReducer(key, options).Reduce(data, ct);
            return RunOnTolerance("dpafc", builder.Build(reduced), builder.Normalize(reduced),
                                  options with { MaxIntersections = null }, ct);
        }

        return RunOnTolerance(key, builder.Build(data), builder.Normalize(data), options, ct);
    }

    /// <summary>
    /// Runs the specified algorithm on a fuzzy tolerance matrix given directly (a relational
    /// data set). The prototype-based algorithms then work without the geometric tie-breaking,
    /// which needs attribute data; the dimension-reduction algorithms are not applicable.
    /// </summary>
    public static IReadOnlyList<ClusterResult> Run(
        string               algorithm,
        FuzzyToleranceMatrix tolerance,
        AlgorithmOptions     options,
        CancellationToken    ct = default)
    {
        string key = NormalizeKey(algorithm);
        if (IsReduction(key))
            throw new ArgumentException(
                $"Algorithm '{key}' reduces the attribute space and needs attribute data, not a tolerance matrix.");
        return RunOnTolerance(key, tolerance, normalizedData: null, options, ct);
    }

    /// <summary>True for the algorithms that work on intuitionistic fuzzy data: dpaifc, daifc, dpaifc-tc.</summary>
    public static bool IsIntuitionistic(string algorithm) =>
        algorithm.ToLowerInvariant().Trim() is "dpaifc" or "daifc" or "dpaifc-tc" or "daifc-ps";

    /// <summary>
    /// Runs an intuitionistic algorithm on an intuitionistic fuzzy tolerance given directly
    /// (a membership and a non-membership matrix). For <c>dpaifc-tc</c> the relation is first
    /// replaced by its transitive closure.
    /// </summary>
    public static IReadOnlyList<ClusterResult> RunIntuitionistic(
        string algorithm, IntuitionisticToleranceMatrix relation, AlgorithmOptions options, CancellationToken ct = default)
    {
        var settings = IntuitionisticSettings(options);
        return NormalizeKey(algorithm) switch
        {
            "dpaifc"    => new DPaifcAlgorithm(settings).Run(relation, ct),
            "daifc"     => new DAifcAlgorithm(RequiredClusterCount(options), settings).Run(relation, ct),
            "dpaifc-tc" => new DPaifcTcAlgorithm(ParseMeasure(options.IntuitionisticMeasure), settings).Run(relation, ct),
            "daifc-ps"  => new DAifcPsAlgorithm(RequiredClusterCount(options), IntuitionisticLabels(options), settings,
                                                ParseSupervisionRule(options.SupervisionRule)).Run(relation, ct),
            var key     => throw new ArgumentException(
                               $"Algorithm '{key}' does not work on intuitionistic fuzzy data. Use dpaifc, daifc, dpaifc-tc or daifc-ps.")
        };
    }

    /// <summary>
    /// Runs an intuitionistic algorithm on intuitionistic attribute data: the tolerance is built
    /// with the similarity measure named in the options (hamming by default).
    /// </summary>
    public static IReadOnlyList<ClusterResult> RunIntuitionistic(
        string algorithm, IntuitionisticDataMatrix data, AlgorithmOptions options, CancellationToken ct = default)
    {
        var measure = ParseMeasure(options.IntuitionisticMeasure);
        if (NormalizeKey(algorithm) == "dpaifc-tc")
            return new DPaifcTcAlgorithm(measure, IntuitionisticSettings(options)).Run(data, ct);
        return RunIntuitionistic(algorithm, IntuitionisticSimilarity.BuildTolerance(data, measure), options, ct);
    }

    private static IReadOnlyList<LabelSpec> RequiredLabels(AlgorithmOptions options) =>
        options.Labels is { Count: > 0 } labels
            ? labels
            : throw new ArgumentException("The algorithms with partial supervision need labelled objects (--label).");

    private static IReadOnlyList<IntuitionisticLabelledObject> IntuitionisticLabels(AlgorithmOptions options) =>
        [.. RequiredLabels(options).Select(label => new IntuitionisticLabelledObject(
            label.ObjectIndex, label.Membership,
            label.NonMembership ?? throw new ArgumentException(
                $"The labelled object {label.ObjectIndex + 1} needs a non-membership grade for D-AIFC-PS(c): --label object:y:f."),
            label.Class))];

    private static SupervisedIntersectionRule ParseSupervisionRule(string? name) => name?.ToLowerInvariant().Trim() switch
    {
        null or "" or "step10" => SupervisedIntersectionRule.AtCurrentPair,
        "full-scan"            => SupervisedIntersectionRule.AfterFullScan,
        var other => throw new ArgumentException($"Unknown rule for the intersection bound: '{other}'. Valid names: step10, full-scan")
    };

    private static int RequiredClusterCount(AlgorithmOptions options) =>
        options.ClusterCount ?? throw new ArgumentException("D-AIFC(c) needs the number of clusters (--clusters).");

    private static IntuitionisticOptions IntuitionisticSettings(AlgorithmOptions options) => new(
        ScanOrder: options.ThresholdScanOrder?.ToLowerInvariant().Trim() switch
        {
            null or "" or "beta-inner" => ThresholdScanOrder.BetaInner,
            "beta-outer"               => ThresholdScanOrder.BetaOuter,
            "order-free"               => ThresholdScanOrder.OrderFree,
            var other => throw new ArgumentException($"Unknown scan order: '{other}'. Valid names: beta-inner, beta-outer, order-free")
        },
        MaxSearchNodes: options.MaxSearchNodes,
        MaxResults: options.MaxResults,
        Diagnostics: options.Diagnostics);

    private static IntuitionisticMeasure ParseMeasure(string? name) => name?.ToLowerInvariant().Trim() switch
    {
        null or "" or "hamming" => IntuitionisticMeasure.Hamming,
        "euclidean"             => IntuitionisticMeasure.Euclidean,
        var other => throw new ArgumentException($"Unknown intuitionistic measure: '{other}'. Valid names: hamming, euclidean")
    };

    private static IReadOnlyList<ClusterResult> RunOnTolerance(
        string key, FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData,
        AlgorithmOptions options, CancellationToken ct)
    {
        if (IsIntuitionistic(key))
            throw new ArgumentException(
                $"Algorithm '{key}' needs intuitionistic fuzzy data: a membership and a non-membership matrix, " +
                "or attribute data given as (membership, non-membership) pairs.");

        var builder   = CreateToleranceBuilder(options);   // used by the data entry points of the algorithms
        var closure   = new MaxMinTransitiveClosure();
        var criterion = CreateCriterion(options.Criterion);
        int minPower  = options.MinClusterPower;
        int maxPower  = options.MaxClusterPower;

        switch (key)
        {
            case "dafc":
            {
                int clusterCount = options.ClusterCount
                    ?? throw new ArgumentException("D-AFC(c) needs the number of clusters (--clusters).");

                // A given threshold restricts the search to the nearest level not above it.
                IAlphaSelector selector = options.UserAlpha.HasValue
                    ? new ManualAlphaSelector([ResolveAlpha(tolerance, options.UserAlpha.Value)])
                    : AllDistinctAlphaSelector.Instance;

                return new DAfcAlgorithm(
                    new DAfcOptions(clusterCount, minPower, maxPower,
                                    MaxIntersections: options.MaxIntersections ?? DAfcOptions.AutoIntersections,
                                    MaxCombinations:  options.MaxSearchNodes,
                                    MaxResults:       options.MaxResults,
                                    LevelProgress:    options.LevelProgress,
                                    Diagnostics:      options.Diagnostics),
                    selector, criterion).Run(tolerance, ct);
            }

            case "dafc-ps":
            {
                int clusterCount = options.ClusterCount
                    ?? throw new ArgumentException("D-AFC-PS(c) needs the number of clusters (--clusters).");
                var labels = RequiredLabels(options);
                if (labels.Any(label => label.NonMembership.HasValue))
                    throw new ArgumentException("D-AFC-PS(c) takes a membership grade only: --label object:y.");
                return new DAfcPsAlgorithm(
                    new DAfcOptions(clusterCount, minPower, maxPower,
                                    MaxIntersections: options.MaxIntersections ?? DAfcOptions.AutoIntersections,
                                    MaxCombinations:  options.MaxSearchNodes,
                                    MaxResults:       options.MaxResults,
                                    LevelProgress:    options.LevelProgress,
                                    Diagnostics:      options.Diagnostics),
                    [.. labels.Select(label => new LabelledObject(label.ObjectIndex, label.Membership, label.Class))],
                    AllDistinctAlphaSelector.Instance, criterion).Run(tolerance, ct);
            }

            case "dafc-u":
            {
                if (maxPower < 1)
                    throw new ArgumentException("D-AFC(u) needs the maximal number of objects in a cluster (--max-size).");
                return new DAfcUAlgorithm(maxPower, criterion, minPower,
                                          options.MaxIntersections ?? DAfcOptions.AutoIntersections,
                                          options.MaxSearchNodes, options.Diagnostics, options.MaxResults,
                                          options.LevelProgress)
                    .Run(tolerance, ct);
            }

            case "dpafc":
                return new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, criterion, minPower, maxPower,
                                          options.MaxIntersections ?? 0, options.LevelProgress,
                                          options.MaxSearchNodes, options.Diagnostics, options.MaxResults)
                    .Run(tolerance, ct);

            case "dafc-tc":
            {
                var algorithm = new DAfcTcAlgorithm(builder, closure, criterion, minPower, maxPower, options.MaxResults);
                return options.ClusterCount is int clusterCount
                    ? algorithm.RunForClusterCount(tolerance, clusterCount, normalizedData, ct)
                    : algorithm.Run(tolerance, normalizedData, ct);
            }

            case "dafc-tc-alpha":
            {
                double leastAlpha = options.UserAlpha
                    ?? throw new ArgumentException("D-AFC-TC(α) needs the least threshold (--alpha).");
                return new DAfcTcAlphaAlgorithm(builder, closure, criterion, leastAlpha, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);
            }

            case "dpafc-tc":
                return new DPafcTcAlgorithm(builder, closure, criterion, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);

            case "dafc-taga":
                return new DAfcTagaAlgorithm(builder, BuildTaga(options.TagaOperator), criterion, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);

            case "dafc-taga-alpha":
            {
                double leastAlpha = options.UserAlpha
                    ?? throw new ArgumentException("D-AFC-TAGA(α) needs the least threshold (--alpha).");
                return new DAfcTagaAlgorithm(builder, BuildTaga(options.TagaOperator), criterion, minPower, maxPower,
                                             options.MaxResults, leastAlpha)
                    .Run(tolerance, normalizedData, ct);
            }

            case "dpafc-taga":
                return new DPafcTagaAlgorithm(builder, BuildTaga(options.TagaOperator), criterion, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);

            case "dafc-to":
                return new DAfcToAlgorithm(builder, BuildOpening(options.OpeningMethod), criterion, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);

            case "dafc-to-alpha":
            {
                double leastAlpha = options.UserAlpha
                    ?? throw new ArgumentException("D-AFC-TO(α) needs the least threshold (--alpha).");
                return new DAfcToAlgorithm(builder, BuildOpening(options.OpeningMethod), criterion, minPower, maxPower,
                                           options.MaxResults, leastAlpha)
                    .Run(tolerance, normalizedData, ct);
            }

            case "dpafc-to":
                return new DPafcToAlgorithm(builder, BuildOpening(options.OpeningMethod), criterion, minPower, maxPower, options.MaxResults)
                    .Run(tolerance, normalizedData, ct);

            case "hafc-tc":
                return new HAfcTcAlgorithm(builder, closure, criterion, minPower).Run(tolerance, normalizedData, ct).Levels;

            case "hafc-taga":
                return HAfcTcAlgorithm.Build(BuildTaga(options.TagaOperator).Apply(tolerance), criterion, minPower,
                                             normalizedData, ct).Levels;

            default:
                throw new ArgumentException($"Unknown algorithm key: '{key}'. Valid keys: {ValidKeys}");
        }
    }

    /// <summary>
    /// Checks that every option that was set applies to the algorithm, and that the options the
    /// algorithm needs are present; an <see cref="ArgumentException"/> names the offending ones.
    /// An option that an algorithm would silently ignore is an error, not a no-op.
    /// </summary>
    public static void EnsureOptionsApply(string algorithm, AlgorithmOptions options)
    {
        string key = NormalizeKey(algorithm);
        var problems = new List<string>();

        void Reject(bool isSet, string option, string usedBy)
        {
            if (isSet) problems.Add($"{option} does not apply to '{key}' (it is used by {usedBy})");
        }

        if (key is not ("dafc" or "dafc-tc" or "daifc" or "dafc-ps" or "daifc-ps"))
            Reject(options.ClusterCount.HasValue, "the number of clusters", "dafc, dafc-tc, daifc, dafc-ps, daifc-ps");
        if (key is not ("dafc-ps" or "daifc-ps"))
            Reject(options.Labels is { Count: > 0 }, "labelled objects", "dafc-ps, daifc-ps");
        else
        {
            if (!options.ClusterCount.HasValue) problems.Add($"'{key}' needs the number of clusters");
            if (options.Labels is not { Count: > 0 }) problems.Add($"'{key}' needs labelled objects");
        }
        if (key != "daifc-ps")
            Reject(!string.IsNullOrEmpty(options.SupervisionRule), "the rule for the intersection bound", "daifc-ps");
        if (IsIntuitionistic(key))
        {
            Reject(options.MinClusterPower != 1 || options.MaxClusterPower != 0, "a bound on the cluster size", "the crisp-relation algorithms");
            Reject(options.Accuracy.HasValue, "the accuracy threshold", "the crisp-relation algorithms");
        }
        else
        {
            Reject(!string.IsNullOrEmpty(options.ThresholdScanOrder), "the scan order of the thresholds", "dpaifc, daifc, dpaifc-tc");
            Reject(!string.IsNullOrEmpty(options.IntuitionisticMeasure), "the intuitionistic similarity measure", "dpaifc, daifc, dpaifc-tc");
        }
        if (key == "daifc" && !options.ClusterCount.HasValue)
            problems.Add("'daifc' needs the number of clusters");
        if (key is not ("dafc" or "dafc-tc-alpha" or "dafc-taga-alpha" or "dafc-to-alpha"))
            Reject(options.UserAlpha.HasValue, "the threshold α", "dafc, dafc-tc-alpha, dafc-taga-alpha, dafc-to-alpha");
        if (key is not ("dafc-to" or "dafc-to-alpha" or "dpafc-to"))
            Reject(!string.IsNullOrEmpty(options.OpeningMethod), "the method of the transitive opening", "dafc-to, dafc-to-alpha, dpafc-to");
        if (key is not ("dafc" or "dafc-u" or "dpafc" or "dafc-ps"))
            Reject(options.MaxIntersections.HasValue, "the intersection bound w", "dafc, dafc-u, dpafc, dafc-ps");
        if (key == "dafc-u" && options.MaxClusterPower < 1)
            problems.Add("'dafc-u' needs the maximal number of objects in a cluster");
        if (!key.Contains("taga"))
            Reject(!string.IsNullOrEmpty(options.TagaOperator), "the aggregation operator", "the TAGA algorithms");

        if (key == "dafc" && !options.ClusterCount.HasValue)
            problems.Add("'dafc' needs the number of clusters");
        if (key is "dafc-tc-alpha" or "dafc-taga-alpha" or "dafc-to-alpha" && !options.UserAlpha.HasValue)
            problems.Add($"'{key}' needs the least threshold α");
        if (options.ClusterCount is < 2)
            problems.Add("the number of clusters must be at least 2");
        if (options.UserAlpha is < 0.0 or > 1.0)
            problems.Add("the threshold α must lie in [0, 1]");
        if (options.MaxIntersections is < 0)
            problems.Add("the intersection bound w must not be negative");
        if (options.MinClusterPower < 1)
            problems.Add("the least number of objects in a cluster must be at least 1");

        if (problems.Count > 0)
            throw new ArgumentException(string.Join("; ", problems) + ".");
    }

    private static string NormalizeKey(string algorithm)
    {
        string key = algorithm.ToLowerInvariant().Trim();
        if (Array.IndexOf(AllKeys, key) < 0)
            throw new ArgumentException($"Unknown algorithm key: '{algorithm}'. Valid keys: {ValidKeys}");
        return key;
    }

    private static bool IsReduction(string key) => key.EndsWith("-dr", StringComparison.Ordinal);

    /// <summary>
    /// Resolves a user-supplied alpha to the nearest matrix value ≤ userAlpha.
    /// Falls back to the smallest distinct value if none qualifies.
    /// </summary>
    private static double ResolveAlpha(FuzzyToleranceMatrix matrix, double userAlpha)
    {
        var values = matrix.GetDistinctValues(); // descending
        double found = values.FirstOrDefault(v => v <= userAlpha);
        return found > 0.0 ? found : (values.Count > 0 ? values[^1] : userAlpha);
    }

    /// <summary>
    /// The tolerance-matrix builder (distance, normalisation, accuracy threshold) named by the options.
    /// An unknown distance or normalisation name is an error, not a silent default.
    /// </summary>
    public static ToleranceMatrixBuilder CreateToleranceBuilder(AlgorithmOptions options)
    {
        IDistance distance = options.DistanceOverride ?? options.Distance.ToLowerInvariant().Trim() switch
        {
            "euclidean" => EuclideanDistance.Instance,
            "hamming"   => HammingDistance.Instance,
            "squared-euclidean" or "squared" or "euclidean2" => SquaredEuclideanDistance.Instance,
            "manhattan" => ManhattanDistance.Instance,
            "chebyshev" => ChebyshevDistance.Instance,
            "cosine"    => CosineDistance.Instance,
            "minkowski" => new MinkowskiDistance(options.MinkowskiP),
            _ => throw new ArgumentException(
                     $"Unknown distance: '{options.Distance}'. Valid names: euclidean, squared-euclidean, " +
                     "hamming, manhattan, chebyshev, minkowski, cosine")
        };

        INormalization norm = options.Normalization.ToLowerInvariant().Trim() switch
        {
            "minmax" => MinMaxNormalization.Instance,
            "max"    => MaxNormalization.Instance,
            "none"   => NoNormalization.Instance,
            _ => throw new ArgumentException(
                     $"Unknown normalisation: '{options.Normalization}'. Valid names: minmax, max, none")
        };

        return new ToleranceMatrixBuilder(distance, norm, options.Accuracy);
    }

    /// <summary>The criterion named <c>f1</c> or <c>f2</c>.</summary>
    public static ICriterion CreateCriterion(string name) => name.ToLowerInvariant().Trim() switch
    {
        "f1" => F1Criterion.Instance,
        "f2" => F2Criterion.Instance,
        _ => throw new ArgumentException($"Unknown criterion: '{name}'. Valid names: f1, f2")
    };

    /// <summary>
    /// The attribute-space reducer for <c>dpafc-dr</c>, <c>dpafc-tc-dr</c> or <c>dpafc-taga-dr</c>
    /// (the short forms <c>dpafc</c>, <c>dpafc-tc</c>, <c>dpafc-taga</c> are accepted as well).
    /// </summary>
    public static IDimensionReducer CreateReducer(string method, AlgorithmOptions options)
    {
        var builder   = CreateToleranceBuilder(options);
        var criterion = CreateCriterion(options.Criterion);
        return method.ToLowerInvariant().Trim() switch
        {
            "dpafc-dr" or "dpafc"           => new DPafcDrReducer(builder, criterion),
            "dpafc-tc-dr" or "dpafc-tc"     => new DPafcTcDrReducer(builder, new MaxMinTransitiveClosure(), criterion),
            "dpafc-taga-dr" or "dpafc-taga" => new DPafcTagaDrReducer(builder, BuildTaga(options.TagaOperator), criterion),
            _ => throw new ArgumentException(
                     $"Unknown reduction method: '{method}'. Valid names: dpafc, dpafc-tc, dpafc-taga")
        };
    }

    /// <summary>
    /// The transitive-approximation processor for an operator name: max, min, mean, median,
    /// umedian, dmedian, or auto (the default) — the operator whose approximation is nearest to T.
    /// </summary>
    /// <summary>
    /// The transitive opening for the TO algorithms: <c>complete-linkage</c> (default), the
    /// construction that reproduces the published example, or <c>single-linkage-order</c>, the
    /// cited algorithm with the bound that keeps its result transitive.
    /// </summary>
    /// <summary>
    /// Reads a labelled object written as <c>object:y</c> or <c>object:y:f</c>, optionally followed
    /// by <c>@class</c>: the 1-based number of the object, its membership grade, for intuitionistic
    /// data its non-membership grade, and the number of its class when several labelled objects
    /// belong to one class.
    /// </summary>
    public static LabelSpec ParseLabel(string text)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string body = text.Trim();
        int? labelClass = null;
        int classMark = body.IndexOf('@');
        if (classMark >= 0)
        {
            if (!int.TryParse(body[(classMark + 1)..], System.Globalization.NumberStyles.Integer, culture, out int parsedClass))
                throw new ArgumentException($"Label '{text}': the class after '@' must be an integer.");
            labelClass = parsedClass;
            body = body[..classMark];
        }
        string[] parts = body.Split(':');
        if (parts.Length is < 2 or > 3
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, culture, out int objectNumber)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, culture, out double membership))
            throw new ArgumentException($"Label '{text}': expected object:y or object:y:f, for example 3:0.9 or 3:0.9:0.1.");
        double? nonMembership = null;
        if (parts.Length == 3)
        {
            if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, culture, out double parsedNonMembership))
                throw new ArgumentException($"Label '{text}': the non-membership grade is not a number.");
            nonMembership = parsedNonMembership;
        }
        if (objectNumber < 1) throw new ArgumentException($"Label '{text}': objects are numbered from 1.");
        return new LabelSpec(objectNumber - 1, membership, nonMembership, labelClass);
    }

    public static TransitiveOpening BuildOpening(string? method) => method?.ToLowerInvariant().Trim() switch
    {
        null or "" or "complete-linkage" => new TransitiveOpening(TransitiveOpeningMethod.CompleteLinkage),
        "single-linkage-order"           => new TransitiveOpening(TransitiveOpeningMethod.SingleLinkageOrder),
        var other => throw new ArgumentException(
            $"Unknown method of the transitive opening: '{other}'. Valid names: complete-linkage, single-linkage-order")
    };

    public static TAGAProcessor BuildTaga(string? operatorName) => operatorName?.ToLowerInvariant().Trim() switch
    {
        null or "" or "auto" or "automatic" => TAGAProcessor.Automatic,
        "mean"                     => new TAGAProcessor(MeanTAGA.Instance),
        "maximum" or "max"         => new TAGAProcessor(MaximumTAGA.Instance),
        "minimum" or "min"         => new TAGAProcessor(MinimumTAGA.Instance),
        "median"                   => new TAGAProcessor(MedianTAGA.Instance),
        "umedian" or "uppermedian" => new TAGAProcessor(UpperMedianTAGA.Instance),
        "dmedian" or "lowermedian" => new TAGAProcessor(LowerMedianTAGA.Instance),
        _ => throw new ArgumentException(
                 $"Unknown TAGA operator: '{operatorName}'. Valid names: auto, max, min, mean, median, umedian, dmedian")
    };
}

/// <summary>Options for algorithm factory — mirrors both CLI flags and WPF panel settings.</summary>
public sealed record AlgorithmOptions(
    string Distance         = "euclidean",
    string Normalization    = "minmax",
    int?   ClusterCount     = null,
    string? TagaOperator    = null,
    double? UserAlpha       = null,
    int    MinClusterPower  = 1,
    int    MaxClusterPower  = 0,
    double MinkowskiP       = 3.0,
    IDistance? DistanceOverride = null,
    string Criterion        = "f1",
    /// <summary>Optional D-AFC level-scan progress callback: (levels done, levels total).</summary>
    Action<int, int>? LevelProgress = null,
    /// <summary>
    /// Accuracy threshold ε (Viattchenin 2013, sec. 2.2.4): the tolerance matrix is rounded to
    /// multiples of ε before the α-levels are built. Null = full double precision.
    /// </summary>
    double? Accuracy        = null,
    /// <summary>
    /// Largest admissible number of objects in intersection areas of the clusters (w).
    /// Null = the algorithm's own rule: D-AFC(c) starts from w = 0 and relaxes it only while no
    /// allotment among c clusters exists; D-PAFC uses w = 0 (fully separated clusters).
    /// </summary>
    int? MaxIntersections   = null,
    /// <summary>
    /// Node budget of a combination search. When it is spent the search stops and returns the
    /// best allotments found so far; <see cref="Diagnostics"/> reports that this happened.
    /// </summary>
    long MaxSearchNodes     = 500_000_000L,
    /// <summary>Largest number of allotments returned, best first.</summary>
    int MaxResults          = 200,
    /// <summary>Optional collector of search statistics (node count, budget exhaustion).</summary>
    SearchDiagnostics? Diagnostics = null,
    /// <summary>
    /// Intuitionistic algorithms: order of the scan over the pairs of thresholds,
    /// <c>beta-inner</c> (default) or <c>beta-outer</c>.
    /// </summary>
    string? ThresholdScanOrder = null,
    /// <summary>
    /// Intuitionistic algorithms on attribute data: similarity measure that builds the tolerance,
    /// <c>hamming</c> (default) or <c>euclidean</c>.
    /// </summary>
    string? IntuitionisticMeasure = null,
    /// <summary>
    /// TO algorithms: how the transitive opening is computed, <c>complete-linkage</c> (default) or
    /// <c>single-linkage-order</c>.
    /// </summary>
    string? OpeningMethod = null,
    /// <summary>Algorithms with partial supervision: the labelled objects.</summary>
    IReadOnlyList<LabelSpec>? Labels = null,
    /// <summary>
    /// D-AIFC-PS(c): when the intersection bound is raised, <c>step10</c> (default, at the current
    /// pair of thresholds, as printed) or <c>full-scan</c> (after all pairs were tried, as in D-AIFC(c)).
    /// </summary>
    string? SupervisionRule = null
);

/// <summary>
/// A labelled object of an algorithm with partial supervision: the 0-based index of the object,
/// its membership grade, its non-membership grade (intuitionistic data only) and the class it
/// belongs to (null: a class of its own).
/// </summary>
public sealed record LabelSpec(int ObjectIndex, double Membership, double? NonMembership = null, int? Class = null);
