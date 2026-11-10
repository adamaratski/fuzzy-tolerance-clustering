using System.Numerics;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Intuitionistic;

namespace MathFuzzy.Clustering.Algorithms.Intuitionistic;

/// <summary>Order in which the pairs of thresholds (α, β) are scanned.</summary>
public enum ThresholdScanOrder
{
    /// <summary>
    /// α in the outer loop (ascending), β in the inner loop (descending): the order of the printed
    /// algorithms; it reproduces the published results of D-AIFC(c) and D-PAIFC-TC.
    /// </summary>
    BetaInner,

    /// <summary>
    /// β in the outer loop (descending), α in the inner loop (ascending): the order that
    /// reproduces the published D-PAIFC result on Hung's ten-object relation.
    /// </summary>
    BetaOuter
}

/// <summary>Settings shared by the intuitionistic algorithms.</summary>
/// <param name="ScanOrder">Order of the scan over the pairs of thresholds.</param>
/// <param name="MaxSearchNodes">Node budget of the search for allotments at one pair of thresholds.</param>
/// <param name="MaxResults">Largest number of allotments returned, best first.</param>
/// <param name="Diagnostics">Optional collector of search statistics (node count, budget exhaustion).</param>
public sealed record IntuitionisticOptions(
    ThresholdScanOrder ScanOrder = ThresholdScanOrder.BetaInner,
    long MaxSearchNodes = 50_000_000L,
    int MaxResults = 200,
    SearchDiagnostics? Diagnostics = null);

/// <summary>
/// D-PAIFC — the direct algorithm for detecting the principal allotment among intuitionistic
/// fuzzy clusters (Viattchenin 2010; 2013, sec. 3.1).
///
/// An intuitionistic fuzzy (α, β)-cluster of an object is the set of objects whose membership
/// in the row of that object is at least α and whose non-membership is at most β. The pairs
/// (α, β) with α + β ≤ 1 are scanned from the weakest to the strongest; pairs at which some
/// cluster is still the whole set are skipped. At the first pair that admits an allotment among
/// fully separated clusters, the least number of clusters is taken, and the allotments of that
/// pair are returned ordered by the criterion
///   F = Σ_l mean μ_l − α·c − (Σ_l mean ν_l − β·c).
/// Several allotments with the largest criterion value mean that the result is not unique.
/// </summary>
public sealed class DPaifcAlgorithm(IntuitionisticOptions? options = null)
{
    private readonly IntuitionisticOptions _options = options ?? new IntuitionisticOptions();

    public IReadOnlyList<ClusterResult> Run(IntuitionisticToleranceMatrix relation, CancellationToken ct = default)
    {
        int objectCount = relation.Size;
        foreach (var (alpha, beta) in IntuitionisticSearch.ThresholdPairs(relation, _options.ScanOrder))
        {
            ct.ThrowIfCancellationRequested();
            var clusters = IntuitionisticSearch.InitialAllotment(relation, alpha, beta);
            if (clusters is null) continue;   // some cluster is the whole set

            for (int clusterCount = 2; clusterCount < objectCount; clusterCount++)
            {
                var found = IntuitionisticSearch.Allotments(relation, clusters, clusterCount, intersectionBound: 0,
                                                            alpha, beta, _options, ct, out bool budgetExhausted);
                if (found.Count > 0 || budgetExhausted) return found;
            }
        }
        return [];
    }
}

/// <summary>
/// D-AIFC(c) — the direct algorithm for detecting the allotment among c intuitionistic fuzzy
/// clusters (Kacprzyk, Owsiński, Viattchenin and Shyrai 2016).
///
/// The number of clusters is given. For w = 0, 1, 2, … (the largest admissible number of
/// objects shared by two clusters) the pairs of thresholds are scanned as in D-PAIFC, and the
/// allotments among exactly c clusters of the first pair that has any are returned ordered by
/// the criterion; w is increased only when no pair admits an allotment.
/// </summary>
public sealed class DAifcAlgorithm(int clusterCount, IntuitionisticOptions? options = null)
{
    private readonly IntuitionisticOptions _options = options ?? new IntuitionisticOptions();

    public IReadOnlyList<ClusterResult> Run(IntuitionisticToleranceMatrix relation, CancellationToken ct = default)
    {
        int objectCount = relation.Size;
        if (clusterCount < 2 || clusterCount >= objectCount)
            throw new ArgumentException($"The number of clusters must satisfy 2 ≤ c < n = {objectCount}; got {clusterCount}.");

        for (int intersectionBound = 0; intersectionBound <= objectCount; intersectionBound++)
        {
            foreach (var (alpha, beta) in IntuitionisticSearch.ThresholdPairs(relation, _options.ScanOrder))
            {
                ct.ThrowIfCancellationRequested();
                var clusters = IntuitionisticSearch.InitialAllotment(relation, alpha, beta);
                if (clusters is null) continue;

                var found = IntuitionisticSearch.Allotments(relation, clusters, clusterCount, intersectionBound,
                                                            alpha, beta, _options, ct, out bool budgetExhausted);
                if (found.Count > 0 || budgetExhausted) return found;
            }
        }
        return [];
    }
}

/// <summary>
/// D-PAIFC-TC — the prototype-based algorithm for intuitionistic fuzzy data
/// (Viattchenin and Shyrai 2015): the intuitionistic fuzzy tolerance is built from the
/// attribute data by a similarity measure, replaced by its transitive closure, and D-PAIFC is
/// applied to the closure. Allotments with the same criterion value are ordered by the
/// similarity of their typical points to the prototypes (the mean attribute vectors) of the
/// clusters; the sources do not say how this comparison is made, and the membership part of
/// the similarity is used here.
/// </summary>
public sealed class DPaifcTcAlgorithm(
    IntuitionisticMeasure measure = IntuitionisticMeasure.Hamming, IntuitionisticOptions? options = null)
{
    private readonly IntuitionisticOptions _options = options ?? new IntuitionisticOptions();

    /// <summary>Runs the algorithm on intuitionistic attribute data.</summary>
    public IReadOnlyList<ClusterResult> Run(IntuitionisticDataMatrix data, CancellationToken ct = default)
    {
        var closure = IntuitionisticSimilarity.TransitiveClosure(IntuitionisticSimilarity.BuildTolerance(data, measure));
        var found = new DPaifcAlgorithm(_options).Run(closure, ct);
        return [.. found
            .Select(result => result with { TiebreakerScore = PrototypeDissimilarity(result, data) })
            .Order(ClusterResult.Order)];
    }

    /// <summary>Runs the algorithm on a relation given directly: its closure, then D-PAIFC.</summary>
    public IReadOnlyList<ClusterResult> Run(IntuitionisticToleranceMatrix relation, CancellationToken ct = default) =>
        new DPaifcAlgorithm(_options).Run(IntuitionisticSimilarity.TransitiveClosure(relation), ct);

    /// <summary>
    /// Σ over clusters of 1 − μ_r, where μ_r is the membership part of the similarity between the
    /// typical point and the prototype of its cluster; smaller means typical points nearer to the prototypes.
    /// </summary>
    private double PrototypeDissimilarity(ClusterResult result, IntuitionisticDataMatrix data)
    {
        double total = 0.0;
        for (int cluster = 0; cluster < result.ClusterCount; cluster++)
        {
            var members = result.MemberSets[cluster];
            int center = result.Centers[cluster];
            var (similarity, _) = IntuitionisticSimilarity.Similarity(
                data.Attributes,
                attribute => (data.NonMembership(center, attribute), data.Hesitancy(center, attribute)),
                attribute => (members.Average(member => data.NonMembership(member, attribute)),
                              members.Average(member => data.Hesitancy(member, attribute))),
                measure);
            total += 1.0 - similarity;
        }
        return total;
    }
}

/// <summary>Building blocks of the intuitionistic algorithms: threshold pairs, clusters, allotments.</summary>
internal static class IntuitionisticSearch
{
    private const double Tolerance = IntuitionisticToleranceMatrix.Tolerance;

    /// <summary>An (α, β)-cluster of the initial allotment: its generating row and its carrier.</summary>
    internal sealed record Cluster(int Row, int[] Members, ulong[] Bits);

    /// <summary>The admissible pairs (α + β ≤ 1) in the order of the scan.</summary>
    internal static IEnumerable<(double Alpha, double Beta)> ThresholdPairs(
        IntuitionisticToleranceMatrix relation, ThresholdScanOrder order)
    {
        var alphas = relation.AlphaSequence();
        var betas = relation.BetaSequence();
        if (order == ThresholdScanOrder.BetaInner)
        {
            foreach (double alpha in alphas)
                foreach (double beta in betas)
                    if (alpha + beta <= 1.0 + Tolerance) yield return (alpha, beta);
        }
        else
        {
            foreach (double beta in betas)
                foreach (double alpha in alphas)
                    if (alpha + beta <= 1.0 + Tolerance) yield return (alpha, beta);
        }
    }

    /// <summary>
    /// The clusters generated by all rows at the pair (α, β); rows that generate the same carrier
    /// with the same degrees are one cluster (the row with the smaller number is kept).
    /// Returns null when some cluster is the whole set.
    /// </summary>
    internal static List<Cluster>? InitialAllotment(IntuitionisticToleranceMatrix relation, double alpha, double beta)
    {
        int objectCount = relation.Size;
        int words = (objectCount + 63) >> 6;
        var clusters = new List<Cluster>(objectCount);

        for (int row = 0; row < objectCount; row++)
        {
            var members = new List<int>();
            var bits = new ulong[words];
            for (int column = 0; column < objectCount; column++)
            {
                if (relation.Membership(row, column) < alpha || relation.NonMembership(row, column) > beta) continue;
                members.Add(column);
                bits[column >> 6] |= 1UL << (column & 63);
            }
            if (members.Count == objectCount) return null;

            bool duplicate = clusters.Any(existing =>
                existing.Bits.AsSpan().SequenceEqual(bits)
                && members.All(member => relation.Membership(existing.Row, member) == relation.Membership(row, member)
                                         && relation.NonMembership(existing.Row, member) == relation.NonMembership(row, member)));
            if (!duplicate) clusters.Add(new Cluster(row, [.. members], bits));
        }
        return clusters;
    }

    /// <summary>
    /// All allotments among exactly <paramref name="clusterCount"/> of the given clusters whose
    /// carriers cover the set and pairwise share at most <paramref name="intersectionBound"/>
    /// objects, ordered by the criterion (best first).
    /// </summary>
    internal static IReadOnlyList<ClusterResult> Allotments(
        IntuitionisticToleranceMatrix relation, List<Cluster> clusters, int clusterCount, int intersectionBound,
        double alpha, double beta, IntuitionisticOptions options, CancellationToken ct, out bool budgetExhausted)
    {
        budgetExhausted = false;
        int objectCount = relation.Size;
        int words = (objectCount + 63) >> 6;
        int available = clusters.Count;
        var results = new List<ClusterResult>();
        if (available < clusterCount)
        {
            options.Diagnostics?.Record(0, false, 1, 0);
            return results;
        }

        // suffixUnion[i] = union of the carriers of clusters i..end: what the rest can still cover
        var suffixUnion = new ulong[available + 1][];
        suffixUnion[available] = new ulong[words];
        for (int index = available - 1; index >= 0; index--)
        {
            suffixUnion[index] = (ulong[])suffixUnion[index + 1].Clone();
            for (int word = 0; word < words; word++) suffixUnion[index][word] |= clusters[index].Bits[word];
        }
        var everything = new ulong[words];
        for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            everything[objectIndex >> 6] |= 1UL << (objectIndex & 63);

        var chosen = new int[clusterCount];
        var covered = new ulong[clusterCount + 1][];
        for (int depth = 0; depth <= clusterCount; depth++) covered[depth] = new ulong[words];
        long nodes = 0;
        bool exhausted = false;

        void Search(int depth, int first)
        {
            if (exhausted) return;
            if (depth == clusterCount)
            {
                if (covered[depth].AsSpan().SequenceEqual(everything))
                    results.Add(Assemble(relation, clusters, chosen, alpha, beta, intersectionBound));
                return;
            }
            if (++nodes > options.MaxSearchNodes) { exhausted = true; return; }
            ct.ThrowIfCancellationRequested();

            for (int index = first; index <= available - (clusterCount - depth); index++)
            {
                // what is covered so far together with everything the remaining clusters offer must be the whole set
                bool coverable = true;
                for (int word = 0; word < words && coverable; word++)
                    coverable = (covered[depth][word] | suffixUnion[index][word]) == everything[word];
                if (!coverable) return;   // later indices offer even less

                var bits = clusters[index].Bits;
                bool separated = true;
                for (int earlier = 0; earlier < depth && separated; earlier++)
                {
                    int shared = 0;
                    var other = clusters[chosen[earlier]].Bits;
                    for (int word = 0; word < words; word++) shared += BitOperations.PopCount(bits[word] & other[word]);
                    separated = shared <= intersectionBound;
                }
                if (!separated) continue;

                chosen[depth] = index;
                for (int word = 0; word < words; word++) covered[depth + 1][word] = covered[depth][word] | bits[word];
                Search(depth + 1, index + 1);
            }
        }

        Search(0, 0);
        budgetExhausted = exhausted;
        options.Diagnostics?.Record(nodes, exhausted, 1, 1);

        // Criterion values that differ by rounding noise are one value; then the typical points decide.
        results.Sort((first, second) =>
        {
            if (Math.Abs(first.Score - second.Score) > Tolerance) return second.Score.CompareTo(first.Score);
            for (int index = 0; index < first.Centers.Length; index++)
            {
                int order = first.Centers[index].CompareTo(second.Centers[index]);
                if (order != 0) return order;
            }
            return 0;
        });
        if (results.Count > 1)
        {
            // equal values get exactly the same number, so that ties are visible to the caller
            for (int index = 1; index < results.Count; index++)
                if (Math.Abs(results[index].Score - results[index - 1].Score) <= Tolerance)
                    results[index] = results[index] with { Score = results[index - 1].Score };
        }
        return results.Count > options.MaxResults ? results.GetRange(0, options.MaxResults) : results;
    }

    private static ClusterResult Assemble(
        IntuitionisticToleranceMatrix relation, List<Cluster> clusters, int[] chosen,
        double alpha, double beta, int intersectionBound)
    {
        int objectCount = relation.Size;
        int clusterCount = chosen.Length;
        var centers = new int[clusterCount];
        var memberSets = new int[clusterCount][];
        var membership = new double[objectCount, clusterCount];
        var nonMembership = new double[objectCount, clusterCount];
        double membershipTerm = 0.0, nonMembershipTerm = 0.0;

        for (int position = 0; position < clusterCount; position++)
        {
            var cluster = clusters[chosen[position]];
            centers[position] = cluster.Row;
            memberSets[position] = cluster.Members;
            double membershipSum = 0.0, nonMembershipSum = 0.0;
            foreach (int member in cluster.Members)
            {
                membership[member, position] = relation.Membership(cluster.Row, member);
                nonMembership[member, position] = relation.NonMembership(cluster.Row, member);
                membershipSum += membership[member, position];
                nonMembershipSum += nonMembership[member, position];
            }
            membershipTerm += membershipSum / cluster.Members.Length;
            nonMembershipTerm += nonMembershipSum / cluster.Members.Length;
        }

        double criterion = membershipTerm - alpha * clusterCount - (nonMembershipTerm - beta * clusterCount);
        return new ClusterResult(clusterCount, alpha, centers, memberSets, membership, criterion, "F_IFS",
                                 Beta: beta, NonMembership: nonMembership, IntersectionBound: intersectionBound);
    }
}
