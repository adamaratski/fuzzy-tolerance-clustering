using System.Globalization;
using System.Text;

namespace MathFuzzy.Core.IO;

/// <summary>
/// A delimited text file read into cells. The separator (comma, semicolon or tab) is detected
/// from the first line; quoted fields follow RFC 4180; a first row without a single number is
/// the header. With a semicolon or tab separator a decimal comma is accepted.
/// </summary>
public sealed class CsvTable
{
    private static readonly HashSet<string> MissingMarkers =
        new(StringComparer.OrdinalIgnoreCase) { "", "?", "NA", "N/A", "NaN", "null" };

    /// <summary>Column names of the header row, or null when the file has no header.</summary>
    public string[]? Header { get; }

    /// <summary>Data rows; every row has <see cref="ColumnCount"/> cells, trimmed.</summary>
    public string[][] Rows { get; }

    public char Separator { get; }
    public int ColumnCount { get; }

    /// <summary>Name of the file or stream the table came from (used in error messages).</summary>
    public string SourceName { get; }

    private CsvTable(string[]? header, string[][] rows, char separator, int columnCount, string sourceName)
    {
        Header = header;
        Rows = rows;
        Separator = separator;
        ColumnCount = columnCount;
        SourceName = sourceName;
    }

    public static CsvTable Load(string path) => Parse(File.ReadAllText(path), Path.GetFileName(path));

    public static CsvTable Parse(string text, string sourceName = "input")
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        char separator = DetectSeparator(text);
        var records = SplitRecords(text, separator);
        if (records.Count == 0)
            throw new FormatException($"{sourceName}: the file is empty.");

        int columnCount = records.Max(record => record.Length);

        // A separator at the end of every line yields an empty last column: drop such columns.
        while (columnCount > 1 && records.All(record => record.Length < columnCount || record[columnCount - 1].Length == 0))
            columnCount--;

        var rows = new List<string[]>(records.Count);
        for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            var record = records[recordIndex];
            if (record.Length < columnCount)
                throw new FormatException(
                    $"{sourceName}: line {recordIndex + 1} has {record.Length} cells, expected {columnCount}.");
            rows.Add(record.Length == columnCount ? record : record[..columnCount]);
        }

        var table = new CsvTable(null, [.. rows], separator, columnCount, sourceName);
        bool firstRowIsHeader = !rows[0].Any(cell => table.TryNumber(cell, out _));
        return firstRowIsHeader
            ? new CsvTable(rows[0], [.. rows.Skip(1)], separator, columnCount, sourceName)
            : table;
    }

    /// <summary>Name of a column: its header cell, or the 1-based position when there is no header.</summary>
    public string ColumnName(int column) =>
        Header is not null && Header[column].Length > 0 ? Header[column] : $"column {column + 1}";

    /// <summary>True for an empty cell and for the usual missing-value markers (?, NA, NaN, null).</summary>
    public static bool IsMissing(string cell) => MissingMarkers.Contains(cell);

    /// <summary>
    /// Parses a number written with a decimal point; when the separator is not a comma,
    /// a decimal comma is accepted as well. Missing-value markers are not numbers.
    /// </summary>
    public bool TryNumber(string cell, out double value)
    {
        value = 0.0;
        if (IsMissing(cell)) return false;
        if (double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return double.IsFinite(value);
        if (Separator != ',' && cell.Contains(',') && !cell.Contains('.')
            && double.TryParse(cell.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return double.IsFinite(value);
        return false;
    }

    /// <summary>True when every cell of the column that is not missing is a number, and at least one is.</summary>
    public bool IsNumericColumn(int column)
    {
        bool anyNumber = false;
        foreach (var row in Rows)
        {
            if (IsMissing(row[column])) continue;
            if (!TryNumber(row[column], out _)) return false;
            anyNumber = true;
        }
        return anyNumber;
    }

    /// <summary>The number in a cell; a missing or malformed value is reported with its position.</summary>
    public double Number(int row, int column)
    {
        string cell = Rows[row][column];
        if (TryNumber(cell, out double value)) return value;

        int line = row + 1 + (Header is null ? 0 : 1);
        throw new FormatException(IsMissing(cell)
            ? $"{SourceName}: missing value at line {line}, {ColumnName(column)}. " +
              "The algorithms need complete data: remove the object or impute the value."
            : $"{SourceName}: '{cell}' at line {line}, {ColumnName(column)} is not a number.");
    }

    private static char DetectSeparator(string text)
    {
        int commas = 0, semicolons = 0, tabs = 0;
        bool quoted = false;
        foreach (char symbol in text)
        {
            if (symbol == '"') quoted = !quoted;
            else if (!quoted)
            {
                if (symbol == '\n' && commas + semicolons + tabs > 0) break;
                if (symbol == ',') commas++;
                else if (symbol == ';') semicolons++;
                else if (symbol == '\t') tabs++;
            }
        }
        if (semicolons > 0 && semicolons >= tabs) return ';';   // "1,5;2,5" — the commas are decimal commas
        if (tabs > 0) return '\t';
        return ',';
    }

    /// <summary>Splits the text into records of trimmed cells; blank lines are skipped.</summary>
    private static List<string[]> SplitRecords(string text, char separator)
    {
        var records = new List<string[]>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        bool cellWasQuoted = false;

        void EndCell()
        {
            cells.Add(cellWasQuoted ? cell.ToString() : cell.ToString().Trim());
            cell.Clear();
            cellWasQuoted = false;
        }

        void EndRecord()
        {
            EndCell();
            if (cells.Count > 1 || cells[0].Length > 0)
                records.Add([.. cells]);
            cells.Clear();
        }

        for (int position = 0; position < text.Length; position++)
        {
            char symbol = text[position];
            if (quoted)
            {
                if (symbol != '"') cell.Append(symbol);
                else if (position + 1 < text.Length && text[position + 1] == '"') { cell.Append('"'); position++; }
                else quoted = false;
            }
            else if (symbol == '"' && cell.ToString().Trim().Length == 0)
            {
                cell.Clear();
                quoted = true;
                cellWasQuoted = true;
            }
            else if (symbol == separator) EndCell();
            else if (symbol == '\n') EndRecord();
            else if (symbol != '\r') cell.Append(symbol);
        }
        if (cell.Length > 0 || cells.Count > 0) EndRecord();

        return records;
    }
}
