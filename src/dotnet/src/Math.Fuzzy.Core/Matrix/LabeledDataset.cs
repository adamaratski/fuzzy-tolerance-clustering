namespace MathFuzzy.Core.Matrix;

/// <summary>
/// A dataset where each row has numeric features AND a class label.
/// Labels are stored as 0-based integers; original names preserved in ClassNames.
/// </summary>
public sealed class LabeledDataset
{
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
    /// Loads a delimited text file that contains both numeric features and a label column
    /// (see <see cref="MathFuzzy.Core.IO.DataFile"/> for how the columns are recognised).
    /// Returns <c>null</c> if no label column can be detected.
    /// </summary>
    public static LabeledDataset? TryFromCsv(string path) => From(MathFuzzy.Core.IO.DataFile.Load(path));

    /// <summary>The labelled view of an already loaded file, or null when it has no label column.</summary>
    public static LabeledDataset? From(MathFuzzy.Core.IO.DataFile file) =>
        file.HasLabels ? new LabeledDataset(file.Data, file.TrueLabels!, file.ClassNames!) : null;
}
