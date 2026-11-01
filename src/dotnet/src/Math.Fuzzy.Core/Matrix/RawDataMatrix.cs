namespace MathFuzzy.Core.Matrix;

/// <summary>
/// Immutable n×m matrix of raw object attributes. Stored row-major as double[].
/// </summary>
public sealed class RawDataMatrix
{
    private readonly double[] _data;

    public int Rows { get; }
    public int Cols { get; }

    public RawDataMatrix(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;
        _data = new double[rows * cols];
    }

    private RawDataMatrix(double[] data, int rows, int cols)
    {
        _data = data;
        Rows = rows;
        Cols = cols;
    }

    public double this[int row, int col]
    {
        get => _data[row * Cols + col];
        set => _data[row * Cols + col] = value;
    }

    public ReadOnlySpan<double> GetRow(int row)
        => _data.AsSpan(row * Cols, Cols);

    public RawDataMatrix Transpose()
    {
        var result = new RawDataMatrix(Cols, Rows);
        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Cols; j++)
            result[j, i] = this[i, j];
        return result;
    }

    public RawDataMatrix SelectColumns(IReadOnlyList<int> columnIndices)
    {
        var result = new RawDataMatrix(Rows, columnIndices.Count);
        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < columnIndices.Count; j++)
            result[i, j] = this[i, columnIndices[j]];
        return result;
    }

    public static RawDataMatrix FromArray(double[,] data)
    {
        int rows = data.GetLength(0), cols = data.GetLength(1);
        var flat = new double[rows * cols];
        for (int i = 0; i < rows; i++)
        for (int j = 0; j < cols; j++)
            flat[i * cols + j] = data[i, j];
        return new RawDataMatrix(flat, rows, cols);
    }

    public static RawDataMatrix FromCsv(string path, char separator = ',')
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        // Skip header row if the first cell is not a number
        int start = 0;
        if (lines.Length > 0)
        {
            var firstCell = lines[0].Split(separator)[0].Trim();
            if (!double.TryParse(firstCell, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                start = 1;
        }

        var dataLines = lines[start..];
        var rows = dataLines.Length;

        // Detect which columns are numeric by checking the first data row.
        // Non-numeric columns (class labels, IDs) are silently skipped.
        var firstParts = dataLines[0].Split(separator);
        var numericCols = Enumerable.Range(0, firstParts.Length)
            .Where(j => double.TryParse(firstParts[j].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            .ToArray();

        var matrix = new RawDataMatrix(rows, numericCols.Length);
        for (int i = 0; i < rows; i++)
        {
            var parts = dataLines[i].Split(separator);
            for (int ci = 0; ci < numericCols.Length; ci++)
                matrix[i, ci] = double.Parse(parts[numericCols[ci]].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);
        }
        return matrix;
    }

    public double[] ToFlatArray() => (double[])_data.Clone();
}
