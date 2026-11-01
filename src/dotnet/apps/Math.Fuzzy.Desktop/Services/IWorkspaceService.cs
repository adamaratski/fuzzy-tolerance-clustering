using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Desktop.Services;

public interface IWorkspaceService
{
    /// <summary>All loaded worksets, in load order.</summary>
    IReadOnlyList<Workset> Worksets { get; }

    /// <summary>The workset all operations apply to. Null until something is loaded.</summary>
    Workset? ActiveWorkset { get; }

    // Convenience accessors for the active workset (kept for existing call sites).
    RawDataMatrix?        RawData         { get; }
    LabeledDataset?       Labels          { get; }
    FuzzyToleranceMatrix? ToleranceMatrix { get; }
    IReadOnlyList<ClusterResult> Results  { get; }

    /// <summary>Loads a CSV as a new workset and activates it.</summary>
    Workset LoadCsv(string path);

    /// <summary>Loads a pre-built n×n tolerance matrix as a new workset and activates it.</summary>
    Workset LoadMatrix(string path);

    /// <summary>Switches the active workset. Fires <see cref="DataChanged"/>.</summary>
    void Activate(Workset workset);

    /// <summary>Builds T for the active workset with the given distance, normalisation and accuracy ε.</summary>
    void BuildToleranceMatrix(MathFuzzy.Core.Distance.IDistance distance, string normalization, double? accuracy);
    void ComputeTransitiveClosure();

    /// <summary>Stores clustering results on the active workset. Fires <see cref="ResultsChanged"/>.</summary>
    void SetResults(IReadOnlyList<ClusterResult> results);

    event EventHandler DataChanged;
    event EventHandler ResultsChanged;
}
