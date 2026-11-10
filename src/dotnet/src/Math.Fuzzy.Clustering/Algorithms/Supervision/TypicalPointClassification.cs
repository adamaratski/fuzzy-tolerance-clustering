using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Supervision;

/// <summary>The two results of <see cref="TypicalPointClassification"/>.</summary>
/// <param name="Allotment">The allotment of the first step; its typical points are the labelled objects.</param>
/// <param name="Labels">The labelled objects: the typical point of cluster l with membership 1 in class l.</param>
/// <param name="Partition">The fuzzy c-partition of the second step, the final result of classification.</param>
public sealed record TypicalPointClassificationResult(
    ClusterResult Allotment, IReadOnlyList<LabelledObject> Labels, SupervisedPartition Partition);

/// <summary>
/// Partially supervised fuzzy classification with labelled objects constructed by a heuristic
/// algorithm of possibilistic clustering (Viattchenin 2013, sec. 4.1): the tolerance is built from
/// the normalised data, D-AFC(c) gives an allotment, its typical points τ^1 … τ^c become the
/// labelled objects with y = 1 in their own class, and Pedrycz's ssfcm-algorithm is applied to the
/// normalised attribute matrix with these labels. The monograph uses D-AFC(c) in the first step;
/// any relational algorithm with a given c can be passed instead, for example D-AFC-PS(c).
/// </summary>
public sealed class TypicalPointClassification(
    ToleranceMatrixBuilder builder,
    PartiallySupervisedFuzzyCMeans? secondStep = null)
{
    private readonly PartiallySupervisedFuzzyCMeans _secondStep = secondStep ?? new PartiallySupervisedFuzzyCMeans();

    /// <summary>Both steps with D-AFC(c) (all levels, criterion F1, w := 0 with automatic increment) in the first.</summary>
    public TypicalPointClassificationResult? Run(RawDataMatrix data, int clusterCount, CancellationToken ct = default) =>
        Run(data, new DAfcAlgorithm(new DAfcOptions(clusterCount), AllDistinctAlphaSelector.Instance, F1Criterion.Instance), ct);

    /// <summary>
    /// Both steps with the given first-step algorithm. Returns null when the first step finds no
    /// allotment. The best allotment of the first step is used.
    /// </summary>
    public TypicalPointClassificationResult? Run(RawDataMatrix data, IRelationalClusterer firstStep, CancellationToken ct = default)
    {
        var allotments = firstStep.Run(builder.Build(data), ct);
        if (allotments.Count == 0) return null;

        var allotment = allotments[0];
        var labels = allotment.Centers
            .Select((typicalPoint, cluster) => new LabelledObject(typicalPoint, 1.0, cluster))
            .ToList();
        var partition = _secondStep.Run(builder.Normalize(data), allotment.ClusterCount, labels, ct);
        return new TypicalPointClassificationResult(allotment, labels, partition);
    }
}
