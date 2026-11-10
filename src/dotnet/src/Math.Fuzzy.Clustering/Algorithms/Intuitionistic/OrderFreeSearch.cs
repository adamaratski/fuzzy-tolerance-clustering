using System.Text;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Intuitionistic;

namespace MathFuzzy.Clustering.Algorithms.Intuitionistic;

/// <summary>
/// The principal allotment among intuitionistic fuzzy clusters defined without a scan order.
///
/// At a pair of thresholds (α, β) the fully separated allotments are the perfect codes of the level
/// graph "μ ≥ α and ν ≤ β". Their number of clusters is the same for all of them, and it does not
/// grow when the pair becomes looser (α smaller, β larger). The pairs are only partially ordered,
/// so "the first admissible pair" depends on the path of a scan: the printed order returns the
/// loosest admissible pair with the least α, the opposite order the one with the largest β, and
/// neither is guaranteed to give the least number of clusters.
///
/// Here all admissible pairs are examined. The loosest admissible pairs form the frontier. The
/// least number of clusters c* is attained on it. Among all fully separated allotments with c*
/// clusters, at whatever pair they occur, the one with the largest
///   Q = Σ_l (mean μ_l − mean ν_l)
/// is the result. At one pair Q differs from the criterion F_IFS by the constant c·(α − β), so the
/// ranking of the printed algorithm is kept, and Q is comparable between pairs. The thresholds
/// reported with an allotment are its own: the least membership and the largest non-membership
/// degree in it, the strictest pair at which the allotment exists.
///
/// For every larger number of clusters that occurs on the frontier the allotments of the frontier
/// pairs with that number follow, best first, so that the whole frontier is visible.
/// </summary>
internal static class OrderFreeSearch
{
    private const double Tolerance = IntuitionisticToleranceMatrix.Tolerance;

    /// <summary>Largest number of allotments kept for a number of clusters other than the least one.</summary>
    private const int KeptAlternatives = 10;

    internal static IReadOnlyList<ClusterResult> Run(
        IntuitionisticToleranceMatrix relation, IntuitionisticOptions options, CancellationToken ct)
    {
        int objectCount = relation.Size;
        var alphas = relation.AlphaSequence();
        var betas = relation.BetaSequence();

        // Different pairs often give the same clusters; each distinct family is searched once.
        var searched = new Dictionary<string, IReadOnlyList<ClusterResult>>(StringComparer.Ordinal);
        var admissible = new List<(int AlphaIndex, int BetaIndex, IReadOnlyList<ClusterResult> Allotments)>();

        for (int alphaIndex = 0; alphaIndex < alphas.Count; alphaIndex++)
        {
            for (int betaIndex = 0; betaIndex < betas.Count; betaIndex++)
            {
                double alpha = alphas[alphaIndex], beta = betas[betaIndex];
                if (alpha + beta > 1.0 + Tolerance) continue;
                ct.ThrowIfCancellationRequested();

                var clusters = IntuitionisticSearch.InitialAllotment(relation, alpha, beta);
                if (clusters is null) continue;   // some cluster is the whole set

                string signature = Signature(clusters);
                if (!searched.TryGetValue(signature, out var found))
                {
                    found = [];
                    for (int clusterCount = 2; clusterCount < objectCount; clusterCount++)
                    {
                        var atCount = IntuitionisticSearch.Allotments(
                            relation, clusters, clusterCount, intersectionBound: 0, alpha, beta, options, ct,
                            out bool budgetExhausted);
                        // without a complete search the least number of clusters is not known
                        if (budgetExhausted) return [];
                        if (atCount.Count == 0) continue;
                        found = atCount;
                        break;
                    }
                    searched[signature] = found;
                }
                if (found.Count > 0) admissible.Add((alphaIndex, betaIndex, found));
            }
        }
        if (admissible.Count == 0) return [];

        // A pair is on the frontier when no other admissible pair is at least as loose in both thresholds.
        // The sequences are ordered from loose to strict, so "looser" is "smaller index".
        bool OnFrontier((int AlphaIndex, int BetaIndex, IReadOnlyList<ClusterResult> Allotments) pair) =>
            !admissible.Any(other => (other.AlphaIndex != pair.AlphaIndex || other.BetaIndex != pair.BetaIndex)
                                     && other.AlphaIndex <= pair.AlphaIndex && other.BetaIndex <= pair.BetaIndex);

        var pairs = admissible
            .Select(pair => new ThresholdPair(alphas[pair.AlphaIndex], betas[pair.BetaIndex],
                                              pair.Allotments[0].ClusterCount, OnFrontier(pair)))
            .ToList();
        if (options.Diagnostics is { } diagnostics) diagnostics.AdmissiblePairs = pairs;

        var frontierCounts = pairs.Where(pair => pair.OnFrontier).Select(pair => pair.ClusterCount).Distinct().Order().ToList();

        var results = new List<ClusterResult>();
        foreach (int clusterCount in frontierCounts)
        {
            // The least number of clusters: every allotment with that number, at whatever pair it occurs,
            // competes for the result. A larger number: what the frontier pairs with that number give,
            // which is what a scan that stops there returns.
            bool least = results.Count == 0;
            var candidates = Enumerable.Range(0, admissible.Count)
                .Where(index => pairs[index].ClusterCount == clusterCount && (least || pairs[index].OnFrontier))
                .SelectMany(index => admissible[index].Allotments);
            results.AddRange(Ranked(candidates).Take(least ? options.MaxResults : KeptAlternatives));
        }
        return results;
    }

    /// <summary>
    /// The distinct allotments (typical points and carriers) with their own thresholds and the
    /// threshold-free quality Q, best first; equal values of Q get exactly the same number.
    /// </summary>
    private static List<ClusterResult> Ranked(IEnumerable<ClusterResult> allotments)
    {
        var distinct = new Dictionary<string, ClusterResult>(StringComparer.Ordinal);
        foreach (var allotment in allotments)
        {
            string key = string.Join('|', allotment.Centers.Select(
                (center, position) => $"{center}:{string.Join(',', allotment.MemberSets[position])}"));
            if (!distinct.ContainsKey(key)) distinct[key] = Intrinsic(allotment);
        }

        var ranked = distinct.Values.ToList();
        ranked.Sort((first, second) =>
        {
            if (Math.Abs(first.Score - second.Score) > Tolerance) return second.Score.CompareTo(first.Score);
            for (int index = 0; index < first.Centers.Length; index++)
            {
                int order = first.Centers[index].CompareTo(second.Centers[index]);
                if (order != 0) return order;
            }
            return 0;
        });
        for (int index = 1; index < ranked.Count; index++)
            if (Math.Abs(ranked[index].Score - ranked[index - 1].Score) <= Tolerance)
                ranked[index] = ranked[index] with { Score = ranked[index - 1].Score };
        return ranked;
    }

    /// <summary>The allotment with its own thresholds and the quality Q in place of the pair and the criterion of a scan.</summary>
    private static ClusterResult Intrinsic(ClusterResult allotment)
    {
        double leastMembership = 1.0, largestNonMembership = 0.0, quality = 0.0;
        for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
        {
            var members = allotment.MemberSets[cluster];
            double sum = 0.0;
            foreach (int member in members)
            {
                double membership = allotment.Membership[member, cluster];
                double nonMembership = allotment.NonMembership![member, cluster];
                sum += membership - nonMembership;
                leastMembership = Math.Min(leastMembership, membership);
                largestNonMembership = Math.Max(largestNonMembership, nonMembership);
            }
            quality += sum / members.Length;
        }
        return allotment with
        {
            Alpha = leastMembership,
            Beta = largestNonMembership,
            Score = quality,
            CriterionName = "Q_IFS"
        };
    }

    private static string Signature(List<IntuitionisticSearch.Cluster> clusters)
    {
        var text = new StringBuilder();
        foreach (var cluster in clusters)
        {
            text.Append(cluster.Row).Append(':');
            foreach (ulong word in cluster.Bits) text.Append(word.ToString("x")).Append('.');
            text.Append(';');
        }
        return text.ToString();
    }
}
