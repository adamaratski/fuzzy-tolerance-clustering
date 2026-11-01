using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Combinations;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

public class CombinationEnumeratorTests
{
    [Theory]
    [InlineData(4, 2, 6)]
    [InlineData(5, 3, 10)]
    [InlineData(6, 1, 6)]
    [InlineData(3, 3, 1)]
    public void Count_MatchesMathematical(int n, int k, long expected)
    {
        Assert.Equal(expected, CombinationEnumerator.Count(n, k));
    }

    [Fact]
    public void Enumerate_ProducesCorrectCount()
    {
        var combinations = CombinationEnumerator.Enumerate(5, 2).ToList();
        Assert.Equal(10, combinations.Count);
    }

    [Fact]
    public void Enumerate_AllCombinationsAreUnique()
    {
        var combinations = CombinationEnumerator.Enumerate(5, 3).ToList();
        var distinct = combinations.Select(c => string.Join(",", c)).Distinct().ToList();
        Assert.Equal(combinations.Count, distinct.Count);
    }

    [Fact]
    public void Enumerate_AllIndicesAscending()
    {
        foreach (var combo in CombinationEnumerator.Enumerate(6, 3))
        {
            for (int i = 1; i < combo.Length; i++)
                Assert.True(combo[i] > combo[i - 1]);
        }
    }
}

public class DAfcAlgorithmTests
{
    /// <summary>
    /// Hand-crafted 5×5 fuzzy tolerance matrix with obvious 2-cluster structure:
    /// Objects {0,1,2} are similar to each other (high μT),
    /// Objects {3,4} are similar to each other,
    /// Cross-group similarity is low.
    /// Expected: D-AFC(c=2) finds centers from {0,1,2} and {3,4}.
    /// </summary>
    private static FuzzyToleranceMatrix BuildTwoClusterMatrix()
    {
        var m = new FuzzyToleranceMatrix(5);
        // Group A: objects 0,1,2
        m[0, 1] = 0.9;
        m[0, 2] = 0.85;
        m[1, 2] = 0.88;
        // Group B: objects 3,4
        m[3, 4] = 0.92;
        // Cross-group (low similarity)
        m[0, 3] = 0.2; m[0, 4] = 0.18;
        m[1, 3] = 0.22; m[1, 4] = 0.19;
        m[2, 3] = 0.21; m[2, 4] = 0.17;
        return m;
    }

    [Fact]
    public void Run_FindsSolutions_ForObviousTwoClusterData()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void Run_BestResult_HasTwoClusters()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);
        var best = results[0];

        Assert.Equal(2, best.ClusterCount);
    }

    [Fact]
    public void Run_BestResult_CentersAreFromDifferentGroups()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);
        var best = results[0];

        // One center from {0,1,2}, one from {3,4}
        var groupA = new HashSet<int> { 0, 1, 2 };
        var groupB = new HashSet<int> { 3, 4 };

        bool hasCenterInA = best.Centers.Any(c => groupA.Contains(c));
        bool hasCenterInB = best.Centers.Any(c => groupB.Contains(c));

        Assert.True(hasCenterInA && hasCenterInB,
            $"Centers {string.Join(",", best.Centers)} should span both groups");
    }

    [Fact]
    public void Run_AllResultsHaveFullCoverage()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);

        foreach (var result in results)
        {
            var covered = new bool[matrix.Size];
            foreach (var set in result.MemberSets)
            foreach (var idx in set)
                covered[idx] = true;

            Assert.True(covered.All(c => c),
                $"Result c={result.ClusterCount} alpha={result.Alpha:F2} has uncovered objects");
        }
    }

    [Fact]
    public void Run_MembershipValues_AreInZeroOne()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);
        var best = results[0];

        for (int i = 0; i < matrix.Size; i++)
        for (int l = 0; l < best.ClusterCount; l++)
        {
            var mu = best.Membership[i, l];
            Assert.True(mu >= 0.0 && mu <= 1.0,
                $"Membership[{i},{l}] = {mu} is out of [0,1]");
        }
    }

    [Fact]
    public void Run_ResultsOrderedByScoreDescending()
    {
        var matrix = BuildTwoClusterMatrix();
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);

        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Score >= results[i].Score);
    }

    [Fact]
    public void Run_WithMinPowerConstraint_FiltersSmallClusters()
    {
        var matrix = BuildTwoClusterMatrix();
        // Require at least 3 objects per cluster — group B has only 2, so many solutions are invalid
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2, MinClusterPower: 3),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(matrix, TestContext.Current.CancellationToken);

        foreach (var result in results)
        foreach (var set in result.MemberSets)
            Assert.True(set.Length >= 3);
    }

    [Fact]
    public void Run_CancellationToken_Cancels()
    {
        var matrix = BuildTwoClusterMatrix();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 2),
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        Assert.Throws<OperationCanceledException>(() => dafc.Run(matrix, cts.Token));
    }
}

public class DPafcAlgorithmTests
{
    [Fact]
    public void Run_FindsSolutions_WithAutomaticClusterCount()
    {
        var m = new FuzzyToleranceMatrix(6);
        // Three clear groups: {0,1}, {2,3}, {4,5}
        m[0, 1] = 0.95; m[2, 3] = 0.93; m[4, 5] = 0.91;
        m[0, 2] = 0.1; m[0, 3] = 0.1; m[0, 4] = 0.1; m[0, 5] = 0.1;
        m[1, 2] = 0.1; m[1, 3] = 0.1; m[1, 4] = 0.1; m[1, 5] = 0.1;
        m[2, 4] = 0.1; m[2, 5] = 0.1;
        m[3, 4] = 0.1; m[3, 5] = 0.1;

        var dpafc = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
        var results = dpafc.Run(m, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        // Should find solutions with c=3 being the dominant structure
        Assert.Contains(results, r => r.ClusterCount == 3);
    }
}
