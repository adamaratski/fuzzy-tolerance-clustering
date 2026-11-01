using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-AFC-TAGA — D-AFC with TAGA aggregation instead of transitive closure.
///
/// Pipeline:
///   1. Build fuzzy tolerance matrix T
///   2. Apply TAGA aggregation → T'
///   3. Run D-PAFC (auto c) on T' across all alpha values
///   4. Select best result by geometric deviation in original data space
///      combined with F1 score.
/// </summary>
public sealed class DAfcTagaAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TAGAProcessor          taga,
    ICriterion             criterion,
    int                    minClusterPower  = 1,
    int                    maxClusterPower  = 0,
    int                    maxIntersections = DAfcOptions.AutoIntersections) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var Tp = PrototypePipeline.BuildWithTaga(data, toleranceBuilder, taga);

        var dpafc = new DPafcAlgorithm(
            AllDistinctAlphaSelector.Instance,
            criterion,
            minClusterPower,
            maxClusterPower,
            maxIntersections);

        var results = dpafc.Run(Tp, ct);
        if (results.Count == 0) return [];

        // Re-rank by geometric deviation in original space (lower = better compact clusters)
        var ranked = results
            .Select(r => (result: r, dev: ComputeGeometricDeviation(data, r)))
            .OrderBy(x => x.dev)
            .ThenByDescending(x => x.result.Score)
            .Select(x => x.result)
            .ToList();

        return ranked;
    }

    private static double ComputeGeometricDeviation(RawDataMatrix data, ClusterResult result)
    {
        double totalDev = 0.0;
        int m = data.Cols;
        var centroid = new double[m];

        foreach (var memberSet in result.MemberSets)
        {
            if (memberSet.Length == 0) continue;

            // Compute geometric center
            Array.Clear(centroid, 0, m);
            foreach (var idx in memberSet)
            {
                var row = data.GetRow(idx);
                for (int j = 0; j < m; j++)
                    centroid[j] += row[j];
            }
            for (int j = 0; j < m; j++)
                centroid[j] /= memberSet.Length;

            // Sum squared distances from centroid
            foreach (var idx in memberSet)
            {
                var row = data.GetRow(idx);
                double dist = 0.0;
                for (int j = 0; j < m; j++)
                {
                    double d = row[j] - centroid[j];
                    dist += d * d;
                }
                totalDev += dist;
            }
        }

        return totalDev;
    }
}
