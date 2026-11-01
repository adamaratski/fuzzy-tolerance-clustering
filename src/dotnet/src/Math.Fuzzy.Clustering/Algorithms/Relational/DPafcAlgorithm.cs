using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Relational;

/// <summary>
/// D-PAFC — Direct Principal Allotment Fuzzy Clustering (Viattchenin 2013, sec. 2.2.3).
///
/// The number of clusters is not given. The α-levels of T are scanned in ascending
/// order; levels at which some α-neighbourhood still covers the whole set X are
/// skipped (step 3). At the first remaining level that admits an allotment, the
/// cluster count is increased from c = 2 until an allotment among c clusters with at
/// most <c>maxIntersections</c> objects in intersection areas exists; that class of
/// solutions B(α) is returned, ordered by the criterion (best first).
///
/// With <c>maxIntersections = 0</c> (the default) the clusters are fully separated and
/// the result is the principal allotment among the minimal number c* of clusters.
/// A positive value admits particularly separated clusters with up to that many
/// objects in intersection areas.
/// </summary>
public sealed class DPafcAlgorithm(
    IAlphaSelector alphaSelector,
    ICriterion     criterion,
    int            minClusterPower  = 1,
    int            maxClusterPower  = 0,
    int            maxIntersections = 0,
    Action<int, int>? levelProgress = null) : IRelationalClusterer
{
    public IReadOnlyList<ClusterResult> Run(FuzzyToleranceMatrix matrix, CancellationToken ct = default)
    {
        int n = matrix.Size;
        if (n < 2) return [];
        int w = Math.Max(0, maxIntersections);
        int maxPower = maxClusterPower > 0 ? maxClusterPower : n;

        var alphas = alphaSelector.Select(matrix).Distinct().Order().ToArray();
        var powers = new int[n];

        for (int z = 0; z < alphas.Length; z++)
        {
            ct.ThrowIfCancellationRequested();
            if (levelProgress is not null && ((z & 31) == 0 || z == alphas.Length - 1))
                levelProgress(z + 1, alphas.Length);

            double alpha = alphas[z];
            for (int i = 0; i < n; i++)
                powers[i] = CountAtLeast(matrix.GetRow(i), alpha);

            // Step 3: the whole set is still a single cluster at this level.
            if (powers.Contains(n)) continue;

            // Feasible cluster counts at this level: the c largest candidate clusters must
            // cover X, and the c smallest must not exceed X by more than w objects.
            var candidatePowers = powers.Where(p => p >= minClusterPower && p <= maxPower).Order().ToArray();
            int k = candidatePowers.Length;
            if (k < 2) continue;

            int cMin = 0;
            for (int c = 1, covered = 0; c <= k; c++)
            {
                covered += candidatePowers[k - c];
                if (covered >= n) { cMin = Math.Max(2, c); break; }
            }
            if (cMin == 0) continue;

            int cMax = 0;
            for (int c = 1, least = 0; c <= k; c++)
            {
                least += candidatePowers[c - 1];
                if (least - n > w) break;
                cMax = c;
            }

            // Step 4: the smallest c with an allotment at this level gives B(α).
            for (int c = cMin; c <= cMax; c++)
            {
                var dafc = new DAfcAlgorithm(
                    new DAfcOptions(c, minClusterPower, maxClusterPower, MaxIntersections: w),
                    alphaSelector, criterion);
                var found = dafc.RunForAlpha(matrix, alpha, ct);
                if (found.Count > 0)
                    return [.. found.OrderByDescending(r => r.Score).ThenBy(r => r, ClusterResult.Order)];
            }
        }

        return [];
    }

    private static int CountAtLeast(ReadOnlySpan<double> row, double alpha)
    {
        int count = 0;
        foreach (double v in row)
            if (v >= alpha) count++;
        return count;
    }
}
