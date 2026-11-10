using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Interval;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Algorithms.Interval;

/// <summary>
/// Types of clustering structure of three-way or interval-valued data (Viattchenin 2011,
/// sec. 1.2): every matrix of the data — for interval data the matrix of lower and the matrix of
/// upper bounds — is clustered by the same algorithm.
/// </summary>
public enum ClusteringStructure
{
    /// <summary>The number of clusters and the prototypes of the clusters are the same for every matrix.</summary>
    Stable,

    /// <summary>The number of clusters is the same for every matrix, the prototypes differ.</summary>
    QuasiStable,

    /// <summary>The number of clusters differs between the matrices.</summary>
    Unstable
}

/// <summary>How the two matrices of bounds are normalised before they are clustered.</summary>
public enum BoundNormalization
{
    /// <summary>
    /// Each matrix of bounds is normalised on its own (<see cref="IntervalDataMatrix.NormalizeSeparately"/>).
    /// This reading reproduces the published numbers of clusters of the oil and project-network
    /// examples (Viattchenin 2011, sec. 4.2; 2015, sec. 3).
    /// </summary>
    Separate,

    /// <summary>
    /// Both matrices are normalised jointly by the printed formula (Viattchenin 2011, eq. (23);
    /// <see cref="IntervalDataMatrix.NormalizeJointly"/>) and then split.
    /// </summary>
    Joint
}

/// <summary>Setting of the stability analysis.</summary>
/// <param name="Normalization">Separate (default, reproduces the published results) or joint normalisation of the bounds.</param>
/// <param name="Scaling">Scaling applied to the bounds: min–max (default) or by the maximum.</param>
/// <param name="Distance">
/// Distance between the normalised objects of one matrix of bounds for the built-in D-AFC-TC;
/// default the squared normalised Euclidean distance (Viattchenin 2011, sec. 3.1).
/// </param>
/// <param name="LeapForm">
/// Form of the leap heuristic of the built-in D-AFC-TC. The ratio form (default) reproduces the
/// published numbers of clusters of the oil data; the difference form does not.
/// </param>
/// <param name="Criterion">Criterion that ranks the typical points of the built-in D-AFC-TC; default F₁.</param>
/// <param name="Clusterer">
/// Optional prototype-based algorithm used instead of the built-in D-AFC-TC. It receives each
/// matrix of bounds after the normalisation chosen here and must not normalise again (use it with
/// <see cref="IntervalScaling.None"/> if it normalises itself); its best result is taken.
/// </param>
/// <param name="PrototypeTolerance">Prototypes whose coordinates differ by no more than this are the same.</param>
public sealed record IntervalStabilityOptions(
    BoundNormalization    Normalization      = BoundNormalization.Separate,
    IntervalScaling       Scaling            = IntervalScaling.MinMax,
    IDistance?            Distance           = null,
    LeapForm              LeapForm           = LeapForm.Ratio,
    ICriterion?           Criterion          = null,
    IPrototypeClusterer?  Clusterer          = null,
    double                PrototypeTolerance = 1e-9);

/// <summary>Which matrix of an interval data set.</summary>
public enum IntervalBound { Lower, Upper }

/// <summary>The clustering of one matrix of bounds.</summary>
/// <param name="Bound">The matrix: lower or upper bounds.</param>
/// <param name="Allotment">The allotment found by the algorithm (its threshold is <see cref="ClusterResult.Alpha"/>).</param>
/// <param name="Prototypes">
/// Prototype of every cluster, in the order of <see cref="ClusterResult.MemberSets"/>: the mean of
/// the bounds of its members, in the units of the data.
/// </param>
public sealed record BoundClustering(IntervalBound Bound, ClusterResult Allotment, IReadOnlyList<double[]> Prototypes)
{
    public int ClusterCount => Allotment.ClusterCount;
    public double Alpha => Allotment.Alpha;
}

/// <summary>Outcome of the stability analysis: the type of the structure and the allotments it was decided from.</summary>
public sealed record IntervalStabilityResult(ClusteringStructure Structure, BoundClustering Lower, BoundClustering Upper)
{
    public IReadOnlyList<BoundClustering> Bounds => [Lower, Upper];
}

/// <summary>
/// Classification of the clustering structure of interval-valued data (Viattchenin 2011,
/// sec. 1.2 and 3.1, step 1): the matrix of lower bounds X^min and the matrix of upper bounds
/// X^max are clustered by the same prototype-based algorithm — D-AFC-TC by default — and the
/// structure is stable when the numbers of clusters and the prototypes agree, quasi-stable when
/// only the numbers agree, unstable otherwise.
/// </summary>
public static class IntervalStability
{
    public static IntervalStabilityResult Analyze(IntervalDataMatrix data, IntervalStabilityOptions? options = null,
                                                  CancellationToken ct = default)
    {
        options ??= new IntervalStabilityOptions();
        RawDataMatrix lowerMatrix, upperMatrix;
        if (options.Normalization == BoundNormalization.Joint)
        {
            var jointly = data.NormalizeJointly(options.Scaling);
            (lowerMatrix, upperMatrix) = (jointly.LowerBounds(), jointly.UpperBounds());
        }
        else
        {
            (lowerMatrix, upperMatrix) = data.NormalizeSeparately(options.Scaling);
        }

        var lower = Cluster(IntervalBound.Lower, lowerMatrix, data.LowerBounds(), options, ct);
        var upper = Cluster(IntervalBound.Upper, upperMatrix, data.UpperBounds(), options, ct);
        return new IntervalStabilityResult(Classify(lower, upper, options.PrototypeTolerance), lower, upper);
    }

    /// <summary>The type of structure of two clusterings (see <see cref="ClusteringStructure"/>).</summary>
    public static ClusteringStructure Classify(BoundClustering lower, BoundClustering upper, double prototypeTolerance = 1e-9)
    {
        if (lower.ClusterCount != upper.ClusterCount) return ClusteringStructure.Unstable;
        return SamePrototypes(lower.Prototypes, upper.Prototypes, prototypeTolerance)
            ? ClusteringStructure.Stable
            : ClusteringStructure.QuasiStable;
    }

    /// <summary>
    /// D-AFC-TC on one normalised matrix of bounds with the leap form of the options: tolerance
    /// T = 1 − d, max-min transitive closure, threshold by the leap heuristic, best allotment of
    /// that level by the criterion.
    /// </summary>
    public static ClusterResult? DAfcTc(RawDataMatrix normalized, IDistance distance, LeapForm leapForm,
                                        ICriterion criterion, CancellationToken ct = default)
    {
        var tolerance = new ToleranceMatrixBuilder(distance, NoNormalization.Instance).Build(normalized);
        var closureMatrix = new MaxMinTransitiveClosure().Compute(tolerance);
        if (LeapHeuristicAlphaSelector.SelectFrom(closureMatrix.GetDistinctValues(), leapForm) is not double alpha)
            return null;
        var ranked = ClosureAllotment.Solve(closureMatrix, alpha, criterion, minClusterPower: 1, maxClusterPower: 0,
                                            normalized, maxResults: 1, ct);
        return ranked.Count > 0 ? ranked[0] : null;
    }

    private static BoundClustering Cluster(IntervalBound bound, RawDataMatrix normalized, RawDataMatrix original,
                                           IntervalStabilityOptions options, CancellationToken ct)
    {
        ClusterResult? allotment = options.Clusterer is { } clusterer
            ? clusterer.Run(normalized, ct).FirstOrDefault()
            : DAfcTc(normalized, options.Distance ?? SquaredEuclideanDistance.Instance, options.LeapForm,
                     options.Criterion ?? F1Criterion.Instance, ct);
        if (allotment is null)
            throw new InvalidOperationException(
                $"The algorithm found no allotment for the matrix of {(bound == IntervalBound.Lower ? "lower" : "upper")} bounds.");
        return new BoundClustering(bound, allotment, Prototypes(allotment, original));
    }

    /// <summary>Mean of the members of every cluster, in the units of the data.</summary>
    public static IReadOnlyList<double[]> Prototypes(ClusterResult allotment, RawDataMatrix data)
    {
        var prototypes = new double[allotment.ClusterCount][];
        for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
        {
            var members = allotment.MemberSets[cluster];
            var prototype = new double[data.Cols];
            foreach (int member in members)
                for (int attribute = 0; attribute < data.Cols; attribute++)
                    prototype[attribute] += data[member, attribute];
            for (int attribute = 0; attribute < data.Cols; attribute++)
                prototype[attribute] /= members.Length;
            prototypes[cluster] = prototype;
        }
        return prototypes;
    }

    /// <summary>True when every prototype of one list equals a distinct prototype of the other.</summary>
    private static bool SamePrototypes(IReadOnlyList<double[]> first, IReadOnlyList<double[]> second, double tolerance)
    {
        if (first.Count != second.Count) return false;
        var used = new bool[second.Count];
        foreach (var prototype in first)
        {
            int match = -1;
            for (int candidate = 0; candidate < second.Count && match < 0; candidate++)
                if (!used[candidate] && prototype.Zip(second[candidate]).All(pair => Math.Abs(pair.First - pair.Second) <= tolerance))
                    match = candidate;
            if (match < 0) return false;
            used[match] = true;
        }
        return true;
    }
}
