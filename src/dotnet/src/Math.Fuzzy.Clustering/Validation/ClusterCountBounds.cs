namespace MathFuzzy.Clustering.Validation;

/// <summary>Shape of the fuzzy number that models the potential number of clusters.</summary>
public enum ClusterCountShape
{
    Triangular,
    Gaussian
}

/// <summary>
/// The set of potential numbers of fuzzy clusters (Viattchenin, Yaroma and Damaratski 2016).
/// </summary>
/// <param name="Least">c_min: the least potential number of clusters.</param>
/// <param name="Largest">c_max: the largest potential number of clusters.</param>
/// <param name="Possibility">Possibility degree μ(g) of every g = Least, …, Largest, in order.</param>
public sealed record ClusterCountBoundsResult(int Least, int Largest, IReadOnlyList<double> Possibility);

/// <summary>
/// Bounds of the set of potential numbers of fuzzy clusters in a sought clustering structure
/// (Viattchenin, Yaroma and Damaratski 2016). A single run of an algorithm that finds the
/// number of clusters itself gives c fully separated clusters at the threshold α. The number of
/// clusters is then modelled by a fuzzy number on {1, …, n} with the mode c, and the set of
/// potential numbers is its α-level restricted to {2, …, n − 1}.
/// </summary>
public static class ClusterCountBounds
{
    /// <summary>
    /// Bounds for n objects, c clusters found at the threshold α.
    /// Triangular number: μ(g) = 1 − (c − g)/(c − 1) for g &lt; c and 1 − (g − c)/(n − c) for g ≥ c.
    /// Gaussian number: μ(g) = exp(−½((g − c)/σ)²) with σ = max(σ_L, σ_R), σ_L = (1 − α)(c − 1)/k,
    /// σ_R = (1 − α)(n − c)/k, k = √(−2 ln(1 − α)); the spreads are anchored at the points where
    /// the triangular number takes the value α, which reproduces the published example.
    /// </summary>
    public static ClusterCountBoundsResult Compute(int objectCount, int clusterCount, double alpha, ClusterCountShape shape)
    {
        if (objectCount < 3)
            throw new ArgumentException("At least three objects are needed.");
        if (clusterCount < 2 || clusterCount >= objectCount)
            throw new ArgumentException($"The number of clusters must satisfy 2 ≤ c < n = {objectCount}; got {clusterCount}.");
        if (!(alpha > 0.0 && alpha < 1.0))
            throw new ArgumentException("The threshold α must lie strictly between 0 and 1.");

        Func<int, double> possibility = shape switch
        {
            ClusterCountShape.Triangular => count => Triangular(count, objectCount, clusterCount),
            ClusterCountShape.Gaussian   => Gaussian(objectCount, clusterCount, alpha),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        const double comparisonTolerance = 1e-12;
        int least = clusterCount, largest = clusterCount;
        while (least - 1 >= 2 && possibility(least - 1) >= alpha - comparisonTolerance) least--;
        while (largest + 1 <= objectCount - 1 && possibility(largest + 1) >= alpha - comparisonTolerance) largest++;

        var degrees = Enumerable.Range(least, largest - least + 1).Select(possibility).ToArray();
        return new ClusterCountBoundsResult(least, largest, degrees);
    }

    /// <summary>
    /// Possibility degree μ(g) that the structure has <paramref name="count"/> clusters, for the
    /// fuzzy number built from c clusters found at the threshold α (see <see cref="Compute"/>).
    /// </summary>
    public static double Possibility(int objectCount, int clusterCount, double alpha, ClusterCountShape shape, int count) =>
        shape == ClusterCountShape.Triangular
            ? Math.Max(0.0, Triangular(count, objectCount, clusterCount))
            : Gaussian(objectCount, clusterCount, alpha)(count);

    private static double Triangular(int count, int objectCount, int clusterCount) =>
        count < clusterCount
            ? 1.0 - (double)(clusterCount - count) / (clusterCount - 1)
            : 1.0 - (double)(count - clusterCount) / (objectCount - clusterCount);

    private static Func<int, double> Gaussian(int objectCount, int clusterCount, double alpha)
    {
        double scale = Math.Sqrt(-2.0 * Math.Log(1.0 - alpha));
        double leftSpread = (1.0 - alpha) * (clusterCount - 1) / scale;
        double rightSpread = (1.0 - alpha) * (objectCount - clusterCount) / scale;
        double spread = Math.Max(leftSpread, rightSpread);
        return count =>
        {
            double standardised = (count - clusterCount) / spread;
            return Math.Exp(-0.5 * standardised * standardised);
        };
    }
}
