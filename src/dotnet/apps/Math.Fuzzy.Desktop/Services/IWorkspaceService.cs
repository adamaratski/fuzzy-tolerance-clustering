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

    /// <summary>
    /// Loads interval-valued attribute data (columns name_lo, name_hi) and builds the tolerance
    /// with the squared Euclidean measure that reproduces the published examples.
    /// </summary>
    Workset LoadIntervalData(string path);

    /// <summary>Loads fuzzy-number data (suffix columns per attribute) and builds the tolerance.</summary>
    Workset LoadFuzzyNumberData(string path);

    /// <summary>
    /// Loads three-way data (one row per object and situation) and builds the tolerance with
    /// unitarisation and the squared generalised Euclidean distance.
    /// </summary>
    Workset LoadThreeWayData(string path);

    /// <summary>Loads an asymmetric proximity matrix and builds the tolerance through the quasi-equivalence.</summary>
    Workset LoadAsymmetricMatrix(string path);

    /// <summary>Loads an intuitionistic fuzzy tolerance from a membership and a non-membership matrix file.</summary>
    Workset LoadIntuitionisticRelation(string membershipPath, string nonMembershipPath);

    /// <summary>Loads intuitionistic attribute data (columns in pairs: membership, non-membership).</summary>
    Workset LoadIntuitionisticData(string path);

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
