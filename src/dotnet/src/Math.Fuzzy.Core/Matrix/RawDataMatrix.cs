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

    /// <summary>
    /// Attribute values of a delimited text file (see <see cref="MathFuzzy.Core.IO.DataFile"/>):
    /// a header row, a label column and a column of object names are recognised and left out.
    /// </summary>
    public static RawDataMatrix FromCsv(string path) => MathFuzzy.Core.IO.DataFile.Load(path).Data;

    public double[] ToFlatArray() => (double[])_data.Clone();
}
