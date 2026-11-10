using MathFuzzy.Clustering;
using MathFuzzy.Core.Asymmetric;
using MathFuzzy.Core.FuzzyData;
using MathFuzzy.Core.Interval;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;
using MathFuzzy.Core.ThreeWay;

namespace MathFuzzy.Cli.Commands;

/// <summary>The options of the kinds of input of <c>mfuzzy cluster</c> other than attribute data.</summary>
internal sealed record SpecialInputOptions(
    bool Interval, bool FuzzyNumbers, bool ThreeWay, bool Asymmetric,
    string? IntervalMeasure, double? Lambda, string? IntervalScaling,
    double? ShapeLeft, double? ShapeRight,
    string? ObjectColumn, string? SituationColumn, bool KeepSelfSimilarity,
    string? Symmetrize, string? Proximity,
    IReadOnlySet<string> GivenOptions)
{
    /// <summary>True when the input is one of the four kinds that are turned into a tolerance here.</summary>
    public bool Any => Interval || FuzzyNumbers || ThreeWay || Asymmetric;
}

/// <summary>Reads interval-valued, fuzzy-number, three-way and asymmetric data into a fuzzy tolerance matrix.</summary>
internal static class SpecialInputs
{
    private static readonly Dictionary<string, string> KindOfOption = new()
    {
        ["--interval-measure"] = "--interval", ["--lambda"] = "--interval", ["--interval-scaling"] = "--interval",
        ["--shape-left"] = "--fuzzy-numbers", ["--shape-right"] = "--fuzzy-numbers",
        ["--object-column"] = "--three-way", ["--situation-column"] = "--three-way", ["--keep-self-similarity"] = "--three-way",
        ["--symmetrize"] = "--asymmetric", ["--proximity"] = "--asymmetric",
    };

    /// <summary>Checks which options may be combined with the chosen kind of input.</summary>
    public static void Validate(ClusterRequest request, SpecialInputOptions special)
    {
        var chosen = new List<string>();
        if (request.InputIsMatrix) chosen.Add("--matrix");
        if (special.Interval) chosen.Add("--interval");
        if (special.FuzzyNumbers) chosen.Add("--fuzzy-numbers");
        if (special.ThreeWay) chosen.Add("--three-way");
        if (special.Asymmetric) chosen.Add("--asymmetric");
        if (request.NonMembership is not null) chosen.Add("--nonmembership");
        if (chosen.Count > 1)
            throw new ArgumentException($"{string.Join(", ", chosen)}: at most one of these kinds of input can be given.");

        string? kind = chosen.Count == 1 ? chosen[0] : null;
        var misplaced = special.GivenOptions
            .Where(name => KindOfOption.TryGetValue(name, out string? needed) && needed != kind)
            .Order().ToList();
        if (misplaced.Count > 0)
            throw new ArgumentException(
                $"{string.Join(", ", misplaced)}: these options need {string.Join(" / ", misplaced.Select(name => KindOfOption[name]).Distinct())}" +
                (kind is null ? "." : $" and are not available with {kind}."));
        if (!special.Any) return;

        if (AlgorithmFactory.IsIntuitionistic(request.Algorithm))
            throw new ArgumentException(
                $"{request.Algorithm}: the intuitionistic algorithms need intuitionistic fuzzy data and cannot be used with {kind}.");
        if (request.Rules is not null)
            throw new ArgumentException($"--rules needs attribute data: fuzzy rules are built on the attributes, not on {kind} data.");

        foreach (string name in new[] { "--distance", "--normalize", "--minkowski-p" }.Where(special.GivenOptions.Contains))
        {
            if (special.ThreeWay && name != "--minkowski-p") continue;
            throw new ArgumentException(
                $"{name}: options on attribute distances do not apply to {kind}" +
                (special.Interval ? " (use --interval-measure and --interval-scaling)." :
                 special.ThreeWay ? " (the distances of three-way data are hamming, euclidean, squared-euclidean)." : "."));
        }
        if (special.Interval && request.Accuracy.HasValue)
            throw new ArgumentException("--accuracy: the tolerance of interval-valued data is not rounded to an accuracy threshold.");
    }

    /// <summary>The fuzzy tolerance of the input file.</summary>
    public static FuzzyToleranceMatrix BuildTolerance(ClusterRequest request, SpecialInputOptions special)
    {
        string path = request.Input.FullName;
        if (special.Interval)
        {
            var data = IntervalDataMatrix.FromCsv(path);
            return IntervalTolerance.Build(
                data, IntervalTolerance.ParseMeasure(special.IntervalMeasure ?? "sqeuclid"),
                special.Lambda ?? 2.0, ParseScaling(special.IntervalScaling));
        }
        if (special.FuzzyNumbers)
        {
            var data = FuzzyNumberDataMatrix.FromCsv(path);
            return data.BuildTolerance(special.ShapeLeft ?? FuzzyNumberDistances.LinearShapeIntegral,
                                       special.ShapeRight ?? FuzzyNumberDistances.LinearShapeIntegral, request.Accuracy);
        }
        if (special.ThreeWay)
        {
            var data = ThreeWayDataMatrix.FromCsv(path, special.ObjectColumn, special.SituationColumn);
            string distanceName = special.GivenOptions.Contains("--distance") ? request.Distance : "squared-euclidean";
            var distance = distanceName.ToLowerInvariant().Trim() switch
            {
                "hamming" => ThreeWayDistance.Hamming,
                "euclidean" => ThreeWayDistance.Euclidean,
                "squared-euclidean" => ThreeWayDistance.SquaredEuclidean,
                _ => throw new ArgumentException(
                    $"Unknown distance of three-way data: '{distanceName}'. Valid names: hamming, euclidean, squared-euclidean")
            };
            INormalization normalization = request.Normalize.ToLowerInvariant().Trim() switch
            {
                "minmax" => MinMaxNormalization.Instance,
                "max" => MaxNormalization.Instance,
                "none" => NoNormalization.Instance,
                _ => throw new ArgumentException($"Unknown normalisation: '{request.Normalize}'. Valid names: minmax, max, none")
            };
            return data.BuildTolerance(normalization, distance, special.KeepSelfSimilarity, request.Accuracy);
        }

        var proximities = AsymmetricProximityMatrix.FromCsv(path);
        var method = (special.Symmetrize ?? "quasi-equivalence").ToLowerInvariant().Trim() switch
        {
            "quasi-equivalence" => SymmetrizationMethod.QuasiEquivalence,
            "likeness" => SymmetrizationMethod.Likeness,
            var other => throw new ArgumentException($"Unknown symmetrization: '{other}'. Valid names: quasi-equivalence, likeness")
        };
        var proximityKind = (special.Proximity ?? "auto").ToLowerInvariant().Trim() switch
        {
            "auto" => AsymmetricProximityKind.Auto,
            "dissimilarity" => AsymmetricProximityKind.Dissimilarity,
            "similarity" => AsymmetricProximityKind.Similarity,
            var other => throw new ArgumentException($"Unknown proximity kind: '{other}'. Valid names: auto, dissimilarity, similarity")
        };
        return proximities.BuildTolerance(method, proximityKind, request.Accuracy);
    }

    /// <summary>Scaling of interval bounds: minmax, max, none.</summary>
    public static IntervalScaling ParseScaling(string? name) => (name ?? "minmax").ToLowerInvariant().Trim() switch
    {
        "minmax" => IntervalScaling.MinMax,
        "max" => IntervalScaling.Maximum,
        "none" => IntervalScaling.None,
        _ => throw new ArgumentException($"Unknown scaling: '{name}'. Valid names: minmax, max, none")
    };
}
