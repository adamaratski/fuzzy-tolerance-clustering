namespace MathFuzzy.Clustering.Algorithms.Interval;

/// <summary>S-norms that combine the fuzzy numbers of clusters (Viattchenin 2011, sec. 3.1).</summary>
public enum SNorm
{
    /// <summary>S₁(p, q) = max(p, q).</summary>
    Maximum,

    /// <summary>S₂(p, q) = p + q − p·q.</summary>
    ProbabilisticSum,

    /// <summary>S₃(p, q) = min(1, p + q).</summary>
    BoundedSum
}

/// <summary>
/// The set of most possible numbers of fuzzy clusters (Viattchenin 2011, sec. 3.1, eqs. (30)–(33)).
/// </summary>
/// <param name="Possibility">π(c) = μ_D(c) for c = 2, …, n − 1, indexed by c (entries 0, 1 and n.. are absent).</param>
/// <param name="Level">The cut level α̂ = min over the matrices of their thresholds.</param>
/// <param name="Counts">D_α̂ = {c : μ_D(c) ≥ α̂}, ascending.</param>
public sealed record PlausibleClusterCounts(IReadOnlyDictionary<int, double> Possibility, double Level, IReadOnlyList<int> Counts)
{
    /// <summary>Values that differ by no more than this are equal when the cut is made.</summary>
    public const double Tolerance = 1e-12;

    /// <summary>
    /// The procedure of the source for the numbers of clusters c^(s) and thresholds α^(s) found on
    /// the matrices of the data:
    ///   1. the distinct numbers c^(s) are taken;
    ///   2. each becomes the LR fuzzy number V = (m, a, b) with m = c, a = m − 1, b = n − m, so
    ///      that μ_V(1) = μ_V(n) = 0, with the triangular shape functions L(u) = R(u) = max(0, 1 − u)
    ///      (the source does not state L and R; the triangular ones reproduce its Fig. 1);
    ///   3. each V is restricted to the integers 2, …, n − 1;
    ///   4. the restrictions are combined by the S-norm;
    ///   5. the union is cut at α̂ = min α^(s).
    /// </summary>
    public static PlausibleClusterCounts Compute(
        IReadOnlyList<(int ClusterCount, double Alpha)> clusterings, int objectCount, SNorm norm = SNorm.Maximum)
    {
        if (clusterings.Count == 0) throw new ArgumentException("At least one clustering is needed.", nameof(clusterings));
        if (objectCount < 3) throw new ArgumentOutOfRangeException(nameof(objectCount), "At least three objects are needed.");

        var distinctCounts = clusterings.Select(clustering => clustering.ClusterCount).Distinct().Order().ToArray();
        double level = clusterings.Min(clustering => clustering.Alpha);

        var possibility = new SortedDictionary<int, double>();
        for (int candidate = 2; candidate <= objectCount - 1; candidate++)
        {
            double combined = 0.0;
            foreach (int mean in distinctCounts)
                combined = Combine(combined, Triangular(candidate, mean, mean - 1, objectCount - mean), norm);
            possibility[candidate] = combined;
        }

        var counts = possibility.Where(entry => entry.Value >= level - Tolerance).Select(entry => entry.Key).ToArray();
        return new PlausibleClusterCounts(possibility, level, counts);
    }

    /// <summary>The set from the result of the stability analysis.</summary>
    public static PlausibleClusterCounts Compute(IntervalStabilityResult stability, int objectCount, SNorm norm = SNorm.Maximum) =>
        Compute([.. stability.Bounds.Select(bound => (bound.ClusterCount, bound.Alpha))], objectCount, norm);

    /// <summary>Membership of z in the triangular LR number (m, a, b).</summary>
    public static double Triangular(double value, double mean, double leftSpread, double rightSpread)
    {
        if (value == mean) return 1.0;
        if (value < mean) return leftSpread <= 0.0 ? 0.0 : Math.Max(0.0, 1.0 - (mean - value) / leftSpread);
        return rightSpread <= 0.0 ? 0.0 : Math.Max(0.0, 1.0 - (value - mean) / rightSpread);
    }

    public static double Combine(double first, double second, SNorm norm) => norm switch
    {
        SNorm.Maximum => Math.Max(first, second),
        SNorm.ProbabilisticSum => first + second - first * second,
        SNorm.BoundedSum => Math.Min(1.0, first + second),
        _ => throw new ArgumentOutOfRangeException(nameof(norm))
    };

    /// <summary>Parses an S-norm name: max, probsum, boundedsum.</summary>
    public static SNorm ParseNorm(string name) => name.Trim().ToLowerInvariant() switch
    {
        "max" or "maximum" => SNorm.Maximum,
        "probsum" or "probabilistic-sum" => SNorm.ProbabilisticSum,
        "boundedsum" or "bounded-sum" => SNorm.BoundedSum,
        _ => throw new ArgumentException($"Unknown S-norm: '{name}'. Valid names: max, probsum, boundedsum")
    };
}
