using MathFuzzy.Core.IO;

namespace MathFuzzy.Core.Intuitionistic;

/// <summary>
/// Intuitionistic fuzzy attribute data: every object is described on every attribute by a
/// pair (μ, ν) with μ + ν ≤ 1 — the degree to which it has the attribute and the degree to
/// which it does not.
/// </summary>
public sealed class IntuitionisticDataMatrix
{
    private readonly double[,] _membership;
    private readonly double[,] _nonMembership;

    public int Objects { get; }
    public int Attributes { get; }

    /// <summary>Names of the attributes (from the file header, or a1…am).</summary>
    public IReadOnlyList<string> AttributeNames { get; }

    public IntuitionisticDataMatrix(double[,] membership, double[,] nonMembership, IReadOnlyList<string>? attributeNames = null)
    {
        Objects = membership.GetLength(0);
        Attributes = membership.GetLength(1);
        if (nonMembership.GetLength(0) != Objects || nonMembership.GetLength(1) != Attributes)
            throw new ArgumentException("The membership and non-membership tables must have the same shape.");

        for (int row = 0; row < Objects; row++)
            for (int attribute = 0; attribute < Attributes; attribute++)
            {
                double degree = membership[row, attribute], opposite = nonMembership[row, attribute];
                if (degree < 0.0 || opposite < 0.0 || degree + opposite > 1.0 + 1e-9)
                    throw new ArgumentException(
                        $"Object {row + 1}, attribute {attribute + 1}: the pair must satisfy μ ≥ 0, ν ≥ 0, μ + ν ≤ 1.");
            }

        _membership = (double[,])membership.Clone();
        _nonMembership = (double[,])nonMembership.Clone();
        AttributeNames = attributeNames ?? [.. Enumerable.Range(1, Attributes).Select(index => $"a{index}")];
    }

    public double Membership(int row, int attribute) => _membership[row, attribute];

    public double NonMembership(int row, int attribute) => _nonMembership[row, attribute];

    public double Hesitancy(int row, int attribute) => 1.0 - _membership[row, attribute] - _nonMembership[row, attribute];

    /// <summary>
    /// Reads a delimited text file whose numeric columns come in pairs: the membership and the
    /// non-membership degree of the first attribute, then of the second, and so on. Text columns
    /// (object names) are ignored. With a header, the attribute name is the header of its
    /// membership column without a trailing "_mu".
    /// </summary>
    public static IntuitionisticDataMatrix FromCsv(string path)
    {
        var table = CsvTable.Load(path);
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        var numericColumns = Enumerable.Range(0, table.ColumnCount).Where(table.IsNumericColumn).ToArray();
        if (numericColumns.Length == 0 || numericColumns.Length % 2 != 0)
            throw new FormatException(
                $"{table.SourceName}: intuitionistic data need an even number of numeric columns " +
                $"(membership and non-membership of every attribute); found {numericColumns.Length}.");

        int objects = table.Rows.Length, attributes = numericColumns.Length / 2;
        var membership = new double[objects, attributes];
        var nonMembership = new double[objects, attributes];
        var names = new string[attributes];
        for (int attribute = 0; attribute < attributes; attribute++)
        {
            int membershipColumn = numericColumns[2 * attribute], nonMembershipColumn = numericColumns[2 * attribute + 1];
            string header = table.Header is null ? "" : table.Header[membershipColumn];
            names[attribute] = header.Length == 0
                ? $"a{attribute + 1}"
                : header.EndsWith("_mu", StringComparison.OrdinalIgnoreCase) ? header[..^3] : header;
            for (int row = 0; row < objects; row++)
            {
                membership[row, attribute] = table.Number(row, membershipColumn);
                nonMembership[row, attribute] = table.Number(row, nonMembershipColumn);
            }
        }
        return new IntuitionisticDataMatrix(membership, nonMembership, names);
    }
}
