using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Supervision;

/// <summary>
/// D-AFC-PS(c) — the direct algorithm for detecting the allotment among c fuzzy clusters with
/// partial supervision (Viattchenin 2007; 2013, sec. 2.2.2).
///
/// The algorithm is D-AFC(c) with a modified Step 5: an allotment enters the class of possible
/// solutions B(c) only if, besides the coverage, separation and power conditions, every
/// labelled object x_k belongs to the cluster of its class with the membership degree
/// μ_lk ≥ y_k. The labelled objects of one class must lie in one cluster and different classes
/// in different clusters; which cluster carries which class is free. As in D-AFC(c), the search
/// starts with fully separated clusters (w = 0) and w is increased by one while no admissible
/// allotment exists at any level — an allotment that violates the labels is not admissible.
/// The search of <see cref="DAfcAlgorithm"/> is reused; the labels enter it as
/// <see cref="DAfcOptions.AllotmentFilter"/>.
/// </summary>
public sealed class DAfcPsAlgorithm : IRelationalClusterer
{
    private readonly DAfcOptions _options;
    private readonly IReadOnlyList<LabelledObject> _labels;
    private readonly IAlphaSelector _alphaSelector;
    private readonly ICriterion _criterion;

    /// <param name="options">The settings of D-AFC(c); an <see cref="DAfcOptions.AllotmentFilter"/> set there is applied as well.</param>
    /// <param name="labels">The labelled objects with their least membership degrees.</param>
    /// <param name="alphaSelector">The levels to scan; null = all distinct values of the tolerance.</param>
    /// <param name="criterion">The criterion that ranks B(c); null = F1.</param>
    public DAfcPsAlgorithm(DAfcOptions options, IReadOnlyList<LabelledObject> labels,
                           IAlphaSelector? alphaSelector = null, ICriterion? criterion = null)
    {
        foreach (var label in labels)
            if (!(label.Membership > 0.0 && label.Membership <= 1.0))
                throw new ArgumentException(
                    $"The membership degree of the labelled object x{label.ObjectIndex + 1} must lie in (0, 1]; got {label.Membership}.");
        _options = options;
        _labels = labels;
        _alphaSelector = alphaSelector ?? AllDistinctAlphaSelector.Instance;
        _criterion = criterion ?? F1Criterion.Instance;
    }

    /// <summary>The labelled objects of this run.</summary>
    public IReadOnlyList<LabelledObject> Labels => _labels;

    /// <summary>Runs the algorithm on a fuzzy tolerance; results best first, as D-AFC(c).</summary>
    public IReadOnlyList<ClusterResult> Run(FuzzyToleranceMatrix matrix, CancellationToken ct = default)
    {
        int clusterCount = _options.ClusterCount;
        var classes = LabelConstraint.Group([.. _labels.Select(label => label.ObjectIndex)],
                                            [.. _labels.Select(label => label.Class)], clusterCount, matrix.Size);
        var objectIndices = classes.ObjectIndices;
        var leastMembership = _labels.Select(label => label.Membership).ToArray();
        var previousFilter = _options.AllotmentFilter;

        bool Consistent(int[] centers, int[][] memberSets)
        {
            if (previousFilter is not null && !previousFilter(centers, memberSets)) return false;
            return LabelConstraint.Assign(classes.Count, centers.Length, (classIndex, cluster) =>
            {
                foreach (int label in classes.Members[classIndex])
                {
                    int objectIndex = objectIndices[label];
                    if (Array.BinarySearch(memberSets[cluster], objectIndex) < 0) return false;
                    if (!LabelConstraint.AtLeast(matrix[centers[cluster], objectIndex], leastMembership[label])) return false;
                }
                return true;
            }) is not null;
        }

        var supervised = _options with { AllotmentFilter = Consistent };
        return new DAfcAlgorithm(supervised, _alphaSelector, _criterion).Run(matrix, ct);
    }

    /// <summary>
    /// Runs the algorithm on attribute data: the tolerance is built by the builder (normalisation,
    /// distance, accuracy threshold), then D-AFC-PS(c) is applied to it.
    /// </summary>
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, ToleranceMatrixBuilder builder, CancellationToken ct = default) =>
        Run(builder.Build(data), ct);
}
