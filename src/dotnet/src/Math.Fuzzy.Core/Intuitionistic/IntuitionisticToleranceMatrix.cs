using MathFuzzy.Core.IO;

namespace MathFuzzy.Core.Intuitionistic;

/// <summary>
/// An intuitionistic fuzzy tolerance on n objects: a membership matrix μ and a non-membership
/// matrix ν, both symmetric, with μ(x, x) = 1, ν(x, x) = 0 and μ + ν ≤ 1 everywhere
/// (Viattchenin 2013, sec. 3.1). The hesitancy of a pair is 1 − μ − ν.
/// </summary>
public sealed class IntuitionisticToleranceMatrix
{
    /// <summary>Values that differ by no more than this are one value (rounding noise of computed relations).</summary>
    public const double Tolerance = 1e-12;

    private readonly double[,] _membership;
    private readonly double[,] _nonMembership;

    public int Size { get; }

    /// <summary>
    /// Creates the relation from the two matrices. They are checked (square, equal sizes,
    /// symmetric, values in [0, 1], μ + ν ≤ 1), the diagonal is set to (1, 0), and values of
    /// each matrix that differ by rounding noise only are merged into one value.
    /// </summary>
    public IntuitionisticToleranceMatrix(double[,] membership, double[,] nonMembership)
    {
        int size = membership.GetLength(0);
        if (membership.GetLength(1) != size || nonMembership.GetLength(0) != size || nonMembership.GetLength(1) != size)
            throw new ArgumentException("The membership and non-membership matrices must be square and of the same size.");

        _membership = (double[,])membership.Clone();
        _nonMembership = (double[,])nonMembership.Clone();
        Size = size;

        for (int row = 0; row < size; row++)
        {
            _membership[row, row] = 1.0;
            _nonMembership[row, row] = 0.0;
            for (int column = row + 1; column < size; column++)
            {
                double degree = _membership[row, column], opposite = _nonMembership[row, column];
                if (Math.Abs(degree - _membership[column, row]) > 1e-9 || Math.Abs(opposite - _nonMembership[column, row]) > 1e-9)
                    throw new ArgumentException($"The relation is not symmetric at ({row + 1}, {column + 1}).");
                if (degree < 0.0 || degree > 1.0 || opposite < 0.0 || opposite > 1.0)
                    throw new ArgumentException($"A degree at ({row + 1}, {column + 1}) lies outside [0, 1].");
                if (degree + opposite > 1.0 + 1e-9)
                    throw new ArgumentException(
                        $"Membership and non-membership at ({row + 1}, {column + 1}) sum to more than 1 " +
                        $"({degree.ToString(System.Globalization.CultureInfo.InvariantCulture)} + " +
                        $"{opposite.ToString(System.Globalization.CultureInfo.InvariantCulture)}).");
                _membership[column, row] = degree;
                _nonMembership[column, row] = opposite;
            }
        }

        SnapValues(_membership);
        SnapValues(_nonMembership);
    }

    public double Membership(int row, int column) => _membership[row, column];

    public double NonMembership(int row, int column) => _nonMembership[row, column];

    /// <summary>Copy of the membership matrix μ.</summary>
    public double[,] MembershipMatrix() => (double[,])_membership.Clone();

    /// <summary>Copy of the non-membership matrix ν.</summary>
    public double[,] NonMembershipMatrix() => (double[,])_nonMembership.Clone();

    /// <summary>
    /// The thresholds α in ascending order: the distinct positive membership degrees, the
    /// diagonal value 1 included. Zero is not a threshold.
    /// </summary>
    public IReadOnlyList<double> AlphaSequence()
    {
        var values = new SortedSet<double>();
        foreach (double value in _membership)
            if (value > 0.0) values.Add(value);
        return [.. values];
    }

    /// <summary>
    /// The thresholds β in descending order: the distinct non-membership degrees below 1, the
    /// diagonal value 0 included.
    /// </summary>
    public IReadOnlyList<double> BetaSequence()
    {
        var values = new SortedSet<double>();
        foreach (double value in _nonMembership)
            if (value < 1.0) values.Add(value);
        return [.. values.Reverse()];
    }

    /// <summary>
    /// Reads the relation from two delimited text files, one with the membership matrix and one
    /// with the non-membership matrix (a header row and a first column of object names are allowed).
    /// </summary>
    public static IntuitionisticToleranceMatrix FromCsv(string membershipPath, string nonMembershipPath) =>
        new(ReadSquareMatrix(membershipPath), ReadSquareMatrix(nonMembershipPath));

    private static double[,] ReadSquareMatrix(string path)
    {
        var table = CsvTable.Load(path);
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");
        int firstColumn = table.IsNumericColumn(0) ? 0 : 1;
        int size = table.Rows.Length;
        if (table.ColumnCount - firstColumn != size)
            throw new FormatException(
                $"{table.SourceName}: a square matrix is expected, found {size} rows and {table.ColumnCount - firstColumn} columns.");

        var matrix = new double[size, size];
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
                matrix[row, column] = table.Number(row, column + firstColumn);
        return matrix;
    }

    /// <summary>Replaces values that differ by rounding noise by the smallest value of their group.</summary>
    private static void SnapValues(double[,] matrix)
    {
        var sorted = matrix.Cast<double>().Distinct().Order().ToArray();
        Dictionary<double, double>? representative = null;
        double groupStart = sorted[0];
        for (int index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] - sorted[index - 1] > Tolerance) { groupStart = sorted[index]; continue; }
            (representative ??= [])[sorted[index]] = groupStart;
        }
        if (representative is null) return;

        int size = matrix.GetLength(0);
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
                if (representative.TryGetValue(matrix[row, column], out double snapped))
                    matrix[row, column] = snapped;
    }
}
