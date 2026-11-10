using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Interval;

/// <summary>
/// Measures between interval-valued fuzzy sets used to build a fuzzy tolerance from
/// interval-valued data (Viattchenin 2011, eqs. (24)–(29); 2012, eqs. (10)–(13); 2015,
/// eqs. (18)–(21)). μ^min and μ^max denote the normalised bounds, m the number of attributes.
/// </summary>
public enum IntervalMeasure
{
    /// <summary>
    /// Normalised Euclidean distance of Burillo and Bustince:
    /// d = ((1/2m)·Σ_t [(μ^min_it − μ^min_jt)² + (μ^max_it − μ^max_jt)²])^{1/2}; T = 1 − d.
    /// </summary>
    BurilloBustince,

    /// <summary>
    /// Normalised Euclidean distance based on the Hausdorff metric (Grzegorzewski):
    /// e = ((1/m)·Σ_t max{(μ^min_it − μ^min_jt)², (μ^max_it − μ^max_jt)²})^{1/2}; T = 1 − e.
    /// </summary>
    Hausdorff,

    /// <summary>
    /// Similarity measure of Ju and Yuan on the interval midpoints, λ ≥ 1:
    /// s = 1 − ((1/m)·Σ_t |(μ^min_it + μ^max_it)/2 − (μ^min_jt + μ^max_jt)/2|^λ)^{1/λ}; T = s.
    /// </summary>
    JuYuan,

    /// <summary>
    /// The squared normalised Euclidean distance generalised to type-two fuzzy sets, with every
    /// bound of x_i compared with every bound of x_j (cross-bound form):
    /// ε = (1/m)·Σ_t (1/4)·Σ_{s,s'} (μ^s_it − μ^{s'}_jt)²; T = 1 − ε.
    /// The measure is not zero on the diagonal, ε(x_i, x_i) = (1/2m)·Σ_t (μ^max_it − μ^min_it)²,
    /// so T is a feeble tolerance whose diagonal is kept (the clusters are subnormal). This is the
    /// form that reproduces the published results (Viattchenin 2011, sec. 4.2; 2015, sec. 3).
    /// </summary>
    SquaredEuclidean,

    /// <summary>The square root of <see cref="SquaredEuclidean"/>: T = 1 − √ε (Viattchenin 2015, eq. (21)).</summary>
    Euclidean,

    /// <summary>
    /// The form printed in the sources, with matching bounds only:
    /// ε = (1/m)·Σ_t (1/4)·Σ_s (μ^s_it − μ^s_jt)²; T = 1 − ε, reflexive. It does not reproduce the
    /// published results and is offered for comparison.
    /// </summary>
    SquaredEuclideanMatchingBounds,

    /// <summary>The square root of <see cref="SquaredEuclideanMatchingBounds"/>: T = 1 − √ε.</summary>
    EuclideanMatchingBounds
}

/// <summary>
/// Construction of a fuzzy tolerance from interval-valued data (Viattchenin 2011, sec. 3.2,
/// step 2): the bounds are normalised jointly (<see cref="IntervalDataMatrix.NormalizeJointly"/>)
/// and the tolerance is T = 1 − d for a distance, T = s for the similarity measure.
/// </summary>
public static class IntervalTolerance
{
    /// <summary>
    /// Builds the tolerance of the interval data. The data are normalised with
    /// <paramref name="scaling"/> jointly over both bounds before the measure is applied (pass
    /// <see cref="IntervalScaling.None"/> for data that are already in [0, 1]). For the
    /// cross-bound measures the diagonal 1 − ε(x_i, x_i) is kept, otherwise it is 1. Values that
    /// differ by rounding noise only are merged into one level.
    /// </summary>
    public static FuzzyToleranceMatrix Build(
        IntervalDataMatrix data, IntervalMeasure measure,
        double lambda = 2.0, IntervalScaling scaling = IntervalScaling.MinMax)
    {
        if (measure == IntervalMeasure.JuYuan && (lambda < 1.0 || !double.IsFinite(lambda)))
            throw new ArgumentOutOfRangeException(nameof(lambda), "The exponent λ of the Ju–Yuan measure must satisfy 1 ≤ λ < ∞.");

        var normalized = data.NormalizeJointly(scaling);
        int objects = normalized.Objects;
        var values = new double[objects, objects];
        for (int row = 0; row < objects; row++)
            for (int column = row; column < objects; column++)
            {
                double similarity = row == column && !IsCrossBound(measure)
                    ? 1.0
                    : Math.Clamp(Similarity(normalized, row, column, measure, lambda), 0.0, 1.0);
                values[row, column] = values[column, row] = similarity;
            }

        var matrix = FuzzyToleranceMatrix.FromArray(values);
        matrix.SnapLevels();
        return matrix;
    }

    /// <summary>True for the measures that compare every bound with every bound (non-unit diagonal).</summary>
    public static bool IsCrossBound(IntervalMeasure measure) =>
        measure is IntervalMeasure.SquaredEuclidean or IntervalMeasure.Euclidean;

    /// <summary>The value of T(x_i, x_j) for two objects of normalised interval data.</summary>
    public static double Similarity(IntervalDataMatrix normalized, int first, int second, IntervalMeasure measure, double lambda = 2.0)
    {
        int attributes = normalized.Attributes;
        double sum = 0.0;
        for (int attribute = 0; attribute < attributes; attribute++)
        {
            double firstLower = normalized.Lower(first, attribute), firstUpper = normalized.Upper(first, attribute);
            double secondLower = normalized.Lower(second, attribute), secondUpper = normalized.Upper(second, attribute);
            double lowerDifference = firstLower - secondLower, upperDifference = firstUpper - secondUpper;
            sum += measure switch
            {
                IntervalMeasure.BurilloBustince => lowerDifference * lowerDifference + upperDifference * upperDifference,
                IntervalMeasure.Hausdorff => Math.Max(lowerDifference * lowerDifference, upperDifference * upperDifference),
                IntervalMeasure.JuYuan => Math.Pow(Math.Abs(0.5 * (firstLower + firstUpper) - 0.5 * (secondLower + secondUpper)), lambda),
                IntervalMeasure.SquaredEuclidean or IntervalMeasure.Euclidean =>
                    0.25 * (Square(firstLower - secondLower) + Square(firstLower - secondUpper)
                          + Square(firstUpper - secondLower) + Square(firstUpper - secondUpper)),
                IntervalMeasure.SquaredEuclideanMatchingBounds or IntervalMeasure.EuclideanMatchingBounds =>
                    0.25 * (lowerDifference * lowerDifference + upperDifference * upperDifference),
                _ => throw new ArgumentOutOfRangeException(nameof(measure))
            };
        }

        return measure switch
        {
            IntervalMeasure.BurilloBustince => 1.0 - Math.Sqrt(sum / (2.0 * attributes)),
            IntervalMeasure.Hausdorff => 1.0 - Math.Sqrt(sum / attributes),
            IntervalMeasure.JuYuan => 1.0 - Math.Pow(sum / attributes, 1.0 / lambda),
            IntervalMeasure.SquaredEuclidean or IntervalMeasure.SquaredEuclideanMatchingBounds => 1.0 - sum / attributes,
            _ => 1.0 - Math.Sqrt(sum / attributes)
        };
    }

    private static double Square(double value) => value * value;

    /// <summary>Parses a measure name: bb, hausdorff, juyuan, sqeuclid, euclid, sqeuclid-matching, euclid-matching.</summary>
    public static IntervalMeasure ParseMeasure(string name) => name.Trim().ToLowerInvariant() switch
    {
        "bb" or "burillo-bustince" => IntervalMeasure.BurilloBustince,
        "hausdorff" => IntervalMeasure.Hausdorff,
        "juyuan" or "ju-yuan" => IntervalMeasure.JuYuan,
        "sqeuclid" or "squared-euclidean" => IntervalMeasure.SquaredEuclidean,
        "euclid" or "euclidean" => IntervalMeasure.Euclidean,
        "sqeuclid-matching" => IntervalMeasure.SquaredEuclideanMatchingBounds,
        "euclid-matching" => IntervalMeasure.EuclideanMatchingBounds,
        _ => throw new ArgumentException(
            $"Unknown interval measure: '{name}'. Valid names: bb, hausdorff, juyuan, sqeuclid, euclid, sqeuclid-matching, euclid-matching")
    };
}
