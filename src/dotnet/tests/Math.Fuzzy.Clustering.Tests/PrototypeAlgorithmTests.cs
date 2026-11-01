using MathFuzzy.Clustering.Algorithms.DimensionReduction;
using MathFuzzy.Clustering.Algorithms.Hierarchical;
using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Tests.Prototype;

/// <summary>
/// Shared test fixture: 8 objects with clear 2-group structure in 2D space.
/// Objects {0,1,2,3} cluster near (0,0), {4,5,6,7} near (1,1).
/// </summary>
public static class TestData
{
    public static RawDataMatrix TwoGroupData() => RawDataMatrix.FromArray(new double[,]
    {
        { 0.05, 0.05 },
        { 0.10, 0.08 },
        { 0.08, 0.12 },
        { 0.06, 0.09 },
        { 0.92, 0.95 },
        { 0.95, 0.90 },
        { 0.88, 0.93 },
        { 0.91, 0.88 },
    });

    public static ToleranceMatrixBuilder Builder() =>
        new(EuclideanDistance.Instance, MinMaxNormalization.Instance);

    public static MaxMinTransitiveClosure Closure() => new();
}

public class DAfcTcAlgorithmTests
{
    [Fact]
    public void Run_FindsSolutions_ForTwoGroupData()
    {
        var algo = new DAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void Run_BestSolution_HasAllObjectsCovered()
    {
        // D-AFC-TC uses leap heuristic: the selected alpha may yield c > 2 solutions.
        // Key invariant: every solution must cover all n objects.
        var algo = new DAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);
        Assert.NotEmpty(results);

        int n = TestData.TwoGroupData().Rows;
        var best = results[0];
        var covered = new bool[n];
        foreach (var set in best.MemberSets)
        foreach (var idx in set)
            covered[idx] = true;

        Assert.True(covered.All(c => c), "Best solution has uncovered objects");
    }

    [Fact]
    public void BuildClosureMatrix_IsTransitive()
    {
        var algo = new DAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var Tt = algo.BuildClosureMatrix(TestData.TwoGroupData());

        int n = Tt.Size;
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        for (int k = 0; k < n; k++)
        {
            double transitiveMin = System.Math.Min(Tt[i, k], Tt[k, j]);
            Assert.True(Tt[i, j] >= transitiveMin - 1e-10,
                $"Transitivity violated: T̃[{i},{j}]={Tt[i, j]:F4} < min(T̃[{i},{k}],T̃[{k},{j}])={transitiveMin:F4}");
        }
    }
}

public class DAfcTcAlphaAlgorithmTests
{
    [Fact]
    public void Run_WithLowAlpha_FindsMorePermissiveClusters()
    {
        var algo = new DAfcTcAlphaAlgorithm(
            TestData.Builder(), TestData.Closure(), F1Criterion.Instance,
            userAlpha: 0.3);

        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Run_WithVeryHighAlpha_MayReturnEmpty()
    {
        var algo = new DAfcTcAlphaAlgorithm(
            TestData.Builder(), TestData.Closure(), F1Criterion.Instance,
            userAlpha: 0.999);

        // Very high alpha: possibly no valid clustering, or empty — both acceptable
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);
        // Just ensure no exception and result is a valid list
        Assert.NotNull(results);
    }
}

public class DPafcTcAlgorithmTests
{
    [Fact]
    public void Run_FindsSolutions_WithAutoClusterCount()
    {
        var algo = new DPafcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ClusterCount == 2);
    }

    [Fact]
    public void Run_AllResults_HaveFullCoverage()
    {
        var algo = new DPafcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);
        int n = TestData.TwoGroupData().Rows;

        foreach (var result in results)
        {
            var covered = new bool[n];
            foreach (var set in result.MemberSets)
            foreach (var idx in set)
                covered[idx] = true;

            Assert.True(covered.All(c => c),
                $"Result c={result.ClusterCount} α={result.Alpha:F3} has uncovered objects");
        }
    }
}

public class DAfcTagaAlgorithmTests
{
    [Fact]
    public void Run_FindsSolutions_WithMeanTaga()
    {
        var taga = new TAGAProcessor(MeanTAGA.Instance);
        var algo = new DAfcTagaAlgorithm(TestData.Builder(), taga, F1Criterion.Instance);
        var results = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData("Maximum")]
    [InlineData("Minimum")]
    [InlineData("Mean")]
    [InlineData("Median")]
    public void Run_DoesNotThrow_ForAllTagaOperators(string operatorName)
    {
        ITAGAOperator op = operatorName switch
        {
            "Maximum" => MaximumTAGA.Instance,
            "Minimum" => MinimumTAGA.Instance,
            "Mean"    => MeanTAGA.Instance,
            "Median"  => MedianTAGA.Instance,
            _         => MeanTAGA.Instance,
        };

        var taga = new TAGAProcessor(op);
        var algo = new DAfcTagaAlgorithm(TestData.Builder(), taga, F1Criterion.Instance);

        var exception = Record.Exception(() => algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken));
        Assert.Null(exception);
    }
}

public class HAfcTcAlgorithmTests
{
    [Fact]
    public void Run_ReturnsHierarchicalResult_WithLevels()
    {
        var algo = new HAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var result = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Root);
    }

    [Fact]
    public void Run_LevelsAreOrderedByAlphaAscending()
    {
        var algo = new HAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var result = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        for (int i = 1; i < result.Levels.Count; i++)
            Assert.True(result.Levels[i].Alpha >= result.Levels[i - 1].Alpha,
                $"Levels not ascending: [{i-1}].Alpha={result.Levels[i-1].Alpha:F3} > [{i}].Alpha={result.Levels[i].Alpha:F3}");
    }

    [Fact]
    public void Run_FinerLevels_HaveMoreOrEqualClusters()
    {
        var algo = new HAfcTcAlgorithm(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var result = algo.Run(TestData.TwoGroupData(), TestContext.Current.CancellationToken);

        if (result.Levels.Count < 2) return; // not enough levels to compare

        // As alpha increases (finer cuts), cluster count should be non-decreasing
        for (int i = 1; i < result.Levels.Count; i++)
            Assert.True(result.Levels[i].ClusterCount >= result.Levels[i - 1].ClusterCount,
                $"Level {i} has fewer clusters ({result.Levels[i].ClusterCount}) than level {i-1} ({result.Levels[i-1].ClusterCount})");
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F2")]
    public void Run_EachLevel_EqualsExhaustiveDAfcSearch(string criterionName)
    {
        ICriterion criterion = criterionName == "F1" ? F1Criterion.Instance : F2Criterion.Instance;
        var builder = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);
        var data = BookExampleTests.SneathSokal();
        var closureMatrix = TestData.Closure().Compute(builder.Build(data));

        var result = new HAfcTcAlgorithm(builder, TestData.Closure(), criterion)
            .Run(data, TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Levels);
        foreach (var level in result.Levels)
        {
            var exhaustive = new DAfcAlgorithm(
                    new DAfcOptions(level.ClusterCount), new ManualAlphaSelector([level.Alpha]), criterion)
                .RunForAlpha(closureMatrix, level.Alpha, TestContext.Current.CancellationToken)[0];

            // Same classes and the same optimum; the typical point may differ only among
            // members whose contributions tie up to rounding (see ClosureAllotment).
            Assert.Equal(exhaustive.Score, level.Score, tolerance: 1e-12);
            Assert.Equal(SortedClasses(exhaustive), SortedClasses(level));
        }
    }

    private static int[][] SortedClasses(ClusterResult result)
        => [.. result.MemberSets.OrderBy(members => members[0])];

    [Fact]
    public void Run_On150Objects_DoesNotEnumerateCenterCombinations()
    {
        // The combination search needed minutes here (∏|class| reaches 10¹⁰ per level).
        var random = new Random(150);
        var data = new RawDataMatrix(150, 4);
        for (int row = 0; row < 150; row++)
            for (int column = 0; column < 4; column++)
                data[row, column] = row % 3 + random.NextDouble();

        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(30));
        var builder = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);
        var result = new HAfcTcAlgorithm(builder, TestData.Closure(), F1Criterion.Instance).Run(data, guard.Token);

        Assert.True(result.Levels.Count > 50);
        Assert.All(result.Levels, level => Assert.Equal(150, level.MemberSets.Sum(members => members.Length)));
    }
}

public class DimensionReductionTests
{
    // 10 objects, 6 features: features {0,1,2} are correlated, {3,4,5} are correlated
    private static RawDataMatrix CorrelatedFeaturesData() => RawDataMatrix.FromArray(new double[,]
    {
        { 0.1, 0.12, 0.09,  0.9, 0.88, 0.92 },
        { 0.2, 0.21, 0.19,  0.8, 0.79, 0.81 },
        { 0.3, 0.31, 0.28,  0.7, 0.68, 0.72 },
        { 0.4, 0.42, 0.38,  0.6, 0.59, 0.61 },
        { 0.5, 0.52, 0.48,  0.5, 0.51, 0.49 },
        { 0.6, 0.61, 0.59,  0.4, 0.39, 0.41 },
        { 0.7, 0.69, 0.71,  0.3, 0.31, 0.29 },
        { 0.8, 0.82, 0.78,  0.2, 0.19, 0.21 },
        { 0.9, 0.91, 0.88,  0.1, 0.11, 0.09 },
        { 1.0, 0.99, 1.01,  0.0, 0.01, 0.02 },
    });

    [Fact]
    public void DPafcDr_ReducesDimensionality()
    {
        var reducer = new DPafcDrReducer(TestData.Builder(), F1Criterion.Instance);
        var reduced = reducer.Reduce(CorrelatedFeaturesData(), TestContext.Current.CancellationToken);

        // At minimum: no exception, correct row count, cols within [1, original]
        Assert.Equal(10, reduced.Rows);
        Assert.InRange(reduced.Cols, 1, 6);
    }

    [Fact]
    public void DPafcDr_SelectedIndices_AreValidColumnIndices()
    {
        var reducer = new DPafcDrReducer(TestData.Builder(), F1Criterion.Instance);
        reducer.Reduce(CorrelatedFeaturesData(), TestContext.Current.CancellationToken);

        Assert.All(reducer.SelectedAttributeIndices,
            idx => Assert.InRange(idx, 0, 5));
    }

    [Fact]
    public void DPafcTcDr_ReducesDimensionality()
    {
        var reducer = new DPafcTcDrReducer(TestData.Builder(), TestData.Closure(), F1Criterion.Instance);
        var reduced = reducer.Reduce(CorrelatedFeaturesData(), TestContext.Current.CancellationToken);

        Assert.True(reduced.Cols <= 6);
        Assert.Equal(10, reduced.Rows);
    }

    [Fact]
    public void DPafcTagaDr_ReducesDimensionality()
    {
        var taga = new TAGAProcessor(MeanTAGA.Instance);
        var reducer = new DPafcTagaDrReducer(TestData.Builder(), taga, F1Criterion.Instance);
        var reduced = reducer.Reduce(CorrelatedFeaturesData(), TestContext.Current.CancellationToken);

        Assert.True(reduced.Cols <= 6);
        Assert.Equal(10, reduced.Rows);
    }
}
