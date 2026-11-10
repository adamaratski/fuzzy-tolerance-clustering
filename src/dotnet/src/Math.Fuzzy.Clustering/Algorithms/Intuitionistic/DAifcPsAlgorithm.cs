using MathFuzzy.Clustering.Algorithms.Supervision;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Intuitionistic;

namespace MathFuzzy.Clustering.Algorithms.Intuitionistic;

/// <summary>When D-AIFC-PS(c) increases the bound w on the objects shared by two clusters.</summary>
public enum SupervisedIntersectionRule
{
    /// <summary>
    /// Step 10 as printed: at the first pair of thresholds that admits an allotment among c
    /// clusters with w = 0, w is increased at that same pair while none of its allotments is
    /// consistent with the labelled objects, up to w = n − c; then there is no solution. If no
    /// pair admits an allotment with w = 0, there is no solution either (Step 9 of the printing
    /// is unreachable).
    /// </summary>
    AtCurrentPair,

    /// <summary>
    /// As in D-AIFC(c) and D-AFC-PS(c): for w = 0, 1, … the whole scan over the pairs is made,
    /// and an allotment that is not consistent with the labelled objects does not stop it; w is
    /// increased only when no pair has a consistent allotment.
    /// </summary>
    AfterFullScan
}

/// <summary>
/// D-AIFC-PS(c) — the direct algorithm for detecting the allotment among c intuitionistic fuzzy
/// clusters with partial supervision (Kacprzyk, Owsiński, Shyrai, Szmidt and Viattchenin 2017,
/// unpublished manuscript; twelve steps).
///
/// Labelled objects x_L(l) carry a membership grade y and a non-membership grade f with
/// 0 ≤ y + f ≤ 1 (Step 1; otherwise "the conditions of classification are incorrect"). An
/// allotment among c partially separated intuitionistic fuzzy clusters is consistent with them
/// if each labelled object belongs to the carrier of the cluster of its class with μ ≥ y and
/// ν ≤ f (the text of the manuscript says "greater than" and "lower than", its Step 10 ≥ and ≤;
/// with y = 1 only ≥ is satisfiable). The pairs of thresholds are scanned in the order
/// <see cref="ThresholdScanOrder.BetaInner"/> (as printed) or <see cref="ThresholdScanOrder.BetaOuter"/>;
/// the consistent allotments of the stopping pair are returned ordered by F_IFS. The rule for
/// increasing w is <see cref="SupervisedIntersectionRule"/>.
/// </summary>
public sealed class DAifcPsAlgorithm
{
    private readonly int _clusterCount;
    private readonly IReadOnlyList<IntuitionisticLabelledObject> _labels;
    private readonly IntuitionisticOptions _options;
    private readonly SupervisedIntersectionRule _rule;

    public DAifcPsAlgorithm(int clusterCount, IReadOnlyList<IntuitionisticLabelledObject> labels,
                            IntuitionisticOptions? options = null,
                            SupervisedIntersectionRule rule = SupervisedIntersectionRule.AtCurrentPair)
    {
        // Step 1: the conditions of classification.
        foreach (var label in labels)
        {
            bool correct = label.Membership > 0.0 && label.Membership <= 1.0
                           && label.NonMembership >= 0.0 && label.NonMembership < 1.0
                           && label.Membership + label.NonMembership <= 1.0 + IntuitionisticToleranceMatrix.Tolerance;
            if (!correct)
                throw new ArgumentException(
                    $"The conditions of classification are incorrect: the labelled object x{label.ObjectIndex + 1} has " +
                    $"y = {label.Membership}, f = {label.NonMembership}; y must lie in (0, 1], f in [0, 1), and y + f ≤ 1.");
        }
        _clusterCount = clusterCount;
        _labels = labels;
        _options = options ?? new IntuitionisticOptions();
        _rule = rule;
    }

    /// <summary>The labelled objects of this run.</summary>
    public IReadOnlyList<IntuitionisticLabelledObject> Labels => _labels;

    public IReadOnlyList<ClusterResult> Run(IntuitionisticToleranceMatrix relation, CancellationToken ct = default)
    {
        int objectCount = relation.Size;
        if (_clusterCount < 2 || _clusterCount >= objectCount)
            throw new ArgumentException($"The number of clusters must satisfy 2 ≤ c < n = {objectCount}; got {_clusterCount}.");
        if (_options.ScanOrder == ThresholdScanOrder.OrderFree)
            throw new ArgumentException(
                "The order-free definition is available for D-PAIFC and D-PAIFC-TC; D-AIFC-PS(c) scans the pairs of thresholds in the order beta-inner or beta-outer.");
        _ = LabelConstraint.Group(   // checks the object numbers and the number of classes
[.. _labels.Select(label => label.ObjectIndex)],
                                            [.. _labels.Select(label => label.Class)], _clusterCount, objectCount);

        // the consistency filter is applied after the search, so the search must not truncate
        var searchOptions = _options with { MaxResults = int.MaxValue };

        IReadOnlyList<ClusterResult> Consistent(IReadOnlyList<ClusterResult> found)
        {
            var kept = found.Where(result => LabelConstraint.AssignClasses(result, _labels) is not null).ToList();
            return kept.Count > _options.MaxResults ? kept.GetRange(0, _options.MaxResults) : kept;
        }

        if (_rule == SupervisedIntersectionRule.AfterFullScan)
        {
            for (int intersectionBound = 0; intersectionBound <= objectCount; intersectionBound++)
            {
                foreach (var (alpha, beta) in IntuitionisticSearch.ThresholdPairs(relation, _options.ScanOrder))
                {
                    ct.ThrowIfCancellationRequested();
                    var clusters = IntuitionisticSearch.InitialAllotment(relation, alpha, beta);
                    if (clusters is null) continue;
                    var found = Consistent(IntuitionisticSearch.Allotments(relation, clusters, _clusterCount, intersectionBound,
                                                                           alpha, beta, searchOptions, ct, out bool exhausted));
                    if (found.Count > 0 || exhausted) return found;
                }
            }
            return [];
        }

        // Steps 3–10 as printed: the first pair with an allotment among c clusters (w = 0) is the
        // pair of the result; w grows there while none of its allotments is consistent.
        foreach (var (alpha, beta) in IntuitionisticSearch.ThresholdPairs(relation, _options.ScanOrder))
        {
            ct.ThrowIfCancellationRequested();
            var clusters = IntuitionisticSearch.InitialAllotment(relation, alpha, beta);
            if (clusters is null) continue;   // Step 6

            var found = IntuitionisticSearch.Allotments(relation, clusters, _clusterCount, intersectionBound: 0,
                                                        alpha, beta, searchOptions, ct, out bool exhausted);
            if (exhausted) return Consistent(found);
            if (found.Count == 0) continue;   // Step 8

            for (int intersectionBound = 0; ; intersectionBound++)   // Step 10
            {
                var consistent = Consistent(found);
                if (consistent.Count > 0 || exhausted) return consistent;
                if (intersectionBound >= objectCount - _clusterCount) return [];   // "no solution"
                found = IntuitionisticSearch.Allotments(relation, clusters, _clusterCount, intersectionBound + 1,
                                                        alpha, beta, searchOptions, ct, out exhausted);
            }
        }
        return [];
    }
}
