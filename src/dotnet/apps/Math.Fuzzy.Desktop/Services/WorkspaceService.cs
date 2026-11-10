using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Asymmetric;
using MathFuzzy.Core.FuzzyData;
using MathFuzzy.Core.Interval;
using MathFuzzy.Core.Intuitionistic;
using MathFuzzy.Core.Normalization;
using MathFuzzy.Core.ThreeWay;
using System.IO;
using MathFuzzy.Core.Matrix;

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
        var file = MathFuzzy.Core.IO.DataFile.Load(path);
        var ws = new Workset
        {
            Name         = Path.GetFileName(path),
            SourcePath   = path,
            RawData      = file.Data,
            Labels       = LabeledDataset.From(file),
            FeatureNames = file.FeatureNames
        };
        _worksets.Add(ws);
        Activate(ws);
        return ws;
    }

    public Workset LoadMatrix(string path)
    {
        var matrix = FuzzyToleranceMatrix.FromCsv(path);
        var ws = new Workset
        {
            Name            = Path.GetFileName(path),
            SourcePath      = path,
            IsMatrixInput   = true,
            InputMatrix     = matrix,
            ToleranceMatrix = matrix.Clone()
        };
        _worksets.Add(ws);
        Activate(ws);
        return ws;
    }

    public Workset LoadIntervalData(string path)
    {
        var data = IntervalDataMatrix.FromCsv(path);
        return AddDerived(path, IntervalTolerance.Build(data, IntervalMeasure.SquaredEuclidean),
                          $"{data.Objects} × {data.Attributes} · interval-valued data · squared Euclidean measure");
    }

    public Workset LoadFuzzyNumberData(string path)
    {
        var data = FuzzyNumberDataMatrix.FromCsv(path);
        return AddDerived(path, data.BuildTolerance(), $"{data.Objects} × {data.Attributes} · fuzzy-number data");
    }

    public Workset LoadThreeWayData(string path)
    {
        var data = ThreeWayDataMatrix.FromCsv(path);
        return AddDerived(path, data.BuildTolerance(new MinMaxNormalization(), ThreeWayDistance.SquaredEuclidean),
                          $"{data.Objects} × {data.Attributes} × {data.Situations} · three-way data");
    }

    public Workset LoadAsymmetricMatrix(string path)
    {
        var data = AsymmetricProximityMatrix.FromCsv(path);
        return AddDerived(path, data.BuildTolerance(), $"{data.Size} × {data.Size} · asymmetric data · quasi-equivalence");
    }

    /// <summary>A workset whose tolerance was built from data of another kind; the algorithms run on the tolerance.</summary>
    private Workset AddDerived(string path, FuzzyToleranceMatrix tolerance, string description)
    {
        var ws = new Workset
        {
            Name            = Path.GetFileName(path),
            SourcePath      = path,
            IsMatrixInput   = true,
            InputMatrix     = tolerance,
            ToleranceMatrix = tolerance.Clone(),
            DerivedFrom     = description
        };
        _worksets.Add(ws);
        Activate(ws);
        return ws;
    }

    public Workset LoadIntuitionisticRelation(string membershipPath, string nonMembershipPath)
    {
        var relation = IntuitionisticToleranceMatrix.FromCsv(membershipPath, nonMembershipPath);
        return AddIntuitionistic(Path.GetFileName(membershipPath), membershipPath, relation, null);
    }

    public Workset LoadIntuitionisticData(string path)
    {
        var data = IntuitionisticDataMatrix.FromCsv(path);
        // the Hamming-type tolerance is what the heat map shows; a run rebuilds it with the chosen measure
        var relation = IntuitionisticSimilarity.BuildTolerance(data, IntuitionisticMeasure.Hamming);
        return AddIntuitionistic(Path.GetFileName(path), path, relation, data);
    }

    private Workset AddIntuitionistic(string name, string path, IntuitionisticToleranceMatrix relation, IntuitionisticDataMatrix? data)
    {
        var ws = new Workset
        {
            Name                    = name,
            SourcePath              = path,
            IsMatrixInput           = true,
            IntuitionisticTolerance = relation,
            IntuitionisticData      = data,
            // the membership part is shown on the Tolerance tab
            ToleranceMatrix         = FuzzyToleranceMatrix.FromArray(relation.MembershipMatrix())
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
