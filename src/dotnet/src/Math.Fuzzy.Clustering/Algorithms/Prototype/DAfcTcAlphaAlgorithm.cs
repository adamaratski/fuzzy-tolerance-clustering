using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TC(α) — D-AFC-TC with user-specified tolerance threshold.
///
/// Same as D-AFC-TC, but the result must satisfy α ≥ userAlpha:
/// alpha* comes from the leap heuristic; when it falls below userAlpha,
/// the heuristic is re-applied to the subsequence {αₛ : αₛ ≥ userAlpha}
/// (per the algorithm specification, docs/algorithms.md §D-AFC-TC(α)).
/// </summary>
public sealed class DAfcTcAlphaAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    double                  userAlpha,
    int                     minClusterPower  = 1,
    int                     maxClusterPower  = 0,
    int                     maxIntersections = DAfcOptions.AutoIntersections) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var Tt = PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);

        // Resolve effective alpha (Viattchenin 2013, D-AFC-TC(α), step 4): leap heuristic on
        // the full sequence; if α* < userAlpha, re-apply it to the levels above userAlpha.
        var allAlphas = AllDistinctAlphaSelector.Instance.Select(Tt); // descending
        double? leapAlpha = LeapHeuristicAlphaSelector.SelectFrom(allAlphas);
        if (leapAlpha is not double effectiveAlpha) return [];
        if (effectiveAlpha < userAlpha)
        {
            var constrained = allAlphas.Where(a => a > userAlpha).ToArray(); // still descending
            if (LeapHeuristicAlphaSelector.SelectFrom(constrained) is not double constrainedAlpha)
                return []; // userAlpha above every level in T̃
            effectiveAlpha = constrainedAlpha;
        }

        int n = Tt.Size;

        // On T̃ the α-cuts partition X into equivalence classes; only c = class count
        // can satisfy the coverage condition, so other c values need not be tried.
        int classCount = PrototypePipeline.CountAlphaClasses(Tt, effectiveAlpha);
        if (classCount < 2 || classCount > n) return [];

        var dafc = new DAfcAlgorithm(
            new DAfcOptions(classCount, minClusterPower, maxClusterPower, maxIntersections),
            new ManualAlphaSelector([effectiveAlpha]),
            criterion);

        return dafc.RunForAlpha(Tt, effectiveAlpha, ct);
    }
}
