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
        var set = new SortedSet<double>(Comparer<double>.Create((a, b) => b.CompareTo(a)));
        for (int i = 0; i < Size; i++)
        for (int j = i + 1; j < Size; j++)
        {
            var v = _data[i * Size + j];
            if (v > 0.0 && v < 1.0) set.Add(v);
        }
        return set.ToList();
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
    /// Loads a square n×n tolerance matrix from a CSV file.
    /// Accepts files with or without a header row.
    /// Values must be in [0,1]; diagonal is forced to 1 after loading.
    /// </summary>
    public static FuzzyToleranceMatrix FromCsv(string path, char separator = ',')
    {
        var lines = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        // Detect and skip header: any line whose first token cannot be parsed as double.
        int dataStart = 0;
        var firstTokens = lines[0].Split(separator);
        if (!double.TryParse(firstTokens[0].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            dataStart = 1;

        var dataLines = lines[dataStart..];
        int size = dataLines.Length;
        if (size == 0) throw new FormatException("CSV contains no data rows.");

        var matrix = new FuzzyToleranceMatrix(size);
        for (int rowIndex = 0; rowIndex < size; rowIndex++)
        {
            var tokens = dataLines[rowIndex].Split(separator);
            if (tokens.Length < size)
                throw new FormatException(
                    $"Row {rowIndex + 1}: expected {size} values, got {tokens.Length}.");

            for (int colIndex = 0; colIndex < size; colIndex++)
            {
                if (!double.TryParse(tokens[colIndex].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double value))
                    throw new FormatException(
                        $"Row {rowIndex + 1}, column {colIndex + 1}: '{tokens[colIndex].Trim()}' is not a valid number.");

                matrix[rowIndex, colIndex] = Math.Clamp(value, 0.0, 1.0);
            }
        }

        // Enforce reflexivity: diagonal must be 1.
        for (int i = 0; i < size; i++)
            matrix[i, i] = 1.0;

        return matrix;
    }

    public double[] ToFlatArray() => (double[])_data.Clone();
}
