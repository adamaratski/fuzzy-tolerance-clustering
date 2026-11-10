using System.Globalization;
using System.Text;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Cli.Commands;

/// <summary>CSV input and output shared by the commands. Numbers are always written with a decimal point.</summary>
internal static class CsvFiles
{
    /// <summary>Attribute data of a CSV file; a label column, when present, is left out.</summary>
    public static RawDataMatrix ReadData(FileInfo input) =>
        LabeledDataset.TryFromCsv(input.FullName)?.Data ?? RawDataMatrix.FromCsv(input.FullName);

    /// <summary>
    /// Header names of the numeric columns, in file order; x1…xm when the file has no header
    /// or the names cannot be matched to the columns of <paramref name="data"/>.
    /// </summary>
    public static string[] FeatureNames(FileInfo input, RawDataMatrix data)
    {
        var fallback = Enumerable.Range(1, data.Cols).Select(column => $"x{column}").ToArray();
        var lines = File.ReadLines(input.FullName).Where(line => !string.IsNullOrWhiteSpace(line)).Take(2).ToArray();
        if (lines.Length < 2) return fallback;

        var header = lines[0].Split(',').Select(cell => cell.Trim().TrimStart('﻿')).ToArray();
        if (IsNumber(header[0])) return fallback;

        var sample = lines[1].Split(',');
        var names = Enumerable.Range(0, Math.Min(header.Length, sample.Length))
            .Where(column => IsNumber(sample[column]))
            .Select(column => header[column])
            .ToArray();
        return names.Length == data.Cols ? names : fallback;
    }

    /// <summary>Writes a square matrix, one row per line, with full double precision.</summary>
    public static string Format(FuzzyToleranceMatrix matrix)
    {
        var text = new StringBuilder();
        for (int row = 0; row < matrix.Size; row++)
        {
            var values = matrix.GetRow(row);
            for (int column = 0; column < matrix.Size; column++)
            {
                if (column > 0) text.Append(',');
                text.Append(values[column].ToString("R", CultureInfo.InvariantCulture));
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    /// <summary>Writes attribute data with a header row.</summary>
    public static string Format(RawDataMatrix data, IReadOnlyList<string> columnNames)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", columnNames));
        for (int row = 0; row < data.Rows; row++)
        {
            for (int column = 0; column < data.Cols; column++)
            {
                if (column > 0) text.Append(',');
                text.Append(data[row, column].ToString("R", CultureInfo.InvariantCulture));
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    /// <summary>Writes the text to the file, or to standard output when no file is given.</summary>
    public static async Task WriteAsync(string text, FileInfo? output, string what)
    {
        if (output is null)
        {
            await Console.Out.WriteAsync(text);
            return;
        }
        await File.WriteAllTextAsync(output.FullName, text);
        await Console.Error.WriteLineAsync($"Wrote {what} to {output.FullName}");
    }

    /// <summary>Runs a command body; a failure is reported on standard error with exit code 2.</summary>
    public static async Task<int> Guard(Func<Task> body)
    {
        try
        {
            await body();
            return 0;
        }
        catch (Exception error) when (error is IOException or FormatException or ArgumentException
                                          or InvalidOperationException or IndexOutOfRangeException
                                          or System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync($"Error: {error.Message}");
            return 2;
        }
    }

    private static bool IsNumber(string cell) =>
        double.TryParse(cell.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
