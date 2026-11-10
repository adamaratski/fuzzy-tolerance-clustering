using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.IO;

/// <summary>
/// An object–attribute data file: numeric attribute columns, an optional column of class
/// labels and an optional column of object names.
/// </summary>
/// <param name="Data">Attribute values, one row per object.</param>
/// <param name="FeatureNames">Names of the attribute columns (header names, or x1…xm).</param>
/// <param name="TrueLabels">0-based class index of every object, or null when the file has no label column.</param>
/// <param name="ClassNames">Class names indexed by <paramref name="TrueLabels"/>, or null.</param>
public sealed record DataFile(
    RawDataMatrix Data,
    string[] FeatureNames,
    int[]? TrueLabels,
    string[]? ClassNames)
{
    private static readonly HashSet<string> LabelColumnNames =
        new(StringComparer.OrdinalIgnoreCase)
        { "class", "label", "labels", "type", "species", "variety", "target", "category", "group" };

    public bool HasLabels => TrueLabels is not null;

    public static DataFile Load(string path) => FromTable(CsvTable.Load(path));

    /// <summary>
    /// Interprets a table. The label column is the column whose header is a known label name
    /// (class, label, species, …; it may be numeric), otherwise the first text column whose
    /// values repeat; a text column of all-distinct values holds object names and is ignored.
    /// Every remaining numeric column is an attribute; a missing value in one is an error.
    /// </summary>
    public static DataFile FromTable(CsvTable table)
    {
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        int objectCount = table.Rows.Length;
        var numeric = Enumerable.Range(0, table.ColumnCount).Select(table.IsNumericColumn).ToArray();

        // A column that is numeric except for a few cells is a damaged attribute, not a label.
        for (int column = 0; column < table.ColumnCount; column++)
        {
            if (numeric[column]) continue;
            int numbers = table.Rows.Count(row => table.TryNumber(row[column], out _));
            if (2 * numbers <= objectCount) continue;
            int firstBad = Array.FindIndex(table.Rows, row => !table.TryNumber(row[column], out _) && !CsvTable.IsMissing(row[column]));
            if (firstBad >= 0) table.Number(firstBad, column);   // throws with the position
        }

        int labelColumn = -1;
        if (table.Header is not null)
            labelColumn = Array.FindIndex(table.Header, LabelColumnNames.Contains);
        if (labelColumn < 0)
        {
            for (int column = 0; column < table.ColumnCount && labelColumn < 0; column++)
            {
                if (numeric[column]) continue;
                int distinct = table.Rows.Select(row => row[column]).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (distinct < objectCount || objectCount == 1) labelColumn = column;
            }
        }

        var featureColumns = Enumerable.Range(0, table.ColumnCount)
            .Where(column => column != labelColumn && numeric[column])
            .ToArray();
        if (featureColumns.Length == 0)
            throw new FormatException($"{table.SourceName}: no numeric attribute column found.");

        var data = new RawDataMatrix(objectCount, featureColumns.Length);
        for (int row = 0; row < objectCount; row++)
            for (int position = 0; position < featureColumns.Length; position++)
                data[row, position] = table.Number(row, featureColumns[position]);

        var featureNames = featureColumns
            .Select((column, position) => table.Header is not null && table.Header[column].Length > 0
                ? table.Header[column]
                : $"x{position + 1}")
            .ToArray();

        if (labelColumn < 0)
            return new DataFile(data, featureNames, null, null);

        var classIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var trueLabels = new int[objectCount];
        for (int row = 0; row < objectCount; row++)
        {
            string name = table.Rows[row][labelColumn];
            if (!classIndex.TryGetValue(name, out int index))
                classIndex[name] = index = classIndex.Count;
            trueLabels[row] = index;
        }
        var classNames = classIndex.OrderBy(entry => entry.Value).Select(entry => entry.Key).ToArray();
        return new DataFile(data, featureNames, trueLabels, classNames);
    }
}
