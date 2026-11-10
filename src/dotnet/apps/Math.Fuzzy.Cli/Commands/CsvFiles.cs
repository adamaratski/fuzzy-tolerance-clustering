using System.Globalization;
using System.Text;
using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Cli.Commands;

/// <summary>CSV input and output shared by the commands. Numbers are always written with a decimal point.</summary>
internal static class CsvFiles
{
    /// <summary>Attribute data, attribute names and labels of a delimited text file.</summary>
    public static DataFile Read(FileInfo input) => DataFile.Load(input.FullName);

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

    /// <summary>
    /// Runs a command body; a failure is reported on standard error with exit code 2.
    /// On success the exit code is 0, or what <paramref name="exitCode"/> returns.
    /// </summary>
    public static async Task<int> Guard(Func<Task> body, Func<int>? exitCode = null)
    {
        try
        {
            await body();
            return exitCode?.Invoke() ?? 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Cancelled.");
            return 130;
        }
        catch (Exception error) when (error is IOException or FormatException or ArgumentException
                                          or InvalidOperationException or IndexOutOfRangeException
                                          or System.Text.Json.JsonException)
        {
            await Console.Error.WriteLineAsync($"Error: {error.Message}");
            return 2;
        }
    }
}
