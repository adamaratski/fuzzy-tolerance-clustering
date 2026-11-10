using MathFuzzy.Clustering.Algorithms.DimensionReduction;
using MathFuzzy.Clustering.Algorithms.Hierarchical;
using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering;

/// <summary>
/// Resolves clustering algorithm instances from string keys.
/// Accepted keys (case-insensitive): dafc, dpafc, dafc-tc, dafc-tc-alpha, dpafc-tc,
/// dafc-taga, dpafc-taga, hafc-tc, dpafc-dr, dpafc-tc-dr, dpafc-taga-dr.
/// </summary>
public static class AlgorithmFactory
{
    /// <summary>
    /// Runs the specified algorithm and returns flat cluster results.
    /// For hierarchical algorithms (<c>hafc-tc</c>), returns the levels list.
    /// For DR algorithms, clusters after dimension reduction.
    /// </summary>
    public static IReadOnlyList<ClusterResult> Run(
        string           algorithm,
        RawDataMatrix    data,
        AlgorithmOptions options,
        CancellationToken ct = default)
    {
        var builder   = CreateToleranceBuilder(options);
        var closure   = new MaxMinTransitiveClosure();
        var taga      = BuildTaga(options.TagaOperator);
        var criterion = CreateCriterion(options.Criterion);

        return algorithm.ToLowerInvariant().Trim() switch
        {
            "dafc" => RunDafc(data, builder, options, criterion, ct),

            "dpafc" =>
                new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, criterion,
                                   options.MinClusterPower, options.MaxClusterPower,
                                   options.MaxIntersections, options.LevelProgress)
                    .Run(BuildMatrix(data, builder), ct),

            "dafc-tc" =>
                RunDafcTc(data, builder, closure, options, criterion, ct),

            "dafc-tc-alpha" =>
                new DAfcTcAlphaAlgorithm(builder, closure, criterion, options.UserAlpha ?? 0.5)
                    .Run(data, ct),

            "dpafc-tc" =>
                new DPafcTcAlgorithm(builder, closure, criterion)
                    .Run(data, ct),

            "dafc-taga" =>
                new DAfcTagaAlgorithm(builder, taga, criterion)
                    .Run(data, ct),

            "dpafc-taga" =>
                new DPafcTagaAlgorithm(builder, taga, criterion)
                    .Run(data, ct),

            "hafc-tc" =>
                new HAfcTcAlgorithm(builder, closure, criterion).Run(data, ct).Levels,

            "dpafc-dr" or "dpafc-tc-dr" or "dpafc-taga-dr" =>
                RunDr(CreateReducer(algorithm, options), data, builder, options, criterion, ct),

            _ => throw new ArgumentException(
                     $"Unknown algorithm key: '{algorithm}'. " +
                     "Valid keys: dafc, dpafc, dafc-tc, dafc-tc-alpha, dpafc-tc, dafc-taga, dpafc-taga, " +
                     "hafc-tc, dpafc-dr, dpafc-tc-dr, dpafc-taga-dr")
        };
    }

    private static IReadOnlyList<ClusterResult> RunDafc(
        RawDataMatrix data, ToleranceMatrixBuilder builder,
        AlgorithmOptions options, ICriterion criterion, CancellationToken ct)
    {
        var matrix = BuildMatrix(data, builder);
        int c = options.ClusterCount ?? throw new InvalidOperationException("D-AFC requires --clusters");

        // When --alpha is provided, search only that single level (fast, predictable).
        // Otherwise fall back to all distinct levels (exhaustive, slow for large n).
        IAlphaSelector selector = options.UserAlpha.HasValue
            ? new ManualAlphaSelector([ResolveAlpha(matrix, options.UserAlpha.Value)])
            : AllDistinctAlphaSelector.Instance;

        var dafc = new DAfcAlgorithm(
            new DAfcOptions(c, options.MinClusterPower, options.MaxClusterPower,
                            LevelProgress: options.LevelProgress),
            selector, criterion);
        return dafc.Run(matrix, ct);
    }

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

    private static IReadOnlyList<ClusterResult> RunDafcTc(
        RawDataMatrix data, ToleranceMatrixBuilder builder,
        MaxMinTransitiveClosure closure, AlgorithmOptions options,
        ICriterion criterion, CancellationToken ct)
    {
        if (options.ClusterCount.HasValue)
        {
            // Fixed c: build closure, search ALL distinct alpha levels in T̃.
            // The closure compresses many raw values → far fewer distinct levels;
            // this makes the search tractable even for n~150.
            var Tt   = new DAfcTcAlgorithm(builder, closure, criterion).BuildClosureMatrix(data);
            var dafc = new DAfcAlgorithm(
                new DAfcOptions(options.ClusterCount.Value, options.MinClusterPower, options.MaxClusterPower,
                                LevelProgress: options.LevelProgress),
                AllDistinctAlphaSelector.Instance, criterion);
            return dafc.Run(Tt, ct);
        }
        // Auto-c: full D-AFC-TC
        return new DAfcTcAlgorithm(builder, closure, criterion).Run(data, ct);
    }

    private static IReadOnlyList<ClusterResult> RunDr(
        IDimensionReducer reducer, RawDataMatrix data,
        ToleranceMatrixBuilder builder, AlgorithmOptions options,
        ICriterion criterion, CancellationToken ct)
    {
        var reduced = reducer.Reduce(data, ct);
        // After DR, run D-PAFC on the reduced data
        var matrix = BuildMatrix(reduced, builder);
        return new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, criterion).Run(matrix, ct);
    }

    private static FuzzyToleranceMatrix BuildMatrix(RawDataMatrix data, ToleranceMatrixBuilder builder)
        => builder.Build(data);

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

    private static TAGAProcessor BuildTaga(string? operatorName)
    {
        ITAGAOperator op = operatorName?.ToLowerInvariant().Trim() switch
        {
            null or "" or "mean" => MeanTAGA.Instance,
            "maximum" or "max"   => MaximumTAGA.Instance,
            "minimum" or "min"   => MinimumTAGA.Instance,
            "median"             => MedianTAGA.Instance,
            "umedian" or "uppermedian" => UpperMedianTAGA.Instance,
            "dmedian" or "lowermedian" => LowerMedianTAGA.Instance,
            _ => throw new ArgumentException(
                     $"Unknown TAGA operator: '{operatorName}'. Valid names: max, min, mean, median, umedian, dmedian")
        };
        return new TAGAProcessor(op);
    }
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
    /// D-PAFC: largest admissible number of objects in intersection areas of the clusters;
    /// 0 = fully separated clusters (the principal allotment of the monograph).
    /// </summary>
    int MaxIntersections    = 0
);
