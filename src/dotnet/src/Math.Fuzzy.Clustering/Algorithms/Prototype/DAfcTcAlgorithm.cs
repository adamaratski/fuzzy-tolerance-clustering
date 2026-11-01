using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TC — Direct AFC with Transitive Closure.
///
/// Pipeline:
///   1. Build fuzzy tolerance matrix T from raw data
///   2. Compute max-min transitive closure T̃
///   3. Select single alpha* via leap heuristic
///   4. Run D-AFC on T̃ at alpha*
///
/// Cluster count c is determined structurally: on a transitive closure the α-cut
/// relation is an equivalence, so valid allotments at alpha* exist only for
/// c = number of equivalence classes — a single D-AFC run suffices.
/// </summary>
public sealed class DAfcTcAlgorithm(
    ToleranceMatrixBuilder  toleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower  = 1,
    int                     maxClusterPower  = 0,
    int                     maxIntersections = DAfcOptions.AutoIntersections) : IPrototypeClusterer
{
    private readonly LeapHeuristicAlphaSelector _leap = LeapHeuristicAlphaSelector.Instance;

    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var Tt     = PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);
        var alphas = _leap.Select(Tt);
        if (alphas.Count == 0) return [];

        double alpha = alphas[0];
        int n = Tt.Size;

        // Normalised data is used for tiebreaker centroid distances (same space as tolerance matrix).
        var normalized = toleranceBuilder.Normalize(data);

        // On T̃ the α-cuts partition X into equivalence classes; only c = class count
        // can satisfy the coverage condition, so other c values need not be tried.
        int classCount = PrototypePipeline.CountAlphaClasses(Tt, alpha);
        if (classCount < 2 || classCount > n) return [];

        var dafc = new DAfcAlgorithm(
            new DAfcOptions(classCount, minClusterPower, maxClusterPower, maxIntersections),
            new ManualAlphaSelector([alpha]),
            criterion);

        var allResults = dafc.RunForAlpha(Tt, alpha, ct);

        // Step 8 (Viattchenin): assign geometric tiebreaker scores, then rank by the
        // deterministic total order (score desc, tiebreaker asc, alpha, c, centers).
        return [.. allResults
            .Select(r => r with { TiebreakerScore = ComputeTiebreakerScore(r, normalized) })
            .Order(ClusterResult.Order)];
    }

    /// <summary>
    /// Computes Σ dist(typical_point_l, centroid_l) in normalised feature space.
    /// Lower values indicate that typical points are closer to the geometric centres of their clusters.
    /// </summary>
    internal static double ComputeTiebreakerScore(ClusterResult r, RawDataMatrix normalized)
    {
        double total = 0.0;
        int m = normalized.Cols;

        for (int l = 0; l < r.ClusterCount; l++)
        {
            var members = r.MemberSets[l];
            if (members.Length == 0) continue;

            // Geometric centroid of cluster l
            var centroid = new double[m];
            foreach (int idx in members)
            {
                var row = normalized.GetRow(idx);
                for (int j = 0; j < m; j++)
                    centroid[j] += row[j];
            }
            for (int j = 0; j < m; j++)
                centroid[j] /= members.Length;

            // Euclidean distance from typical point to centroid
            var centerRow = normalized.GetRow(r.Centers[l]);
            double sumSq = 0.0;
            for (int j = 0; j < m; j++)
            {
                double d = centerRow[j] - centroid[j];
                sumSq += d * d;
            }
            total += Math.Sqrt(sumSq);
        }

        return total;
    }

    /// <summary>Exposes the transitive closure matrix for inspection (e.g. UI).</summary>
    public FuzzyToleranceMatrix BuildClosureMatrix(RawDataMatrix data)
        => PrototypePipeline.BuildWithClosure(data, toleranceBuilder, closure);
}
