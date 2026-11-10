using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Interval;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Interval;

/// <summary>Setting of the technique that constructs the stable clustering structure.</summary>
/// <param name="Measure">Measure between interval-valued fuzzy sets for the tolerance of step 2.</param>
/// <param name="Lambda">Exponent λ of the Ju–Yuan measure.</param>
/// <param name="Scaling">Joint normalisation of the bounds before the measure (min–max, eq. (23) of the source).</param>
/// <param name="Norm">S-norm of the set of most possible numbers of clusters.</param>
/// <param name="Stability">Setting of the stability analysis of step 1.</param>
/// <param name="ValidityMeasure">Validity measure that selects c: qmf (default), lmf or msc.</param>
/// <param name="MaximizeValidity">
/// True (default) to select the largest value of the validity measure, as the source does with
/// V_QMF; false to select the smallest.
/// </param>
/// <param name="ClusterCounts">Numbers of clusters to try instead of the set of step 1 (null = step 1 decides).</param>
/// <param name="Criterion">Criterion of D-AFC(c); default F₁.</param>
public sealed record StableStructureOptions(
    IntervalMeasure           Measure          = IntervalMeasure.SquaredEuclidean,
    double                    Lambda           = 2.0,
    IntervalScaling           Scaling          = IntervalScaling.MinMax,
    SNorm                     Norm             = SNorm.Maximum,
    IntervalStabilityOptions? Stability        = null,
    string                    ValidityMeasure  = "qmf",
    bool                      MaximizeValidity = true,
    IReadOnlyList<int>?       ClusterCounts    = null,
    ICriterion?               Criterion        = null);

/// <summary>
/// D-AFC(c) for one number of clusters.
/// </summary>
/// <param name="ClusterCount">The number c.</param>
/// <param name="Allotment">The best allotment, or null when none exists.</param>
/// <param name="IsUnique">False when several allotments share the largest value of the criterion.</param>
/// <param name="Validity">Value of the validity measure of the best allotment (null without allotment).</param>
public sealed record ClusterCountCandidate(int ClusterCount, ClusterResult? Allotment, bool IsUnique, double? Validity)
{
    /// <summary>Whether the candidate takes part in the selection of c*.</summary>
    public bool IsAdmissible => Allotment is not null && IsUnique;
}

/// <summary>Result of the technique.</summary>
/// <param name="Stability">Step 1: the clusterings of the matrices of bounds and the type of structure (null when the numbers were given).</param>
/// <param name="Plausible">Step 1: the set of most possible numbers of clusters (null when the numbers were given).</param>
/// <param name="Tolerance">Step 2: the tolerance of the interval data.</param>
/// <param name="Candidates">Step 3: D-AFC(c) for every c tried, ascending in c.</param>
/// <param name="Selected">The allotment P* among c* clusters, or null when no c is admissible.</param>
public sealed record StableStructureResult(
    IntervalStabilityResult?            Stability,
    PlausibleClusterCounts?             Plausible,
    FuzzyToleranceMatrix                Tolerance,
    IReadOnlyList<ClusterCountCandidate> Candidates,
    ClusterResult?                      Selected);

/// <summary>
/// Construction of the stable clustering structure of interval-valued data (Viattchenin 2011,
/// sec. 3.2; 2013, sec. 3.4):
///   1. the set D_α̂ of most possible numbers of clusters from D-AFC-TC on the matrices of lower
///      and upper bounds (<see cref="IntervalStability"/>, <see cref="PlausibleClusterCounts"/>);
///   2. the tolerance T of the jointly normalised interval data (<see cref="IntervalTolerance"/>);
///   3. D-AFC(c) on T for every c ∈ D_α̂; the values of c for which the maximiser of the criterion is
///      not unique are discarded, and c* is selected by the validity measure — in the source the
///      quadratic measure of fuzziness V_QMF, maximised.
/// The result is called stable because one allotment is obtained for the interval data whatever
/// the type of the structure of the matrices of bounds.
/// </summary>
public static class StableStructureTechnique
{
    /// <summary>Values of the criterion that differ by no more than this are a tie.</summary>
    public const double TieTolerance = 1e-12;

    public static StableStructureResult Run(IntervalDataMatrix data, StableStructureOptions? options = null,
                                            CancellationToken ct = default)
    {
        options ??= new StableStructureOptions();
        IntervalStabilityResult? stability = null;
        PlausibleClusterCounts? plausible = null;
        IReadOnlyList<int> counts;
        if (options.ClusterCounts is { } given)
        {
            counts = [.. given.Distinct().Order()];
        }
        else
        {
            stability = IntervalStability.Analyze(data, options.Stability, ct);
            plausible = PlausibleClusterCounts.Compute(stability, data.Objects, options.Norm);
            counts = plausible.Counts;
        }

        var tolerance = IntervalTolerance.Build(data, options.Measure, options.Lambda, options.Scaling);
        var criterion = options.Criterion ?? F1Criterion.Instance;
        var candidates = counts.Select(count => RunForCount(tolerance, count, criterion, options.ValidityMeasure, ct)).ToList();

        var admissible = candidates.Where(candidate => candidate.IsAdmissible).ToList();
        ClusterResult? selected = null;
        if (admissible.Count > 0)
        {
            // ascending in c, so the smaller number of clusters wins a tie of the validity measure
            var best = admissible[0];
            foreach (var candidate in admissible.Skip(1))
            {
                double difference = candidate.Validity!.Value - best.Validity!.Value;
                if (options.MaximizeValidity ? difference > TieTolerance : difference < -TieTolerance)
                    best = candidate;
            }
            selected = best.Allotment;
        }
        return new StableStructureResult(stability, plausible, tolerance, candidates, selected);
    }

    /// <summary>
    /// D-AFC(c) on the tolerance (all levels, w relaxed from 0 as in the original algorithm) with
    /// the uniqueness of the maximiser and the validity of the best allotment.
    /// </summary>
    public static ClusterCountCandidate RunForCount(FuzzyToleranceMatrix tolerance, int clusterCount, ICriterion criterion,
                                                    string validityMeasure = "qmf", CancellationToken ct = default)
    {
        var algorithm = new DAfcAlgorithm(new DAfcOptions(clusterCount), AllDistinctAlphaSelector.Instance, criterion);
        var ranked = algorithm.Run(tolerance, ct);
        if (ranked.Count == 0) return new ClusterCountCandidate(clusterCount, null, false, null);

        var best = ranked[0];
        bool unique = ranked.Count < 2 || Math.Abs(ranked[1].Score - best.Score) > TieTolerance;
        double validity = AllotmentMeasures.Compute(best).Value(validityMeasure);
        return new ClusterCountCandidate(clusterCount, best, unique, validity);
    }
}
