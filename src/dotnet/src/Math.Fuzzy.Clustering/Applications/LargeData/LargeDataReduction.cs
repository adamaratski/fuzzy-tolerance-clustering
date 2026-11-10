using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Applications.LargeData;

/// <summary>
/// The reduced data set of the reduction of large data sets: a hard partition of the objects into
/// classes G_1, …, G_ñ and, for every class, the interval vector of the minima and maxima of the
/// attributes over its members.
/// </summary>
/// <param name="ClassOfObject">0-based class index of every original object.</param>
/// <param name="ClassMembers">Original objects of every class, ascending.</param>
/// <param name="Lower">Lower bounds x̃^{t(min)}, one row per class (interval-valued object).</param>
/// <param name="Upper">Upper bounds x̃^{t(max)}, one row per class.</param>
public sealed record IntervalRepresentatives(
    int[] ClassOfObject,
    int[][] ClassMembers,
    RawDataMatrix Lower,
    RawDataMatrix Upper)
{
    /// <summary>Number ñ of interval-valued objects.</summary>
    public int Count => ClassMembers.Length;
}

/// <summary>Settings of <see cref="LargeDataReduction.Run(RawDataMatrix, LargeDataOptions, IRelationalClusterer, CancellationToken)"/>.</summary>
/// <param name="Reduce">
/// False switches the reduction off: every object is its own class, the interval vectors are
/// degenerate, and the result is that of the algorithm applied to the objects themselves.
/// </param>
/// <param name="RepresentativeCount">
/// Number ñ of classes of the first step. Null = the heuristic formula ñ = ⌊√n⌋ of the source.
/// </param>
/// <param name="Partition">
/// A hard partition supplied by the caller (class index of every object) instead of k-means;
/// "some clustering procedure" in the source. Any integer labels may be used; the classes are
/// renumbered in the order of their first object. When set, <see cref="RepresentativeCount"/> and
/// the k-means settings are ignored.
/// </param>
/// <param name="PartitionNormalization">
/// Normalisation of the data before k-means. Null = the data as given (the source does not state one).
/// </param>
/// <param name="KMeansRestarts">Number of k-means++ seedings; the partition with the smallest sum of squares is kept.</param>
/// <param name="KMeansSeed">Seed of the k-means++ seeding.</param>
/// <param name="KMeansMaxIterations">Largest number of Lloyd iterations per seeding.</param>
/// <param name="RepresentativeTolerance">
/// Builds the tolerance between the interval-valued objects from their lower and upper bounds.
/// Null = <see cref="LargeDataReduction.BurilloBustinceTolerance"/>.
/// </param>
public sealed record LargeDataOptions(
    bool Reduce = true,
    int? RepresentativeCount = null,
    int[]? Partition = null,
    INormalization? PartitionNormalization = null,
    int KMeansRestarts = 10,
    int KMeansSeed = 0,
    int KMeansMaxIterations = 300,
    Func<RawDataMatrix, RawDataMatrix, FuzzyToleranceMatrix>? RepresentativeTolerance = null);

/// <summary>Result of the clustering of a large data set through interval-valued representatives.</summary>
/// <param name="Representatives">The partition and the interval-valued objects.</param>
/// <param name="RepresentativeTolerance">Tolerance between the interval-valued objects that was clustered.</param>
/// <param name="RepresentativeResults">Allotments of the interval-valued objects, as ranked by the algorithm.</param>
/// <param name="ObjectResults">The same allotments carried over to the original objects, in the same order.</param>
public sealed record LargeDataResult(
    IntervalRepresentatives Representatives,
    FuzzyToleranceMatrix RepresentativeTolerance,
    IReadOnlyList<ClusterResult> RepresentativeResults,
    IReadOnlyList<ClusterResult> ObjectResults)
{
    /// <summary>The best allotment of all original objects, or <see cref="ClusterResult.Empty"/>.</summary>
    public ClusterResult Best => ObjectResults.Count > 0 ? ObjectResults[0] : ClusterResult.Empty;
}

/// <summary>
/// Reduction of a large data set before clustering (Viattchenin 2013, "A new approach to reduction
/// of large data set", CDAM'2013, sec. 2; book chapter 6, Large Data Reduction):
/// <list type="number">
/// <item>the objects are partitioned by a clustering procedure (k-means here) into ñ = ⌊√n⌋ classes;</item>
/// <item>for every class and attribute the minimum and the maximum over the members are computed;</item>
/// <item>the ñ interval vectors form the reduced data set.</item>
/// </list>
/// The source stops there. Following the book, the reduced data are clustered as interval-valued
/// data: a tolerance between the interval vectors is built (by default the complement of the
/// normalised Euclidean distance of Burillo and Bustince on the jointly normalised bounds, book
/// eq. interval-bb) and given to an existing relational algorithm. Each original object then
/// takes the memberships of the interval vector of its class; this carrying over is a choice of
/// this implementation, since neither the source nor the book defines memberships of the
/// original objects.
/// </summary>
public static class LargeDataReduction
{
    /// <summary>The heuristic number of representatives ñ = ⌊√n⌋ of the source (formula (2)).</summary>
    public static int RepresentativeCount(int objectCount)
    {
        if (objectCount < 0) throw new ArgumentOutOfRangeException(nameof(objectCount));
        int root = (int)Math.Sqrt(objectCount);
        while ((long)root * root > objectCount) root--;
        while ((long)(root + 1) * (root + 1) <= objectCount) root++;
        return root;
    }

    /// <summary>
    /// Steps 2 and 3 of the reduction: the interval vector [min, max] of every attribute over the
    /// members of every class of <paramref name="classOfObject"/>. Classes are numbered 0, 1, …;
    /// every number up to the largest must occur.
    /// </summary>
    public static IntervalRepresentatives BuildRepresentatives(RawDataMatrix data, int[] classOfObject)
    {
        if (classOfObject.Length != data.Rows)
            throw new ArgumentException($"The partition has {classOfObject.Length} entries for {data.Rows} objects.", nameof(classOfObject));
        if (data.Rows == 0)
            return new IntervalRepresentatives([], [], new RawDataMatrix(0, data.Cols), new RawDataMatrix(0, data.Cols));
        if (classOfObject.Any(label => label < 0))
            throw new ArgumentException("Class indices must be non-negative.", nameof(classOfObject));

        int classCount = classOfObject.Max() + 1;
        var members = Enumerable.Range(0, classCount).Select(_ => new List<int>()).ToArray();
        for (int objectIndex = 0; objectIndex < classOfObject.Length; objectIndex++)
            members[classOfObject[objectIndex]].Add(objectIndex);
        int empty = Array.FindIndex(members, list => list.Count == 0);
        if (empty >= 0)
            throw new ArgumentException($"Class {empty} of the partition has no objects.", nameof(classOfObject));

        int attributeCount = data.Cols;
        var lower = new RawDataMatrix(classCount, attributeCount);
        var upper = new RawDataMatrix(classCount, attributeCount);
        for (int classIndex = 0; classIndex < classCount; classIndex++)
        {
            for (int attribute = 0; attribute < attributeCount; attribute++)
            {
                double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
                foreach (int objectIndex in members[classIndex])
                {
                    double value = data[objectIndex, attribute];
                    if (value < minimum) minimum = value;
                    if (value > maximum) maximum = value;
                }
                lower[classIndex, attribute] = minimum;
                upper[classIndex, attribute] = maximum;
            }
        }
        return new IntervalRepresentatives([.. classOfObject], [.. members.Select(list => list.ToArray())], lower, upper);
    }

    /// <summary>
    /// Step 1 and steps 2–3 together: the partition (supplied or by k-means into ñ classes) and the
    /// interval-valued objects. With <see cref="LargeDataOptions.Reduce"/> = false every object is
    /// its own class.
    /// </summary>
    public static IntervalRepresentatives Reduce(RawDataMatrix data, LargeDataOptions options)
    {
        int[] partition;
        if (!options.Reduce)
            partition = [.. Enumerable.Range(0, data.Rows)];
        else if (options.Partition is not null)
            partition = KMeansPartition.Canonical(options.Partition);
        else if (data.Rows == 0)
            partition = [];
        else
        {
            int count = options.RepresentativeCount ?? RepresentativeCount(data.Rows);
            var clustered = options.PartitionNormalization?.Apply(data) ?? data;
            partition = KMeansPartition.Partition(clustered, Math.Clamp(count, 1, data.Rows),
                                                  options.KMeansRestarts, options.KMeansSeed, options.KMeansMaxIterations);
        }
        return BuildRepresentatives(data, partition);
    }

    /// <summary>
    /// Tolerance between interval-valued objects: T = 1 − d_I with the normalised Euclidean distance
    /// of Burillo and Bustince, d_I = ((1/2m) Σ_t [(μ^min_it − μ^min_jt)² + (μ^max_it − μ^max_jt)²])^{1/2},
    /// on bounds normalised attribute by attribute jointly over the objects and both bounds
    /// (book chapter 5, eqs. interval-normalise and interval-bb; Viattchenin 2011, eqs. (23), (25)).
    /// For degenerate intervals it equals 1 minus the normalised Euclidean distance of the min–max
    /// normalised data. Values that differ by rounding noise only are merged into one level.
    /// </summary>
    public static FuzzyToleranceMatrix BurilloBustinceTolerance(RawDataMatrix lower, RawDataMatrix upper)
    {
        if (lower.Rows != upper.Rows || lower.Cols != upper.Cols)
            throw new ArgumentException("The matrices of lower and upper bounds must have the same shape.");
        int objectCount = lower.Rows, attributeCount = lower.Cols;
        var normalisedLower = new RawDataMatrix(objectCount, attributeCount);
        var normalisedUpper = new RawDataMatrix(objectCount, attributeCount);
        for (int attribute = 0; attribute < attributeCount; attribute++)
        {
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                minimum = Math.Min(minimum, Math.Min(lower[objectIndex, attribute], upper[objectIndex, attribute]));
                maximum = Math.Max(maximum, Math.Max(lower[objectIndex, attribute], upper[objectIndex, attribute]));
            }
            double range = maximum - minimum;
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                normalisedLower[objectIndex, attribute] = range < 1e-12 ? 0.0 : (lower[objectIndex, attribute] - minimum) / range;
                normalisedUpper[objectIndex, attribute] = range < 1e-12 ? 0.0 : (upper[objectIndex, attribute] - minimum) / range;
            }
        }

        var tolerance = new FuzzyToleranceMatrix(objectCount);
        for (int row = 0; row < objectCount; row++)
        {
            for (int column = row + 1; column < objectCount; column++)
            {
                double sum = 0.0;
                for (int attribute = 0; attribute < attributeCount; attribute++)
                {
                    double lowerDifference = normalisedLower[row, attribute] - normalisedLower[column, attribute];
                    double upperDifference = normalisedUpper[row, attribute] - normalisedUpper[column, attribute];
                    sum += lowerDifference * lowerDifference + upperDifference * upperDifference;
                }
                double distance = attributeCount == 0 ? 0.0 : Math.Sqrt(sum / (2.0 * attributeCount));
                tolerance[row, column] = Math.Clamp(1.0 - distance, 0.0, 1.0);
            }
        }
        tolerance.SnapLevels();
        return tolerance;
    }

    /// <summary>
    /// The whole procedure: reduction, tolerance between the interval-valued objects, clustering
    /// of that tolerance by <paramref name="clusterer"/>, and assignment of every original object.
    /// </summary>
    public static LargeDataResult Run(RawDataMatrix data, LargeDataOptions options, IRelationalClusterer clusterer,
                                      CancellationToken ct = default)
        => Run(data, options, clusterer.Run, ct);

    /// <summary>
    /// As <see cref="Run(RawDataMatrix, LargeDataOptions, IRelationalClusterer, CancellationToken)"/>, with the
    /// algorithm given as a function of the tolerance matrix, for example
    /// <c>(tolerance, token) =&gt; AlgorithmFactory.Run("dafc", tolerance, algorithmOptions, token)</c>.
    /// </summary>
    public static LargeDataResult Run(RawDataMatrix data, LargeDataOptions options,
                                      Func<FuzzyToleranceMatrix, CancellationToken, IReadOnlyList<ClusterResult>> cluster,
                                      CancellationToken ct = default)
    {
        var representatives = Reduce(data, options);
        var toleranceBuilder = options.RepresentativeTolerance ?? BurilloBustinceTolerance;
        var tolerance = toleranceBuilder(representatives.Lower, representatives.Upper);
        IReadOnlyList<ClusterResult> representativeResults = representatives.Count < 2 ? [] : cluster(tolerance, ct);
        var objectResults = representativeResults.Select(result => ToObjects(data, representatives, result)).ToList();
        return new LargeDataResult(representatives, tolerance, representativeResults, objectResults);
    }

    /// <summary>
    /// Carries an allotment of the interval-valued objects over to the original objects: an object
    /// belongs to a cluster with the membership of its class, and the clusters consist of the
    /// members of their classes. The typical point of a cluster is the member of the typical class
    /// nearest (Euclidean, min–max normalised data) to the midpoint of the class's box, the
    /// smaller index on a tie; for a class of one object it is that object.
    /// </summary>
    public static ClusterResult ToObjects(RawDataMatrix data, IntervalRepresentatives representatives, ClusterResult representativeResult)
    {
        int objectCount = data.Rows, clusterCount = representativeResult.ClusterCount;
        var membership = new double[objectCount, clusterCount];
        for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
        {
            int classIndex = representatives.ClassOfObject[objectIndex];
            for (int cluster = 0; cluster < clusterCount; cluster++)
                membership[objectIndex, cluster] = representativeResult.Membership[classIndex, cluster];
        }

        var memberSets = representativeResult.MemberSets
            .Select(classes => classes.SelectMany(classIndex => representatives.ClassMembers[classIndex]).Order().ToArray())
            .ToArray();

        var normalised = MinMaxNormalization.Instance.Apply(data);
        var centers = representativeResult.Centers
            .Select(classIndex => NearestToMidpoint(normalised, representatives.ClassMembers[classIndex]))
            .ToArray();

        double[,]? nonMembership = null;
        if (representativeResult.NonMembership is { } classNonMembership)
        {
            nonMembership = new double[objectCount, clusterCount];
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
                for (int cluster = 0; cluster < clusterCount; cluster++)
                    nonMembership[objectIndex, cluster] = classNonMembership[representatives.ClassOfObject[objectIndex], cluster];
        }

        return representativeResult with
        {
            Centers = centers,
            MemberSets = memberSets,
            Membership = membership,
            NonMembership = nonMembership,
        };
    }

    private static int NearestToMidpoint(RawDataMatrix normalised, int[] members)
    {
        if (members.Length == 1) return members[0];
        int attributeCount = normalised.Cols;
        var midpoint = new double[attributeCount];
        for (int attribute = 0; attribute < attributeCount; attribute++)
        {
            double minimum = members.Min(objectIndex => normalised[objectIndex, attribute]);
            double maximum = members.Max(objectIndex => normalised[objectIndex, attribute]);
            midpoint[attribute] = (minimum + maximum) / 2.0;
        }

        int best = members[0];
        double bestDistance = double.PositiveInfinity;
        foreach (int objectIndex in members)   // ascending, so a tie keeps the smaller index
        {
            double distance = 0.0;
            for (int attribute = 0; attribute < attributeCount; attribute++)
            {
                double difference = normalised[objectIndex, attribute] - midpoint[attribute];
                distance += difference * difference;
            }
            if (distance < bestDistance) { bestDistance = distance; best = objectIndex; }
        }
        return best;
    }
}
