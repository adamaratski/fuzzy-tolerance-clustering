using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// Regression tests tied to the worked examples of the book (five-object tolerance
/// matrix, chapters 1–3) and to the original D-AFC(c) semantics (w := 0 with
/// automatic increment, deterministic ranking).
/// </summary>
public class BookExampleTests
{
    /// <summary>Matrix T of eq. (example-T), chapter 1.</summary>
    private static FuzzyToleranceMatrix BookMatrix()
    {
        var t = new FuzzyToleranceMatrix(5);
        t[0, 1] = 0.8; t[0, 2] = 0.0; t[0, 3] = 0.1; t[0, 4] = 0.2;
        t[1, 2] = 0.4; t[1, 3] = 0.0; t[1, 4] = 0.9;
        t[2, 3] = 0.0; t[2, 4] = 0.0;
        t[3, 4] = 0.5;
        return t;
    }

    [Fact]
    public void DAfc3_OnBookMatrix_ReturnsFullySeparatedAllotmentAtAlpha05()
    {
        var dafc = new DAfcAlgorithm(
            new DAfcOptions(ClusterCount: 3),          // w := 0 with automatic increment
            AllDistinctAlphaSelector.Instance,
            F1Criterion.Instance);

        var results = dafc.Run(BookMatrix(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        var best = results[0];
        Assert.Equal(0.5, best.Alpha, precision: 10);
        Assert.Equal(new[] { 0, 2, 3 }, best.Centers);            // x1, x3, x4
        Assert.Equal(1.15, best.Score, precision: 10);            // F1 = 0.9 + 1.0 + 0.75 − 1.5
        Assert.Equal(0, best.IntersectionCount);                  // fully separated

        // The class of solutions B(3) contains exactly the allotments at α=0.5 and α=0.8.
        Assert.Equal(2, results.Count);
        Assert.Equal(0.8, results[1].Alpha, precision: 10);
        Assert.Equal(new[] { 1, 2, 3 }, results[1].Centers);      // x2, x3, x4
        Assert.Equal(0.5, results[1].Score, precision: 10);
    }

    [Fact]
    public void DAfc_AutoIntersections_RelaxesOnlyWhenNoPartitionExists()
    {
        // Three objects pairwise tolerant at 0.9: no fully separated 2-allotment exists
        // at any level, so w is incremented until an allotment with one shared object appears.
        var t = new FuzzyToleranceMatrix(3);
        t[0, 1] = 0.9; t[1, 2] = 0.9; t[0, 2] = 0.3;

        var dafc = new DAfcAlgorithm(new DAfcOptions(ClusterCount: 2), AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
        var results = dafc.Run(t, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        // Fully separated solution exists at α = 0.9 with centers x1 and x3? No: A(x1)={x1,x2}, A(x3)={x2,x3}
        // overlap in x2; at α=0.3 everything is one cluster. Hence w = 1 is required.
        Assert.Equal(1, results[0].IntersectionCount);
    }

    [Fact]
    public void DAfc_ExplicitZeroIntersections_ReturnsEmptyWhenNoPartition()
    {
        var t = new FuzzyToleranceMatrix(3);
        t[0, 1] = 0.9; t[1, 2] = 0.9; t[0, 2] = 0.3;

        var dafc = new DAfcAlgorithm(new DAfcOptions(ClusterCount: 2, MaxIntersections: 0), AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
        Assert.Empty(dafc.Run(t, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MaxMinClosure_OfBookMatrix_MatchesChapter3()
    {
        var closure = new MaxMinTransitiveClosure().Compute(BookMatrix());
        double[,] expected =
        {
            { 1.0, 0.8, 0.4, 0.5, 0.8 },
            { 0.8, 1.0, 0.4, 0.5, 0.9 },
            { 0.4, 0.4, 1.0, 0.4, 0.4 },
            { 0.5, 0.5, 0.4, 1.0, 0.5 },
            { 0.8, 0.9, 0.4, 0.5, 1.0 },
        };
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                Assert.Equal(expected[i, j], closure[i, j], precision: 10);

        // leaps 0.1, 0.3, 0.1 → maximal 0.3 → α* = 0.8, at which the closure has three classes
        Assert.Equal(0.8, LeapHeuristicAlphaSelector.SelectFrom(closure.GetDistinctValues())!.Value, precision: 10);
    }

    [Fact]
    public void Ranking_IsDeterministic_AcrossRuns()
    {
        // Random tolerance matrix with many ties → the ordering must not depend on thread timing.
        var rng = new Random(12345);
        int n = 40;
        var t = new FuzzyToleranceMatrix(n);
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                t[i, j] = System.Math.Round(rng.NextDouble(), 1); // only 10 distinct values → many ties

        var dafc = new DAfcAlgorithm(new DAfcOptions(ClusterCount: 3, MaxResults: 20), AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
        var first  = dafc.Run(t, TestContext.Current.CancellationToken);
        var second = dafc.Run(t, TestContext.Current.CancellationToken);

        Assert.Equal(first.Count, second.Count);
        for (int k = 0; k < first.Count; k++)
        {
            Assert.Equal(first[k].Alpha, second[k].Alpha);
            Assert.Equal(first[k].Score, second[k].Score);
            Assert.Equal(first[k].Centers, second[k].Centers);
        }
        for (int k = 1; k < first.Count; k++)
            Assert.True(ClusterResult.Order.Compare(first[k - 1], first[k]) <= 0);
    }

    /// <summary>Sneath–Sokal data (16 × 2), chapter 2 / Viattchenin 2013, sec. 2.2.6.</summary>
    internal static RawDataMatrix SneathSokal()
    {
        int[,] xy = { {0,4},{0,3},{1,5},{2,4},{3,3},{2,2},{2,1},{1,0},
                      {5,5},{6,5},{7,6},{5,3},{7,3},{6,2},{6,1},{8,1} };
        var m = new RawDataMatrix(16, 2);
        for (int i = 0; i < 16; i++) { m[i, 0] = xy[i, 0]; m[i, 1] = xy[i, 1]; }
        return m;
    }

    [Fact]
    public void DPafc_OnSneathSokal_ReturnsPrincipalAllotmentAmongFourClusters()
    {
        var options = new AlgorithmOptions(Distance: "squared-euclidean", Normalization: "max", Accuracy: 1e-5);

        var results = AlgorithmFactory.Run("dpafc", SneathSokal(), options, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        var best = results[0];
        Assert.Equal(4, best.ClusterCount);
        Assert.Equal(0.91319, best.Alpha, precision: 4);       // α* of the monograph
        Assert.Equal(16, best.MemberSets.Sum(s => s.Length)); // fully separated: a partition of X
        Assert.Equal(0, best.IntersectionCount);              // fully separated
    }

    [Fact]
    public void DPafc_AdmittingIntersections_NeverNeedsMoreClustersThanFullySeparated()
    {
        var strict  = AlgorithmFactory.Run("dpafc", SneathSokal(), new AlgorithmOptions(), TestContext.Current.CancellationToken);
        var relaxed = AlgorithmFactory.Run("dpafc", SneathSokal(), new AlgorithmOptions(MaxIntersections: 3), TestContext.Current.CancellationToken);

        Assert.NotEmpty(strict);
        Assert.NotEmpty(relaxed);
        Assert.All(relaxed, r => Assert.True(r.IntersectionCount <= 3));
        Assert.True(relaxed[0].Alpha <= strict[0].Alpha);   // the scan is ascending, so w > 0 can only stop earlier
    }
}
