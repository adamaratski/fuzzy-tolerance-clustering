using System.Globalization;
using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Asymmetric;

/// <summary>How the normalised asymmetric relation Ā is turned into a weak fuzzy preference relation R.</summary>
public enum AsymmetricProximityKind
{
    /// <summary>
    /// As the monograph states it: when Ā is antireflexive (a fuzzy intolerance, zero diagonal) it is
    /// complemented, R = 1 − Ā; when it is reflexive (unit diagonal) it is used as it is.
    /// </summary>
    Auto,

    /// <summary>The values are dissimilarities: R = 1 − Ā, whatever the diagonal.</summary>
    Dissimilarity,

    /// <summary>The values are similarities: R = Ā, whatever the diagonal.</summary>
    Similarity,
}

/// <summary>The symmetric fuzzy relation built from a weak fuzzy preference relation R (Viattchenin 2013, sec. 1.1.2).</summary>
public enum SymmetrizationMethod
{
    /// <summary>Fuzzy quasi-equivalence relation Q_ij = min(R_ij, R_ji), eq. (1.51).</summary>
    QuasiEquivalence,

    /// <summary>Fuzzy likeness relation L_ij = max(min(1 − R_ij, 1 − R_ji), min(R_ij, R_ji)), eq. (1.50).</summary>
    Likeness,
}

/// <summary>
/// A square, possibly asymmetric proximity matrix A = [a_ij] with non-negative entries, such as
/// dominance relations, communication flows or perception data (Viattchenin 2013, sec. 4.3). A fuzzy
/// tolerance is obtained from it in three steps (sec. 4.3.1):
/// <list type="number">
/// <item>normalisation by the maximum, Ā_ij = a_ij / max_kl a_kl;</item>
/// <item>when Ā is a fuzzy intolerance (antireflexive) relation, its complement R = 1 − Ā, a weak fuzzy preference relation;</item>
/// <item>the fuzzy quasi-equivalence relation Q or the fuzzy likeness relation L of R, both reflexive and symmetric.</item>
/// </list>
/// </summary>
public sealed class AsymmetricProximityMatrix
{
    private readonly double[,] _values;

    public int Size { get; }

    /// <summary>Names of the objects (from the file), or x1…xn.</summary>
    public IReadOnlyList<string> ObjectNames { get; }

    public AsymmetricProximityMatrix(double[,] values, IReadOnlyList<string>? objectNames = null)
    {
        Size = values.GetLength(0);
        if (Size == 0 || values.GetLength(1) != Size)
            throw new ArgumentException("A non-empty square matrix is expected.");
        for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
                if (!double.IsFinite(values[row, column]) || values[row, column] < 0.0)
                    throw new ArgumentException(
                        $"Value at row {row + 1}, column {column + 1}: the entries must be finite and non-negative.");
        if (objectNames is not null && objectNames.Count != Size)
            throw new ArgumentException("One name per object is expected.");

        _values = (double[,])values.Clone();
        ObjectNames = objectNames is null ? [.. Enumerable.Range(1, Size).Select(index => $"x{index}")] : [.. objectNames];
    }

    public double this[int row, int column] => _values[row, column];

    /// <summary>Ā_ij = a_ij / max_kl a_kl (step 1); a zero matrix stays zero.</summary>
    public double[,] Normalized()
    {
        double maximum = 0.0;
        foreach (double value in _values)
            if (value > maximum) maximum = value;
        var normalized = new double[Size, Size];
        for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
                normalized[row, column] = maximum > 0.0 ? _values[row, column] / maximum : 0.0;
        return normalized;
    }

    /// <summary>True when every diagonal entry is 0: the normalised matrix is a fuzzy intolerance relation.</summary>
    public bool IsAntireflexive
    {
        get
        {
            for (int index = 0; index < Size; index++)
                if (_values[index, index] != 0.0) return false;
            return true;
        }
    }

    /// <summary>
    /// The weak fuzzy preference relation R (step 2): the complement of Ā for dissimilarities,
    /// Ā itself for similarities. With <see cref="AsymmetricProximityKind.Auto"/> an antireflexive
    /// matrix is complemented and a matrix with unit normalised diagonal is used as it is; any other
    /// diagonal is ambiguous and needs an explicit kind.
    /// </summary>
    public double[,] WeakPreference(AsymmetricProximityKind kind = AsymmetricProximityKind.Auto)
    {
        var normalized = Normalized();
        bool complement = kind switch
        {
            AsymmetricProximityKind.Dissimilarity => true,
            AsymmetricProximityKind.Similarity => false,
            _ => IsAntireflexive ? true
                 : Enumerable.Range(0, Size).All(index => Math.Abs(normalized[index, index] - 1.0) <= 1e-12) ? false
                 : throw new InvalidOperationException(
                     "The matrix is neither antireflexive (zero diagonal) nor reflexive (diagonal equal to the maximum); " +
                     "state whether its entries are dissimilarities or similarities."),
        };
        if (!complement) return normalized;

        var preference = new double[Size, Size];
        for (int row = 0; row < Size; row++)
            for (int column = 0; column < Size; column++)
                preference[row, column] = 1.0 - normalized[row, column];
        return preference;
    }

    /// <summary>
    /// The fuzzy tolerance built from the weak fuzzy preference relation R (step 3): the fuzzy
    /// quasi-equivalence Q_ij = min(R_ij, R_ji) or the fuzzy likeness
    /// L_ij = max(min(1 − R_ij, 1 − R_ji), min(R_ij, R_ji)) (Viattchenin 2013, eqs. (1.50)–(1.51)).
    /// The diagonal is 1. With <paramref name="accuracy"/> the values are rounded to multiples of ε;
    /// otherwise values that differ by rounding noise only are merged.
    /// </summary>
    public FuzzyToleranceMatrix BuildTolerance(
        SymmetrizationMethod method = SymmetrizationMethod.QuasiEquivalence,
        AsymmetricProximityKind kind = AsymmetricProximityKind.Auto,
        double? accuracy = null)
    {
        var preference = WeakPreference(kind);
        var tolerance = new FuzzyToleranceMatrix(Size);
        for (int row = 0; row < Size; row++)
            for (int column = row + 1; column < Size; column++)
            {
                double forward = preference[row, column], backward = preference[column, row];
                double both = Math.Min(forward, backward);
                tolerance[row, column] = method == SymmetrizationMethod.QuasiEquivalence
                    ? both
                    : Math.Max(Math.Min(1.0 - forward, 1.0 - backward), both);
            }

        if (accuracy is double epsilon) return tolerance.Quantize(epsilon);
        tolerance.SnapLevels();
        return tolerance;
    }

    /// <summary>
    /// Loads a square n × n matrix of non-negative proximities from a delimited text file, read like
    /// <see cref="FuzzyToleranceMatrix.FromCsv"/> but without the symmetry and [0, 1] requirements:
    /// a header row and a first column of object names are recognised and skipped (the names are kept).
    /// </summary>
    public static AsymmetricProximityMatrix FromCsv(string path) => FromTable(CsvTable.Load(path));

    /// <summary>Reads the matrix from delimited text; see <see cref="FromCsv"/>.</summary>
    public static AsymmetricProximityMatrix Parse(string text, string sourceName = "input")
        => FromTable(CsvTable.Parse(text, sourceName));

    private static AsymmetricProximityMatrix FromTable(CsvTable table)
    {
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        int firstColumn = table.IsNumericColumn(0) ? 0 : 1;   // a first column of object names
        int size = table.Rows.Length;
        if (table.ColumnCount - firstColumn != size)
            throw new FormatException(
                $"{table.SourceName}: a square matrix is expected, found {size} rows and {table.ColumnCount - firstColumn} columns.");

        var values = new double[size, size];
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
            {
                double value = table.Number(row, column + firstColumn);
                if (value < 0.0)
                    throw new FormatException(
                        $"{table.SourceName}: value {value.ToString(CultureInfo.InvariantCulture)} at row {row + 1}, " +
                        $"column {column + 1} is negative; proximities must be non-negative.");
                values[row, column] = value;
            }

        IReadOnlyList<string>? names = firstColumn == 1
            ? [.. table.Rows.Select(row => row[0])]
            : table.Header is not null ? [.. table.Header] : null;
        return new AsymmetricProximityMatrix(values, names);
    }
}
