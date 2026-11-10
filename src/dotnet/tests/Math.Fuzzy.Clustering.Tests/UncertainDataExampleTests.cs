using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Asymmetric;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Normalization;
using MathFuzzy.Core.ThreeWay;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// The worked examples of chapter 5 of the book that run the standard algorithms on a fuzzy
/// tolerance built from three-way data (Sato–Sato) and from asymmetric data (children relationship
/// data of Sato-Ilic and Jain).
/// </summary>
public class UncertainDataExampleTests
{
    private static string DatasetPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }

    private static ClusterResult DAfc(MathFuzzy.Core.Matrix.FuzzyToleranceMatrix tolerance, int clusterCount)
    {
        var results = new DAfcAlgorithm(new DAfcOptions(ClusterCount: clusterCount),
                AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(tolerance, TestContext.Current.CancellationToken);
        Assert.NotEmpty(results);
        return results[0];
    }

    private static int[][] Carriers(ClusterResult result)
        => [.. result.MemberSets.Select(set => set.Select(index => index + 1).Order().ToArray()).OrderBy(set => set[0])];

    // ── Sato–Sato three-way data ──────────────────────────────────────────────────────────

    /// <summary>
    /// Book, sec. "Clustering Three-Way Data": normalisation (3.52), squared Euclidean generalisation
    /// (3.55), D-AFC(3) → three fully separated clusters at α* = 0.93120 (Viattchenin 2013, sec. 3.3.3)
    /// of 3, 7 and 28 boys (Kacprzyk, Owsiński and Viattchenin 2014, p. 55).
    /// </summary>
    [Fact]
    public void SatoSato_DAfc3_GivesThreeFullySeparatedClustersAtPublishedThreshold()
    {
        var data = ThreeWayDataMatrix.FromCsv(DatasetPath("sato_sato_threeway.csv"));
        var tolerance = data.BuildTolerance(MinMaxNormalization.Instance, ThreeWayDistance.SquaredEuclidean);

        var best = DAfc(tolerance, 3);

        Assert.Equal(0.93120, best.Alpha, precision: 5);
        Assert.Equal(0, best.IntersectionCount);
        Assert.Equal(new[] { 3, 7, 28 }, best.MemberSets.Select(set => set.Length).Order());
        var carriers = Carriers(best);
        Assert.Contains(new[] { 7, 14, 38 }, carriers);
        Assert.Contains(new[] { 9, 15, 20, 30, 32, 33, 37 }, carriers);
    }

    /// <summary>
    /// The published memberships of the typical points, x₁₄: 0.98298, x₂₀: 0.97888, x₅: 0.98392
    /// (Viattchenin 2013, p. 164; Kacprzyk, Owsiński and Viattchenin 2014, p. 55), are reproduced on
    /// the relation whose diagonal is 1 − ε_G(x_i, x_i): the generalised distance of an object to
    /// itself is not zero, and the published typical point is the object of largest membership.
    /// In the third cluster that object (x₅) is not the object that generates the cluster (x₃₁), whose
    /// own membership is 1 − ε_G(x₃₁, x₃₁) = 0.98217. The second value is printed truncated (0.978886).
    /// </summary>
    [Fact]
    public void SatoSato_TypicalPointMemberships_AreThoseOfTheRelationWithSelfSimilarity()
    {
        var data = ThreeWayDataMatrix.FromCsv(DatasetPath("sato_sato_threeway.csv"));
        var relation = data.BuildTolerance(MinMaxNormalization.Instance, ThreeWayDistance.SquaredEuclidean,
            keepSelfSimilarity: true);

        var best = DAfc(relation, 3);
        Assert.Equal(0.93120, best.Alpha, precision: 5);

        var expected = new Dictionary<int, double> { [14] = 0.98298, [20] = 0.97888, [5] = 0.98392 };
        int[] typicalPoints = new int[3];
        for (int cluster = 0; cluster < 3; cluster++)
        {
            int typical = best.MemberSets[cluster].MaxBy(member => best.Membership[member, cluster]);
            typicalPoints[cluster] = typical + 1;
            Assert.True(expected.ContainsKey(typical + 1), $"unexpected typical point x{typical + 1}");
            Assert.True(System.Math.Abs(expected[typical + 1] - best.Membership[typical, cluster]) < 1e-5,
                $"x{typical + 1}: {best.Membership[typical, cluster]}");
        }
        Assert.Equal(new[] { 5, 14, 20 }, typicalPoints.Order());
    }

    /// <summary>
    /// D-AFC(c) for c = 2, 3 with the measure of separation and compactness: c* = 3 at its first
    /// minimum (Viattchenin 2013, sec. 3.3.3, Fig. 3.16).
    /// </summary>
    [Fact]
    public void SatoSato_SeparationCompactness_IsSmallerForThreeClustersThanForTwo()
    {
        var data = ThreeWayDataMatrix.FromCsv(DatasetPath("sato_sato_threeway.csv"));
        var tolerance = data.BuildTolerance(MinMaxNormalization.Instance, ThreeWayDistance.SquaredEuclidean);

        double twoClusters = AllotmentMeasures.Compute(DAfc(tolerance, 2)).SeparationCompactness;
        double threeClusters = AllotmentMeasures.Compute(DAfc(tolerance, 3)).SeparationCompactness;

        Assert.True(threeClusters < twoClusters, $"V_MSC(2) = {twoClusters}, V_MSC(3) = {threeClusters}");
    }

    // ── Children relationship data ────────────────────────────────────────────────────────

    /// <summary>
    /// Book, sec. "Asymmetric Data": R = 1 − A/6, Q = min(R_ij, R_ji), D-PAFC → principal allotment
    /// among five fully separated clusters at α* = 0.6666 (= 4/6, printed truncated), typical points
    /// x₅, x₂, x₈, x₁₀, x₁₂, every other member with membership 0.8333 (Viattchenin 2013, sec. 4.3.2).
    /// </summary>
    [Fact]
    public void Children_DPafcOnQuasiEquivalence_GivesFivePublishedClusters()
    {
        var children = AsymmetricProximityMatrix.FromCsv(DatasetPath("children_dislike.csv"));
        var quasiEquivalence = children.BuildTolerance(SymmetrizationMethod.QuasiEquivalence);

        var results = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(quasiEquivalence, TestContext.Current.CancellationToken);
        Assert.NotEmpty(results);
        var best = results[0];

        Assert.Equal(5, best.ClusterCount);
        Assert.Equal(0.6666, best.Alpha, precision: 3);
        Assert.Equal(4.0 / 6.0, best.Alpha, precision: 12);
        Assert.Equal(0, best.IntersectionCount);
        Assert.Equal(
            new[] { new[] { 1, 5, 6, 7 }, [2, 3, 4], [8, 11], [9, 12, 14, 16], [10, 13, 15] },
            Carriers(best));
        // Within {x8, x11}, {x10, x13, x15} and {x9, x12, x14, x16} every pair has Q = 5/6, so any
        // member can be the typical point: 2 · 3 · 4 = 24 choices give the same clusters and the same
        // value of the criterion (up to rounding noise), and the published typical points are one of them.
        var tied = results
            .Where(result => System.Math.Abs(result.Score - best.Score) <= 1e-12 && result.HasSameClusters(best))
            .Select(result => string.Join(",", result.Centers.Select(center => center + 1).Order()))
            .ToHashSet();
        Assert.Equal(24, tied.Count);
        Assert.Contains("2,5,8,10,12", tied);
        Assert.All(tied, centers => Assert.StartsWith("2,5,", centers));   // x2 and x5 are unique

        for (int cluster = 0; cluster < 5; cluster++)
            foreach (int member in best.MemberSets[cluster])
                Assert.Equal(member == best.Centers[cluster] ? 1.0 : 5.0 / 6.0, best.Membership[member, cluster], precision: 12);
    }

    /// <summary>
    /// With the likeness relation L the monograph reports the same allotment at α* = 0.8333; the
    /// recomputation printed in the book does not confirm this and gives four fully separated
    /// clusters, because L_ij = 1 for every pair of children who dislike each other maximally. The
    /// test follows the recomputed result of the book.
    /// </summary>
    [Fact]
    public void Children_DPafcOnLikeness_GivesTheFourClustersOfTheRecomputation()
    {
        var children = AsymmetricProximityMatrix.FromCsv(DatasetPath("children_dislike.csv"));
        var likeness = children.BuildTolerance(SymmetrizationMethod.Likeness);

        var results = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(likeness, TestContext.Current.CancellationToken);
        Assert.NotEmpty(results);
        var best = results[0];

        Assert.Equal(0, best.IntersectionCount);
        Assert.Equal(
            new[] { new[] { 1, 5, 6, 7, 10, 13, 15 }, [2, 3, 4], [8, 11], [9, 12, 14, 16] },
            Carriers(best));
    }
}
