using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Distance;
using System.IO;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Desktop.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly List<Workset> _worksets = [];

    public IReadOnlyList<Workset> Worksets => _worksets;
    public Workset? ActiveWorkset { get; private set; }

    public RawDataMatrix?        RawData         => ActiveWorkset?.RawData;
    public LabeledDataset?       Labels          => ActiveWorkset?.Labels;
    public FuzzyToleranceMatrix? ToleranceMatrix => ActiveWorkset?.ToleranceMatrix;
    public IReadOnlyList<ClusterResult> Results  => ActiveWorkset?.Results ?? [];

    public event EventHandler? DataChanged;
    public event EventHandler? ResultsChanged;

    public Workset LoadCsv(string path)
    {
        var labels = LabeledDataset.TryFromCsv(path);
        var raw    = labels?.Data ?? RawDataMatrix.FromCsv(path);
        var ws = new Workset
        {
            Name         = Path.GetFileName(path),
            SourcePath   = path,
            RawData      = raw,
            Labels       = labels,
            FeatureNames = ReadFeatureNames(path, raw.Cols)
        };
        _worksets.Add(ws);
        Activate(ws);
        return ws;
    }

    public Workset LoadMatrix(string path)
    {
        var ws = new Workset
        {
            Name            = Path.GetFileName(path),
            SourcePath      = path,
            IsMatrixInput   = true,
            ToleranceMatrix = FuzzyToleranceMatrix.FromCsv(path)
        };
        _worksets.Add(ws);
        Activate(ws);
        return ws;
    }

    public void Activate(Workset workset)
    {
        if (!_worksets.Contains(workset))
            throw new InvalidOperationException("Unknown workset.");
        ActiveWorkset = workset;
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BuildToleranceMatrix(IDistance distance, string normalization, double? accuracy)
    {
        var ws = ActiveWorkset ?? throw new InvalidOperationException("No workset active.");
        if (ws.RawData is null) throw new InvalidOperationException("No data loaded.");
        var builder = new ToleranceMatrixBuilder(distance, NormalizationFor(normalization), accuracy);
        ws.ToleranceMatrix = builder.Build(ws.RawData);
        ws.ClosureApplied  = false;
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ComputeTransitiveClosure()
    {
        var ws = ActiveWorkset ?? throw new InvalidOperationException("No workset active.");
        if (ws.ToleranceMatrix is null) throw new InvalidOperationException("Build tolerance matrix first.");
        ws.ToleranceMatrix = new MaxMinTransitiveClosure().Compute(ws.ToleranceMatrix);
        ws.ClosureApplied  = true;
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private static readonly HashSet<string> LabelColumnKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "class", "label", "target", "species", "type", "category", "y" };

    /// <summary>
    /// Header names of the numeric feature columns, in the order the loaders keep them.
    /// Falls back to x1…xm when there is no header or the names cannot be matched to the columns.
    /// </summary>
    private static IReadOnlyList<string> ReadFeatureNames(string path, int cols)
    {
        var fallback = Enumerable.Range(1, cols).Select(i => $"x{i}").ToArray();
        var lines = File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).Take(2).ToArray();
        if (lines.Length < 2) return fallback;

        static bool IsNumber(string s) => double.TryParse(s.Trim(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

        var header = lines[0].Split(',').Select(h => h.Trim().TrimStart('\uFEFF')).ToArray();
        if (IsNumber(header[0])) return fallback;

        var sample = lines[1].Split(',');
        var names = Enumerable.Range(0, System.Math.Min(header.Length, sample.Length))
            .Where(j => IsNumber(sample[j]))
            .Select(j => header[j])
            .ToList();
        // A numeric label column (e.g. wine's "class") is dropped by the labelled loader.
        if (names.Count == cols + 1)
            names.Remove(names.FirstOrDefault(LabelColumnKeywords.Contains) ?? names[^1]);
        return names.Count == cols ? names : fallback;
    }

    /// <summary>Maps the panel / CLI key (minmax | max | none) to the normalisation, as AlgorithmFactory does.</summary>
    public static INormalization NormalizationFor(string key) => key.ToLowerInvariant() switch
    {
        "max"  => MaxNormalization.Instance,
        "none" => NoNormalization.Instance,
        _      => MinMaxNormalization.Instance
    };

    public void SetResults(IReadOnlyList<ClusterResult> results)
    {
        var ws = ActiveWorkset ?? throw new InvalidOperationException("No workset active.");
        ws.Results = results;
        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }
}
