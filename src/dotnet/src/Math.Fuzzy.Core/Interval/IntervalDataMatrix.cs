using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Interval;

/// <summary>How the bounds of interval-valued data are scaled to [0, 1].</summary>
public enum IntervalScaling
{
    /// <summary>(v − min) / (max − min) per attribute.</summary>
    MinMax,

    /// <summary>v / max|v| per attribute (normalisation by the maximum, Viattchenin 2013, eq. (2.11)).</summary>
    Maximum,

    /// <summary>The bounds are used as given.</summary>
    None
}

/// <summary>
/// Interval-valued attribute data: every object is described on every attribute by a closed
/// interval [lower, upper] (Viattchenin 2011, sec. 1.2; 2013, sec. 3.4). The data are three-way
/// data with two "situations": they can be read as two ordinary attribute matrices, the matrix
/// of lower bounds and the matrix of upper bounds. If all intervals are degenerate the two
/// matrices coincide.
/// </summary>
public sealed class IntervalDataMatrix
{
    private readonly double[,] _lower;
    private readonly double[,] _upper;

    public int Objects { get; }
    public int Attributes { get; }

    /// <summary>Names of the attributes (from the file header, or a1…am).</summary>
    public IReadOnlyList<string> AttributeNames { get; }

    /// <summary>Names of the objects (the first text column of the file), or null.</summary>
    public IReadOnlyList<string>? ObjectNames { get; }

    public IntervalDataMatrix(double[,] lower, double[,] upper,
        IReadOnlyList<string>? attributeNames = null, IReadOnlyList<string>? objectNames = null)
    {
        Objects = lower.GetLength(0);
        Attributes = lower.GetLength(1);
        if (upper.GetLength(0) != Objects || upper.GetLength(1) != Attributes)
            throw new ArgumentException("The tables of lower and upper bounds must have the same shape.");
        if (attributeNames is not null && attributeNames.Count != Attributes)
            throw new ArgumentException("One name per attribute is expected.", nameof(attributeNames));
        if (objectNames is not null && objectNames.Count != Objects)
            throw new ArgumentException("One name per object is expected.", nameof(objectNames));

        for (int row = 0; row < Objects; row++)
            for (int attribute = 0; attribute < Attributes; attribute++)
            {
                double low = lower[row, attribute], high = upper[row, attribute];
                if (!double.IsFinite(low) || !double.IsFinite(high))
                    throw new ArgumentException($"Object {row + 1}, attribute {attribute + 1}: the bounds must be finite numbers.");
                if (low > high)
                    throw new ArgumentException(
                        $"Object {row + 1}, attribute {attribute + 1}: the lower bound exceeds the upper bound.");
            }

        _lower = (double[,])lower.Clone();
        _upper = (double[,])upper.Clone();
        AttributeNames = attributeNames ?? [.. Enumerable.Range(1, Attributes).Select(index => $"a{index}")];
        ObjectNames = objectNames;
    }

    public double Lower(int row, int attribute) => _lower[row, attribute];

    public double Upper(int row, int attribute) => _upper[row, attribute];

    /// <summary>Midpoint (lower + upper) / 2 of an interval.</summary>
    public double Midpoint(int row, int attribute) => 0.5 * (_lower[row, attribute] + _upper[row, attribute]);

    /// <summary>True when every interval is a single point.</summary>
    public bool IsDegenerate
    {
        get
        {
            for (int row = 0; row < Objects; row++)
                for (int attribute = 0; attribute < Attributes; attribute++)
                    if (_lower[row, attribute] != _upper[row, attribute]) return false;
            return true;
        }
    }

    /// <summary>The matrix of lower bounds X^min as ordinary attribute data.</summary>
    public RawDataMatrix LowerBounds() => RawDataMatrix.FromArray(_lower);

    /// <summary>The matrix of upper bounds X^max as ordinary attribute data.</summary>
    public RawDataMatrix UpperBounds() => RawDataMatrix.FromArray(_upper);

    /// <summary>The matrix of midpoints as ordinary attribute data.</summary>
    public RawDataMatrix Midpoints()
    {
        var result = new RawDataMatrix(Objects, Attributes);
        for (int row = 0; row < Objects; row++)
            for (int attribute = 0; attribute < Attributes; attribute++)
                result[row, attribute] = Midpoint(row, attribute);
        return result;
    }

    /// <summary>
    /// Normalisation of the bounds attribute by attribute, jointly over the objects and the two
    /// bounds (Viattchenin 2011, eq. (23); 2012, eq. (9); 2015, eq. (17)): for
    /// <see cref="IntervalScaling.MinMax"/>
    ///   x̂ = (x − min_{i,s} x_i^{t(s)}) / (max_{i,s} x_i^{t(s)} − min_{i,s} x_i^{t(s)}),
    /// so that every object becomes an interval-valued fuzzy set with bounds in [0, 1] and the
    /// order of the bounds is kept. An attribute with a single value is mapped to 0.
    /// </summary>
    public IntervalDataMatrix NormalizeJointly(IntervalScaling scaling = IntervalScaling.MinMax)
    {
        if (scaling == IntervalScaling.None) return this;
        var lower = new double[Objects, Attributes];
        var upper = new double[Objects, Attributes];
        for (int attribute = 0; attribute < Attributes; attribute++)
        {
            double minimum = double.MaxValue, maximum = double.MinValue, largestMagnitude = 0.0;
            for (int row = 0; row < Objects; row++)
            {
                minimum = Math.Min(minimum, _lower[row, attribute]);
                maximum = Math.Max(maximum, _upper[row, attribute]);
                largestMagnitude = Math.Max(largestMagnitude,
                    Math.Max(Math.Abs(_lower[row, attribute]), Math.Abs(_upper[row, attribute])));
            }
            for (int row = 0; row < Objects; row++)
            {
                lower[row, attribute] = Scale(_lower[row, attribute], scaling, minimum, maximum, largestMagnitude);
                upper[row, attribute] = Scale(_upper[row, attribute], scaling, minimum, maximum, largestMagnitude);
            }
        }
        return new IntervalDataMatrix(lower, upper, AttributeNames, ObjectNames);
    }

    /// <summary>
    /// Normalisation of each matrix of bounds on its own: the lower bounds are scaled with the
    /// extremes of the lower bounds of the attribute, the upper bounds with those of the upper
    /// bounds. This is the reading under which the published numbers of clusters of the
    /// matrices X^min and X^max are reproduced (Viattchenin 2011, sec. 4.2; 2015, sec. 3).
    /// The result need not be an interval matrix (a lower bound may exceed the upper bound
    /// after scaling), therefore the two matrices are returned separately.
    /// </summary>
    public (RawDataMatrix Lower, RawDataMatrix Upper) NormalizeSeparately(IntervalScaling scaling = IntervalScaling.MinMax)
        => (ScaleColumns(_lower, scaling), ScaleColumns(_upper, scaling));

    private RawDataMatrix ScaleColumns(double[,] values, IntervalScaling scaling)
    {
        var result = new RawDataMatrix(Objects, Attributes);
        for (int attribute = 0; attribute < Attributes; attribute++)
        {
            double minimum = double.MaxValue, maximum = double.MinValue, largestMagnitude = 0.0;
            for (int row = 0; row < Objects; row++)
            {
                minimum = Math.Min(minimum, values[row, attribute]);
                maximum = Math.Max(maximum, values[row, attribute]);
                largestMagnitude = Math.Max(largestMagnitude, Math.Abs(values[row, attribute]));
            }
            for (int row = 0; row < Objects; row++)
                result[row, attribute] = Scale(values[row, attribute], scaling, minimum, maximum, largestMagnitude);
        }
        return result;
    }

    private static double Scale(double value, IntervalScaling scaling, double minimum, double maximum, double largestMagnitude)
        => scaling switch
        {
            IntervalScaling.MinMax => maximum - minimum < 1e-12 ? 0.0 : (value - minimum) / (maximum - minimum),
            IntervalScaling.Maximum => largestMagnitude < 1e-12 ? 0.0 : value / largestMagnitude,
            _ => value
        };

    /// <summary>
    /// Reads a delimited text file whose numeric columns come in pairs: the lower and the upper
    /// bound of the first attribute, then of the second, and so on — for example
    /// <c>name,gravity_lo,gravity_hi,iodine_lo,iodine_hi</c>. The first text column, if any, holds
    /// the object names; other text columns are ignored. With a header, the attribute name is
    /// the header of its lower-bound column without a trailing "_lo" (or "_min").
    /// </summary>
    public static IntervalDataMatrix FromCsv(string path) => FromTable(CsvTable.Load(path));

    /// <summary>Reads interval data from delimited text (see <see cref="FromCsv"/>).</summary>
    public static IntervalDataMatrix Parse(string text, string sourceName = "input") => FromTable(CsvTable.Parse(text, sourceName));

    private static IntervalDataMatrix FromTable(CsvTable table)
    {
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        var numericColumns = Enumerable.Range(0, table.ColumnCount).Where(table.IsNumericColumn).ToArray();
        if (numericColumns.Length == 0 || numericColumns.Length % 2 != 0)
            throw new FormatException(
                $"{table.SourceName}: interval data need an even number of numeric columns " +
                $"(lower and upper bound of every attribute); found {numericColumns.Length}.");

        int objects = table.Rows.Length, attributes = numericColumns.Length / 2;
        var lower = new double[objects, attributes];
        var upper = new double[objects, attributes];
        var names = new string[attributes];
        for (int attribute = 0; attribute < attributes; attribute++)
        {
            int lowerColumn = numericColumns[2 * attribute], upperColumn = numericColumns[2 * attribute + 1];
            string header = table.Header is null ? "" : table.Header[lowerColumn];
            names[attribute] = header.Length == 0 ? $"a{attribute + 1}" : StripSuffix(header);
            for (int row = 0; row < objects; row++)
            {
                lower[row, attribute] = table.Number(row, lowerColumn);
                upper[row, attribute] = table.Number(row, upperColumn);
                if (lower[row, attribute] > upper[row, attribute])
                    throw new FormatException(
                        $"{table.SourceName}: line {row + 1 + (table.Header is null ? 0 : 1)}, attribute '{names[attribute]}': " +
                        "the lower bound exceeds the upper bound.");
            }
        }

        string[]? objectNames = null;
        int nameColumn = Enumerable.Range(0, table.ColumnCount).FirstOrDefault(column => !numericColumns.Contains(column), -1);
        if (nameColumn >= 0)
            objectNames = [.. table.Rows.Select(row => row[nameColumn])];

        return new IntervalDataMatrix(lower, upper, names, objectNames);
    }

    private static string StripSuffix(string header)
    {
        foreach (var suffix in (string[])["_lo", "_min", "_lower"])
            if (header.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return header[..^suffix.Length];
        return header;
    }
}
