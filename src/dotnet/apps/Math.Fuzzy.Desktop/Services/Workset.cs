using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Desktop.Services;

/// <summary>
/// One loaded dataset with everything computed for it: raw data (or a directly
/// loaded tolerance matrix), labels, T/T̃ and clustering results.
/// </summary>
public sealed class Workset
{
    public required string Name { get; init; }
    public string? SourcePath   { get; init; }
    public bool IsMatrixInput   { get; init; }

    public RawDataMatrix?        RawData         { get; set; }
    public LabeledDataset?       Labels          { get; set; }
    /// <summary>Names of the feature columns (CSV header), x1…xm when the file has none.</summary>
    public IReadOnlyList<string> FeatureNames    { get; init; } = [];
    public FuzzyToleranceMatrix? ToleranceMatrix { get; set; }
    /// <summary>True when <see cref="ToleranceMatrix"/> is the max-min transitive closure T̃.</summary>
    public bool ClosureApplied  { get; set; }

    public IReadOnlyList<ClusterResult> Results { get; set; } = [];

    public string Meta => IsMatrixInput
        ? $"{ToleranceMatrix?.Size ?? 0} × {ToleranceMatrix?.Size ?? 0} · matrix input"
        : $"{RawData?.Rows ?? 0} × {RawData?.Cols ?? 0}{(Labels is not null ? $" · {Labels.ClassCount} classes" : "")}";
}
