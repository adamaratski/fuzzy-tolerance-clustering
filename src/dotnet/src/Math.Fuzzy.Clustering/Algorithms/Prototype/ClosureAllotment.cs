using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// The criterion-maximising allotment of one α-level of a transitive closure T̃
/// (Viattchenin 2013, sec. 2.3.2, steps 6–8).
///
/// On T̃ the α-cut of every object is its equivalence class, so every allotment in
/// B(α) has the same supports — the classes — and differs only in the typical point
/// chosen inside each class. For a decomposable criterion (F₁, F₂) the arg max is
/// therefore the best typical point of each class taken independently: O(Σ|class|²)
/// work instead of the ∏|class| allotments a D-AFC combination search enumerates.
///
/// The result is assembled as D-AFC assembles it: centers ascending, cluster
/// contributions summed in that order. Ties need care: T̃ is ultrametric, so several
/// members of a class often have the same contribution in exact arithmetic, while the
/// computed values differ by a few ulps depending on summation order. Contributions
/// within <see cref="TieTolerance"/> are therefore treated as equal and the smallest
/// object index wins (the D-AFC ordering rule). The exhaustive search instead let
/// those last-bit differences pick the center, so on such levels its score can be
/// ~10⁻¹⁵ higher with another, equally good typical point.
/// </summary>
internal static class ClosureAllotment
{
    /// <summary>
    /// Contributions are means of memberships in [0, 1]; rounding error stays far below
    /// this bound, while genuinely different contributions differ far above it.
    /// </summary>
    internal const double TieTolerance = 1e-12;

    /// <summary>
    /// Best allotment among the given α-classes, or null when some class is smaller
    /// than <paramref name="minClusterPower"/> (no allotment among exactly c clusters).
    /// </summary>
    internal static ClusterResult? FindBest(
        FuzzyToleranceMatrix   closureMatrix,
        double                 alpha,
        IReadOnlyList<int[]>   classes,
        IDecomposableCriterion criterion,
        int                    minClusterPower,
        CancellationToken      ct)
    {
        if (classes.Any(members => members.Length < minClusterPower)) return null;

        var clusters = new List<(int Center, int[] Members, double Contribution)>(classes.Count);
        foreach (var members in classes)
        {
            ct.ThrowIfCancellationRequested();
            var (center, contribution) = BestTypicalPoint(closureMatrix, alpha, members, criterion);
            clusters.Add((center, members, contribution));
        }

        clusters.Sort((first, second) => first.Center.CompareTo(second.Center));

        double contributionSum = 0.0;
        foreach (var cluster in clusters)
            contributionSum += cluster.Contribution;

        var centers    = clusters.Select(cluster => cluster.Center).ToArray();
        var memberSets = clusters.Select(cluster => cluster.Members).ToArray();
        var membership = MembershipMatrix.Build(closureMatrix, centers, memberSets, closureMatrix.Size);
        double score   = criterion.Aggregate(contributionSum, clusters.Count, alpha);

        return new ClusterResult(clusters.Count, alpha, centers, memberSets, membership, score, criterion.Name);
    }

    /// <summary>
    /// The class member with the largest cluster contribution; among members tied with
    /// the maximum (up to <see cref="TieTolerance"/>) the smallest object index.
    /// </summary>
    private static (int Center, double Contribution) BestTypicalPoint(
        FuzzyToleranceMatrix closureMatrix, double alpha, int[] members, IDecomposableCriterion criterion)
    {
        var contributions = members
            .Select(candidate => criterion.EvaluateCluster(closureMatrix, candidate, members, alpha))
            .ToArray();
        double maximum = contributions.Max();

        // Members are ascending, so the first one tied with the maximum has the smallest index.
        int position = Array.FindIndex(contributions, contribution => contribution >= maximum - TieTolerance);
        return (members[position], contributions[position]);
    }
}
