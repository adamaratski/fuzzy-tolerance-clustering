using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Core.Matrix;

/// <summary>
/// Builds a fuzzy tolerance matrix from raw data:
///   1. Normalize columns
///   2. Compute pairwise distances
///   3. T[i,j] = 1 - dist(i,j)
///   4. Optionally quantise T to the accuracy threshold ε (Viattchenin 2013, sec. 2.2.4):
///      values that differ by less than ε are merged into one α-level. Without ε the values
///      that differ by rounding noise only (≤ 10⁻¹²) are merged
///      (<see cref="FuzzyToleranceMatrix.SnapLevels"/>).
/// </summary>
/// <param name="distance">Distance between normalised objects.</param>
/// <param name="normalization">Column normalisation applied before the distance.</param>
/// <param name="accuracy">
/// Accuracy threshold ε ∈ (0,1]. When set, every entry of T is rounded to the nearest
/// multiple of ε (see <see cref="FuzzyToleranceMatrix.Quantize"/>). Null = full double precision.
/// </param>
public sealed class ToleranceMatrixBuilder(IDistance distance, INormalization normalization, double? accuracy = null)
{
    /// <summary>Accuracy threshold ε applied to the built matrix, or null for full precision.</summary>
    public double? Accuracy { get; } = accuracy;

    /// <summary>Returns the normalized form of <paramref name="data"/> as used internally by <see cref="Build"/>.</summary>
    public RawDataMatrix Normalize(RawDataMatrix data) => normalization.Apply(data);

    public FuzzyToleranceMatrix Build(RawDataMatrix data)
    {
        var normalized = normalization.Apply(data);
        int n = normalized.Rows;
        var matrix = new FuzzyToleranceMatrix(n);

        Parallel.For(0, n, i =>
        {
            var rowI = normalized.GetRow(i);
            for (int j = i + 1; j < n; j++)
            {
                double dist = distance.Compute(rowI, normalized.GetRow(j));
                double rel  = Math.Clamp(1.0 - dist, 0.0, 1.0);
                matrix[i, j] = rel; // setter maintains symmetry
            }
        });

        if (Accuracy is double eps) return matrix.Quantize(eps);

        // Full precision: values that differ only by rounding noise are one α-level.
        matrix.SnapLevels();
        return matrix;
    }
}
