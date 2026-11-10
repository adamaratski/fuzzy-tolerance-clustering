namespace MathFuzzy.Core.Matrix;

/// <summary>
/// Symmetric n×n fuzzy tolerance relation matrix. Values in [0,1].
/// Diagonal is 1 (reflexivity). Stored row-major as double[].
/// </summary>
public sealed class FuzzyToleranceMatrix
{
    private readonly double[] _data;

    public int Size { get; }

    public FuzzyToleranceMatrix(int size)
    {
        Size = size;
        _data = new double[size * size];
        for (int i = 0; i < size; i++)
            _data[i * size + i] = 1.0;
    }

    private FuzzyToleranceMatrix(double[] data, int size)
    {
        _data = data;
        Size = size;
    }

    public double this[int i, int j]
    {
        get => _data[i * Size + j];
        set
        {
            _data[i * Size + j] = value;
            _data[j * Size + i] = value; // maintain symmetry
        }
    }

    public ReadOnlySpan<double> GetRow(int i)
        => _data.AsSpan(i * Size, Size);

    public Span<double> GetRowMutable(int i)
        => _data.AsSpan(i * Size, Size);

    /// <summary>All distinct values in (0,1) excluding diagonal, descending.</summary>
    public IReadOnlyList<double> GetDistinctValues()
    {
        var values = new double[Size * (Size - 1) / 2];
        int count = 0;
        for (int i = 0; i < Size; i++)
        for (int j = i + 1; j < Size; j++)
        {
            var v = _data[i * Size + j];
            if (v > 0.0 && v < 1.0) values[count++] = v;
        }
        Array.Sort(values, 0, count);

        var distinct = new List<double>();
        for (int position = count - 1; position >= 0; position--)
            if (distinct.Count == 0 || values[position] != distinct[^1])
                distinct.Add(values[position]);
        return distinct;
    }

    /// <summary>
    /// Returns a copy in which every off-diagonal entry is rounded to the nearest multiple
    /// of the accuracy threshold <paramref name="epsilon"/> (Viattchenin 2013, sec. 2.2.4):
    /// entries that differ by less than ε collapse into one α-level, which coarsens the
    /// level sequence, raises the selected threshold and enlarges cluster kernels.
    /// The diagonal stays equal to 1; the result is clamped to [0,1].
    /// </summary>
    public FuzzyToleranceMatrix Quantize(double epsilon)
    {
        if (epsilon <= 0.0 || epsilon > 1.0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Accuracy threshold must lie in (0,1].");

        var result = Clone();
        for (int i = 0; i < Size; i++)
        {
            for (int j = i + 1; j < Size; j++)
            {
                double k = Math.Round(this[i, j] / epsilon, MidpointRounding.AwayFromZero);
                result[i, j] = Math.Clamp(k * epsilon, 0.0, 1.0); // setter keeps symmetry
            }
        }
        return result;
    }

    public FuzzyToleranceMatrix Clone()
        => new((double[])_data.Clone(), Size);

    public static FuzzyToleranceMatrix FromArray(double[,] data)
    {
        int n = data.GetLength(0);
        var flat = new double[n * n];
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
            flat[i * n + j] = data[i, j];
        return new FuzzyToleranceMatrix(flat, n);
    }

    /// <summary>
    /// Levels that differ by less than this are one level. Distances accumulated in a different
    /// order differ in the last bits, which would otherwise split one α-level of the data in two.
    /// </summary>
    public const double DefaultLevelTolerance = 1e-12;

    /// <summary>
    /// Merges off-diagonal values that differ by no more than <paramref name="tolerance"/> into a
    /// single level, in place: the sorted values are swept, a gap larger than the tolerance starts
    /// a new level, and every value of a level is replaced by the smallest one. Returns the number
    /// of levels removed. A tolerance of 0 leaves the matrix untouched.
    /// </summary>
    public int SnapLevels(double tolerance = DefaultLevelTolerance)
    {
        if (tolerance <= 0.0 || Size < 2) return 0;

        var values = new double[Size * (Size - 1) / 2];
        int count = 0;
        for (int row = 0; row < Size; row++)
            for (int column = row + 1; column < Size; column++)
                values[count++] = _data[row * Size + column];
        Array.Sort(values);

        // representative[value] for the values that change
        Dictionary<double, double>? representative = null;
        double levelStart = values[0];
        int removed = 0;
        for (int position = 1; position < values.Length; position++)
        {
            double value = values[position];
            if (value == values[position - 1]) continue;
            if (value - values[position - 1] > tolerance) { levelStart = value; continue; }
            (representative ??= [])[value] = levelStart;
            removed++;
        }
        if (representative is null) return 0;

        for (int index = 0; index < _data.Length; index++)
            if (representative.TryGetValue(_data[index], out double snapped))
                _data[index] = snapped;
        return removed;
    }

    /// <summary>
    /// Loads a square n × n fuzzy tolerance matrix from a delimited text file. A header row and
    /// a first column of object names are skipped. The values must lie in [0, 1] and the matrix
    /// must be symmetric; the diagonal is set to 1.
    /// </summary>
    public static FuzzyToleranceMatrix FromCsv(string path)
    {
        var table = MathFuzzy.Core.IO.CsvTable.Load(path);
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        int firstColumn = table.IsNumericColumn(0) ? 0 : 1;   // a first column of object names
        int size = table.Rows.Length;
        if (table.ColumnCount - firstColumn != size)
            throw new FormatException(
                $"{table.SourceName}: a square matrix is expected, found {size} rows and {table.ColumnCount - firstColumn} columns.");

        var matrix = new FuzzyToleranceMatrix(size);
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                double value = table.Number(row, column + firstColumn);
                if (value < 0.0 || value > 1.0)
                    throw new FormatException(
                        $"{table.SourceName}: value {value.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                        $"at row {row + 1}, column {column + 1} lies outside [0, 1]; a fuzzy tolerance matrix is expected.");
                matrix._data[row * size + column] = value;
            }
        }

        for (int row = 0; row < size; row++)
        {
            for (int column = row + 1; column < size; column++)
            {
                if (System.Math.Abs(matrix._data[row * size + column] - matrix._data[column * size + row]) > 1e-9)
                    throw new FormatException(
                        $"{table.SourceName}: the matrix is not symmetric at ({row + 1}, {column + 1}).");
                matrix._data[column * size + row] = matrix._data[row * size + column];
            }
            matrix._data[row * size + row] = 1.0;
        }

        matrix.SnapLevels();
        return matrix;
    }

    public double[] ToFlatArray() => (double[])_data.Clone();
}
