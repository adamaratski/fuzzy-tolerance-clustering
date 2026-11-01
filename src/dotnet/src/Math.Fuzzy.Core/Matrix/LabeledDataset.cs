using System.Globalization;

namespace MathFuzzy.Core.Matrix;

/// <summary>
/// A dataset where each row has numeric features AND a class label.
/// Labels are stored as 0-based integers; original names preserved in ClassNames.
/// </summary>
public sealed class LabeledDataset
{
    private static readonly HashSet<string> LabelColumnNames =
        new(StringComparer.OrdinalIgnoreCase)
        { "class", "label", "labels", "type", "species", "variety", "target", "category" };

    public RawDataMatrix Data       { get; }
    public int[]         TrueLabels { get; }          // 0-based class index per row
    public string[]      ClassNames { get; }           // ClassNames[i] = name of class i

    public int ClassCount => ClassNames.Length;

    private LabeledDataset(RawDataMatrix data, int[] trueLabels, string[] classNames)
    {
        Data       = data;
        TrueLabels = trueLabels;
        ClassNames = classNames;
    }

    /// <summary>
    /// Loads a CSV file that contains both numeric features and a label column.
    /// Returns <c>null</c> if no label column can be detected.
    /// </summary>
    public static LabeledDataset? TryFromCsv(string path, char separator = ',')
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        if (lines.Length == 0) return null;

        // Detect header
        bool hasHeader = false;
        string[]? headerNames = null;
        var firstCells = lines[0].Split(separator);
        if (!double.TryParse(firstCells[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            hasHeader = true;
            headerNames = firstCells.Select(c => c.Trim()).ToArray();
        }

        var dataLines = hasHeader ? lines[1..] : lines;
        if (dataLines.Length == 0) return null;

        var sampleParts = dataLines[0].Split(separator);
        int colCount = sampleParts.Length;

        // Determine which column is the label:
        // Priority 1 — first non-numeric column in the data
        int labelCol = -1;
        for (int j = 0; j < colCount; j++)
        {
            if (!double.TryParse(sampleParts[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                labelCol = j;
                break;
            }
        }

        // Priority 2 — column whose header name is a known label keyword (handles integer labels like wine)
        if (labelCol < 0 && headerNames is not null)
        {
            for (int j = 0; j < headerNames.Length; j++)
            {
                if (LabelColumnNames.Contains(headerNames[j]))
                {
                    labelCol = j;
                    break;
                }
            }
        }

        if (labelCol < 0) return null; // no label column found

        // Feature columns = all except labelCol
        var featureCols = Enumerable.Range(0, colCount)
            .Where(j => j != labelCol)
            .Where(j => double.TryParse(sampleParts[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .ToArray();

        // Build string labels and map to integers
        var rawLabels = dataLines
            .Select(l => l.Split(separator)[labelCol].Trim())
            .ToArray();

        var nameToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in rawLabels)
            if (!nameToIdx.ContainsKey(name))
                nameToIdx[name] = nameToIdx.Count;

        var classNames = nameToIdx.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        var trueLabels = rawLabels.Select(l => nameToIdx[l]).ToArray();

        // Build feature matrix
        int n = dataLines.Length;
        var matrix = new RawDataMatrix(n, featureCols.Length);
        for (int i = 0; i < n; i++)
        {
            var parts = dataLines[i].Split(separator);
            for (int ci = 0; ci < featureCols.Length; ci++)
                matrix[i, ci] = double.Parse(parts[featureCols[ci]].Trim(), CultureInfo.InvariantCulture);
        }

        return new LabeledDataset(matrix, trueLabels, classNames);
    }
}
