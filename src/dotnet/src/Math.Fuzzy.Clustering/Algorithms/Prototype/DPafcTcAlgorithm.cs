using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-PAFC-TC — principal allotment with transitive closure (Viattchenin 2013, sec. 2.3.2).
///
/// The leap heuristic is not used: for the ascending sequence α₀ &lt; α₁ &lt; … of levels
/// of T̃ the α₀-cut is the whole set (Lemma 1.2), so the principal allotment — the
/// allotment among the minimal number c* ≥ 2 of fully separated clusters — is
/// constructed at α₁, the first level at which the closure splits into several
/// equivalence classes. Steps 4–9 of D-AFC-TC (single-level D-AFC with the geometric
/// tiebreaker) are then applied at that level.
///
/// Pass <paramref name="exhaustive"/> = true to obtain the former behaviour: a sweep
/// over c = 2..n and all levels of T̃ returning every valid allotment ranked by score.
/// </summary>
public sealed class DPafcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower  = 1,
    int                     maxClusterPower  = 0,
    int                     maxIntersections = DAfcOptions.AutoIntersections,
    bool                    exhaustive       = false) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var Tt = PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);

        if (exhaustive)
        {
            var dpafc = new DPafcAlgorithm(
                AllDistinctAlphaSelector.Instance,
                criterion,
                minClusterPower,
                maxClusterPower,
                maxIntersections);
            return dpafc.Run(Tt, ct);
        }

        int n = Tt.Size;
        var ascending = Tt.GetDistinctValues().OrderBy(a => a).ToArray();

        // α₁ = first level whose cut has at least two equivalence classes.
        double? alphaStar = null;
        int classCount = 0;
        foreach (var alpha in ascending)
        {
            classCount = PrototypePipeline.CountAlphaClasses(Tt, alpha);
            if (classCount >= 2) { alphaStar = alpha; break; }
        }
        if (alphaStar is not double a1 || classCount > n) return [];

        var normalized = toleranceBuilder.Normalize(data);
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(classCount, minClusterPower, maxClusterPower, maxIntersections),
            new ManualAlphaSelector([a1]),
            criterion);

        var results = dafc.RunForAlpha(Tt, a1, ct);
        return [.. results
            .Select(r => r with { TiebreakerScore = DAfcTcAlgorithm.ComputeTiebreakerScore(r, normalized) })
            .Order(ClusterResult.Order)];
    }

    public FuzzyToleranceMatrix BuildClosureMatrix(RawDataMatrix data)
        => PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);
}
