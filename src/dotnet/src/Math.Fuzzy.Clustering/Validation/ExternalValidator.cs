using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Clustering.Validation;

/// <summary>
/// External clustering validation indices — compare cluster assignments
/// against ground-truth class labels.
/// All indices are in [0, 1] (higher = better agreement with ground truth).
/// </summary>
public sealed record ExternalValidationResult(
    double   Purity,
    double   RandIndex,
    double   AdjustedRandIndex,
    double   NormalizedMutualInfo,
    double   FowlkesMallows,
    int[]    AssignedLabels,    // majority-class index per cluster
    string[] ClusterMajority   // majority class name per cluster
);

public static class ExternalValidator
{
    /// <summary>
    /// Computes all external indices for a flat clustering result. Objects that belong to
    /// several clusters are assigned to the cluster of their largest membership.
    /// </summary>
    /// <param name="result">Clustering output.</param>
    /// <param name="trueLabels">Ground-truth 0-based class index, one per object.</param>
    /// <param name="classNames">Human-readable class names indexed by trueLabels value.</param>
    public static ExternalValidationResult Compute(
        ClusterResult result, int[] trueLabels, string[] classNames)
    {
        int n = trueLabels.Length;
        int c = result.ClusterCount;
        int k = classNames.Length;

        // clusterOf[obj] = cluster index. An object in an intersection area belongs to several
        // clusters; it is counted in the one where its membership is largest (the first such
        // cluster on a tie), which is how a possibilistic allotment is hardened.
        var clusterOf = new int[n];
        var bestMembership = new double[n];
        Array.Fill(bestMembership, double.NegativeInfinity);
        for (int ci = 0; ci < result.MemberSets.Length; ci++)
        foreach (var obj in result.MemberSets[ci])
        {
            double membership = result.Membership[obj, ci];
            if (membership > bestMembership[obj])
            {
                bestMembership[obj] = membership;
                clusterOf[obj] = ci;
            }
        }

        // Contingency table nij[cluster, class]
        var nij = new int[c, k];
        for (int obj = 0; obj < n; obj++)
            nij[clusterOf[obj], trueLabels[obj]]++;

        var rowSum = new int[c];
        var colSum = new int[k];
        for (int ci = 0; ci < c; ci++)
        for (int ki = 0; ki < k; ki++)
        {
            rowSum[ci] += nij[ci, ki];
            colSum[ki] += nij[ci, ki];
        }

        // ── Purity ─────────────────────────────────────────────────────────
        double purity = 0;
        var assignedLabels  = new int[c];
        var clusterMajority = new string[c];
        for (int ci = 0; ci < c; ci++)
        {
            int maxCount = 0, maxClass = 0;
            for (int ki = 0; ki < k; ki++)
                if (nij[ci, ki] > maxCount) { maxCount = nij[ci, ki]; maxClass = ki; }
            purity += maxCount;
            assignedLabels[ci]  = maxClass;
            clusterMajority[ci] = classNames[maxClass];
        }
        purity /= n;

        // ── Rand Index ─────────────────────────────────────────────────────
        long tp = 0, fp = 0, fn = 0, tn = 0;
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            bool sc = clusterOf[i] == clusterOf[j];
            bool sk = trueLabels[i] == trueLabels[j];
            if      ( sc &&  sk) tp++;
            else if ( sc && !sk) fp++;
            else if (!sc &&  sk) fn++;
            else                 tn++;
        }
        long pairs = (long)n * (n - 1) / 2;
        double ri = pairs > 0 ? (double)(tp + tn) / pairs : 1.0;

        // ── Adjusted Rand Index (contingency table formula) ─────────────────
        long a        = 0;
        for (int ci = 0; ci < c; ci++)
        for (int ki = 0; ki < k; ki++)
            a += C2(nij[ci, ki]);

        long sumR = rowSum.Sum(r => C2(r));
        long sumC = colSum.Sum(col => C2(col));
        long tot  = C2(n);

        double ari;
        double ariDenom = 0.5 * (sumR + sumC) - (double)sumR * sumC / tot;
        ari = ariDenom == 0.0
            ? 1.0
            : (a - (double)sumR * sumC / tot) / ariDenom;

        // ── NMI ────────────────────────────────────────────────────────────
        double mi = 0;
        for (int ci = 0; ci < c; ci++)
        for (int ki = 0; ki < k; ki++)
        {
            if (nij[ci, ki] == 0) continue;
            double p = (double)nij[ci, ki] / n;
            mi += p * Math.Log((double)nij[ci, ki] * n / ((double)rowSum[ci] * colSum[ki]));
        }
        double hK = Entropy(colSum, n);
        double hC = Entropy(rowSum, n);
        double nmi = (hK + hC) > 0 ? Math.Max(0, 2.0 * mi / (hK + hC)) : 1.0;

        // ── Fowlkes-Mallows ────────────────────────────────────────────────
        double fm = (tp + fp) > 0 && (tp + fn) > 0
            ? tp / Math.Sqrt((double)(tp + fp) * (tp + fn))
            : 0.0;

        return new ExternalValidationResult(purity, ri, ari, nmi, fm, assignedLabels, clusterMajority);
    }

    private static long C2(long value) => value < 2 ? 0 : value * (value - 1) / 2;

    private static double Entropy(int[] counts, int total)
    {
        double entropy = 0;
        foreach (var cnt in counts)
        {
            if (cnt == 0) continue;
            double prob = (double)cnt / total;
            entropy -= prob * Math.Log(prob);
        }
        return entropy;
    }
}
