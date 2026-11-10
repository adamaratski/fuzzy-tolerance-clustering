using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// The exact-cover bound of the D-AFC search (a fully separated allotment is an efficient
/// dominating set of the α-cut graph, so the cluster powers sum to n) must not change any
/// result — it only removes work.
/// </summary>
public class ExactCoverPruningTests
{
    private static FuzzyToleranceMatrix SneathSokalTolerance() =>
        new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance)
            .Build(BookExampleTests.SneathSokal());

    private static FuzzyToleranceMatrix RandomTolerance(int objectCount, int seed)
    {
        var random = new Random(seed);
        var data = new RawDataMatrix(objectCount, 3);
        for (int row = 0; row < objectCount; row++)
            for (int column = 0; column < 3; column++)
                data[row, column] = row % 4 * 2.0 + random.NextDouble();
        return new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance).Build(data);
    }

    private static string Describe(IReadOnlyList<ClusterResult> results) =>
        string.Join("\n", results.Select(result =>
            $"{result.Alpha:R} {result.Score:R} [{string.Join(",", result.Centers)}] " +
            $"[{string.Join("|", result.MemberSets.Select(members => string.Join(",", members)))}]"));

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(3, 2)]
    [InlineData(4, 5)]
    [InlineData(3, DAfcOptions.AutoIntersections)]
    [InlineData(6, DAfcOptions.AutoIntersections)]
    public void Run_WithAndWithoutTheBound_ReturnsTheSameAllotments(int clusterCount, int maxIntersections)
    {
        foreach (var tolerance in new[] { SneathSokalTolerance(), RandomTolerance(28, seed: 7), RandomTolerance(33, seed: 11) })
        {
            var withBound = new SearchDiagnostics();
            var withoutBound = new SearchDiagnostics();

            var pruned = new DAfcAlgorithm(
                    new DAfcOptions(clusterCount, MaxIntersections: maxIntersections, MaxResults: 5000, Diagnostics: withBound),
                    AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
                .Run(tolerance, TestContext.Current.CancellationToken);
            var plain = new DAfcAlgorithm(
                    new DAfcOptions(clusterCount, MaxIntersections: maxIntersections, MaxResults: 5000,
                                    Diagnostics: withoutBound, ExactCoverPruning: false),
                    AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
                .Run(tolerance, TestContext.Current.CancellationToken);

            Assert.Equal(Describe(plain), Describe(pruned));
            Assert.True(withBound.NodesVisited <= withoutBound.NodesVisited,
                $"the bound visited more nodes ({withBound.NodesVisited}) than the plain search ({withoutBound.NodesVisited})");
            Assert.False(withBound.BudgetExhausted);
        }
    }

    [Fact]
    public void Run_FullySeparatedAllotments_HaveOneClusterCountPerLevel()
    {
        // Proposition: the number of fully separated clusters of a level is unique.
        var tolerance = SneathSokalTolerance();
        var countsByLevel = new Dictionary<double, HashSet<int>>();
        for (int clusterCount = 2; clusterCount <= 8; clusterCount++)
        {
            var found = new DAfcAlgorithm(
                    new DAfcOptions(clusterCount, MaxIntersections: 0, MaxResults: 100_000),
                    AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
                .Run(tolerance, TestContext.Current.CancellationToken);
            foreach (var result in found)
            {
                if (!countsByLevel.TryGetValue(result.Alpha, out var counts))
                    countsByLevel[result.Alpha] = counts = [];
                counts.Add(result.ClusterCount);
            }
        }

        Assert.NotEmpty(countsByLevel);
        Assert.All(countsByLevel.Values, counts => Assert.Single(counts));

        // …and it does not decrease as the level rises.
        var ordered = countsByLevel.OrderBy(entry => entry.Key).Select(entry => entry.Value.Single()).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            Assert.True(ordered[index] >= ordered[index - 1]);
    }

    [Fact]
    public void Run_WhenTheNodeBudgetIsSpent_SaysSo()
    {
        var tolerance = RandomTolerance(40, seed: 3);
        var diagnostics = new SearchDiagnostics();

        new DAfcAlgorithm(
                new DAfcOptions(4, MaxIntersections: int.MaxValue, MaxCombinations: 50, Diagnostics: diagnostics),
                AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(tolerance, TestContext.Current.CancellationToken);

        Assert.True(diagnostics.BudgetExhausted);
        Assert.True(diagnostics.NodesVisited >= 50);
    }

    [Fact]
    public void Run_WithinTheNodeBudget_DoesNotReportExhaustion()
    {
        var diagnostics = new SearchDiagnostics();

        var found = new DAfcAlgorithm(
                new DAfcOptions(4, Diagnostics: diagnostics),
                AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(SneathSokalTolerance(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(found);
        Assert.False(diagnostics.BudgetExhausted);
        Assert.True(diagnostics.NodesVisited > 0);
        Assert.True(diagnostics.LevelsSearched <= diagnostics.LevelsTotal);
    }

    [Fact]
    public void DPafc_WhenTheNodeBudgetIsSpent_ReturnsNothingRatherThanAWrongPrincipalAllotment()
    {
        // unbounded w switches the exact-cover bound off, so the tiny budget is certain to run out
        var diagnostics = new SearchDiagnostics();

        var found = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance,
                                       maxIntersections: int.MaxValue, maxSearchNodes: 1, diagnostics: diagnostics)
            .Run(RandomTolerance(40, seed: 5), TestContext.Current.CancellationToken);

        if (diagnostics.BudgetExhausted)
            Assert.Empty(found);
        else
            Assert.NotEmpty(found);
    }
}

/// <summary>
/// On a transitive closure the allotments of a level are ranked class by class
/// (<c>ClosureAllotment</c>); the ranking must agree with the combination search.
/// </summary>
public class ClosureRankingTests
{
    private static readonly ToleranceMatrixBuilder Builder =
        new(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);

    [Theory]
    [InlineData("F1")]
    [InlineData("F2")]
    public void DAfcTc_RankedList_EqualsTheCombinationSearch(string criterionName)
    {
        ICriterion criterion = criterionName == "F1" ? F1Criterion.Instance : F2Criterion.Instance;
        var data = BookExampleTests.SneathSokal();
        var closure = new MaxMinTransitiveClosure();
        var closureMatrix = closure.Compute(Builder.Build(data));
        var normalized = Builder.Normalize(data);

        var ranked = new DAfcTcAlgorithm(Builder, closure, criterion, maxResults: 100_000)
            .Run(data, TestContext.Current.CancellationToken);
        Assert.NotEmpty(ranked);

        double alpha = ranked[0].Alpha;
        var searched = new DAfcAlgorithm(
                new DAfcOptions(ranked[0].ClusterCount, MaxIntersections: 0, MaxResults: 100_000),
                new ManualAlphaSelector([alpha]), criterion)
            .RunForAlpha(closureMatrix, alpha, TestContext.Current.CancellationToken);

        // the same allotments …
        static string Key(ClusterResult result) => string.Join(",", result.Centers);
        Assert.Equal(searched.Select(Key).Order(), ranked.Select(Key).Order());

        // … with the same criterion values (up to the tie tolerance) …
        var searchedScores = searched.ToDictionary(Key, result => result.Score);
        Assert.All(ranked, result => Assert.Equal(searchedScores[Key(result)], result.Score, tolerance: 1e-12));

        // … in an order that never improves: criterion first, then the centroid distance.
        for (int index = 1; index < ranked.Count; index++)
        {
            Assert.True(ranked[index].Score <= ranked[index - 1].Score);
            if (ranked[index].Score == ranked[index - 1].Score)
                Assert.True(ranked[index].TiebreakerScore >= ranked[index - 1].TiebreakerScore);
        }

        // the reported tie-breaker is the centroid distance of the allotment
        Assert.All(ranked, result => Assert.Equal(
            Distance(result, normalized), result.TiebreakerScore!.Value, tolerance: 1e-9));
    }

    private static double Distance(ClusterResult result, RawDataMatrix normalized)
    {
        double total = 0.0;
        for (int cluster = 0; cluster < result.ClusterCount; cluster++)
        {
            var members = result.MemberSets[cluster];
            double sumOfSquares = 0.0;
            for (int feature = 0; feature < normalized.Cols; feature++)
            {
                double mean = members.Average(member => normalized[member, feature]);
                double difference = normalized[result.Centers[cluster], feature] - mean;
                sumOfSquares += difference * difference;
            }
            total += System.Math.Sqrt(sumOfSquares);
        }
        return total;
    }

    [Fact]
    public void DAfcTc_MaxResults_KeepsTheBestOnes()
    {
        var data = BookExampleTests.SneathSokal();
        var closure = new MaxMinTransitiveClosure();

        var all = new DAfcTcAlgorithm(Builder, closure, F1Criterion.Instance, maxResults: 100_000)
            .Run(data, TestContext.Current.CancellationToken);
        var top = new DAfcTcAlgorithm(Builder, closure, F1Criterion.Instance, maxResults: 7)
            .Run(data, TestContext.Current.CancellationToken);

        Assert.True(all.Count > 7);
        Assert.Equal(7, top.Count);
        Assert.Equal(all.Take(7).Select(result => string.Join(",", result.Centers)),
                     top.Select(result => string.Join(",", result.Centers)));
    }

    [Fact]
    public void HAfcTc_LevelOfTheLeapThreshold_IsTheAllotmentOfDAfcTc()
    {
        var data = BookExampleTests.SneathSokal();
        var closure = new MaxMinTransitiveClosure();

        var best = new DAfcTcAlgorithm(Builder, closure, F1Criterion.Instance)
            .Run(data, TestContext.Current.CancellationToken)[0];
        var hierarchy = new Algorithms.Hierarchical.HAfcTcAlgorithm(Builder, closure, F1Criterion.Instance)
            .Run(data, TestContext.Current.CancellationToken);

        var level = Assert.Single(hierarchy.Levels, candidate => candidate.Alpha == best.Alpha);
        Assert.Equal(best.Centers, level.Centers);
        Assert.Equal(best.Score, level.Score);
    }

    [Fact]
    public void DAfcTc_WithAGivenClusterCount_ReturnsOnlyThatCount()
    {
        var data = BookExampleTests.SneathSokal();
        var algorithm = new DAfcTcAlgorithm(Builder, new MaxMinTransitiveClosure(), F1Criterion.Instance);

        var found = algorithm.RunForClusterCount(Builder.Build(data), 3, Builder.Normalize(data),
                                                 TestContext.Current.CancellationToken);

        Assert.All(found, result => Assert.Equal(3, result.ClusterCount));
        for (int index = 1; index < found.Count; index++)
            Assert.True(ClusterResult.Order.Compare(found[index - 1], found[index]) <= 0);
    }
}

/// <summary>A fuzzy tolerance matrix given directly, without attribute data.</summary>
public class MatrixInputTests
{
    private static readonly AlgorithmOptions MonographSetting =
        new(Distance: "squared-euclidean", Normalization: "max");

    [Theory]
    [InlineData("dpafc")]
    [InlineData("dafc-tc")]
    [InlineData("dpafc-tc")]
    [InlineData("hafc-tc")]
    public void Run_OnTheToleranceMatrix_GivesTheClustersOfTheDataRun(string algorithm)
    {
        var data = BookExampleTests.SneathSokal();
        var tolerance = AlgorithmFactory.CreateToleranceBuilder(MonographSetting).Build(data);

        var fromData = AlgorithmFactory.Run(algorithm, data, MonographSetting, TestContext.Current.CancellationToken);
        var fromMatrix = AlgorithmFactory.Run(algorithm, tolerance, new AlgorithmOptions(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(fromData);
        Assert.Equal(fromData[0].Alpha, fromMatrix[0].Alpha);
        Assert.Equal(fromData[0].MemberSets.Select(members => string.Join(",", members)).Order(),
                     fromMatrix[0].MemberSets.Select(members => string.Join(",", members)).Order());
        Assert.Equal(fromData[0].Score, fromMatrix[0].Score, tolerance: 1e-12);
    }

    [Fact]
    public void Run_Dafc_OnTheToleranceMatrix_IsIdenticalToTheDataRun()
    {
        var data = BookExampleTests.SneathSokal();
        var options = MonographSetting with { ClusterCount = 4 };
        var tolerance = AlgorithmFactory.CreateToleranceBuilder(MonographSetting).Build(data);

        var fromData = AlgorithmFactory.Run("dafc", data, options, TestContext.Current.CancellationToken);
        var fromMatrix = AlgorithmFactory.Run("dafc", tolerance, new AlgorithmOptions(ClusterCount: 4),
                                              TestContext.Current.CancellationToken);

        Assert.Equal(fromData.Select(result => (result.Alpha, result.Score, string.Join(",", result.Centers))),
                     fromMatrix.Select(result => (result.Alpha, result.Score, string.Join(",", result.Centers))));
    }

    [Fact]
    public void Run_PrototypeAlgorithmOnAMatrix_HasNoGeometricTieBreaker()
    {
        var tolerance = AlgorithmFactory.CreateToleranceBuilder(MonographSetting).Build(BookExampleTests.SneathSokal());

        var found = AlgorithmFactory.Run("dafc-tc", tolerance, new AlgorithmOptions(), TestContext.Current.CancellationToken);

        Assert.All(found, result => Assert.Null(result.TiebreakerScore));
    }

    [Fact]
    public void Run_ReductionAlgorithmOnAMatrix_IsRejected()
    {
        var tolerance = new FuzzyToleranceMatrix(3);
        Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.Run("dpafc-dr", tolerance, new AlgorithmOptions(), TestContext.Current.CancellationToken));
    }
}

public class OptionValidationTests
{
    [Theory]
    [InlineData("dpafc")]
    [InlineData("dpafc-tc")]
    [InlineData("hafc-tc")]
    [InlineData("dafc-taga")]
    public void EnsureOptionsApply_ClusterCountForAnAlgorithmThatFindsIt_IsRejected(string algorithm)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.EnsureOptionsApply(algorithm, new AlgorithmOptions(ClusterCount: 3)));
        Assert.Contains("number of clusters", error.Message);
    }

    [Theory]
    [InlineData("dpafc")]
    [InlineData("dafc-tc")]
    [InlineData("hafc-tc")]
    public void EnsureOptionsApply_ThresholdForAnAlgorithmThatSelectsIt_IsRejected(string algorithm)
    {
        Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.EnsureOptionsApply(algorithm, new AlgorithmOptions(UserAlpha: 0.9)));
    }

    [Fact]
    public void EnsureOptionsApply_OperatorWithoutATagaAlgorithm_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.EnsureOptionsApply("dafc-tc", new AlgorithmOptions(TagaOperator: "mean")));
    }

    [Fact]
    public void EnsureOptionsApply_IntersectionBoundOnAClosure_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.EnsureOptionsApply("dpafc-tc", new AlgorithmOptions(MaxIntersections: 2)));
    }

    [Fact]
    public void EnsureOptionsApply_MissingRequiredOptions_AreReported()
    {
        Assert.Throws<ArgumentException>(() => AlgorithmFactory.EnsureOptionsApply("dafc", new AlgorithmOptions()));
        Assert.Throws<ArgumentException>(() => AlgorithmFactory.EnsureOptionsApply("dafc-tc-alpha", new AlgorithmOptions()));
    }

    [Theory]
    [InlineData("dafc", 3, 0.9, 2)]
    [InlineData("dafc-tc", 3, null, null)]
    [InlineData("dafc-tc-alpha", null, 0.9, null)]
    [InlineData("dpafc", null, null, 2)]
    public void EnsureOptionsApply_OptionsTheAlgorithmUses_AreAccepted(string algorithm, int? clusterCount, double? alpha, int? intersections)
    {
        AlgorithmFactory.EnsureOptionsApply(algorithm,
            new AlgorithmOptions(ClusterCount: clusterCount, UserAlpha: alpha, MaxIntersections: intersections));
    }

    [Fact]
    public void EnsureOptionsApply_UnknownAlgorithm_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => AlgorithmFactory.EnsureOptionsApply("kmeans", new AlgorithmOptions()));
    }
}

public class OverlapValidationTests
{
    [Fact]
    public void Compute_ObjectInTwoClusters_IsCountedWhereItsMembershipIsLargest()
    {
        // Object 2 lies in both clusters with memberships 0.9 (cluster 0) and 0.4 (cluster 1).
        var membership = new double[5, 2];
        membership[0, 0] = 1.0; membership[1, 0] = 0.8; membership[2, 0] = 0.9;
        membership[2, 1] = 0.4; membership[3, 1] = 1.0; membership[4, 1] = 0.7;
        var result = new ClusterResult(2, 0.4, [0, 3], [[0, 1, 2], [2, 3, 4]], membership, 0.0, "F1");

        var validation = ExternalValidator.Compute(result, [0, 0, 0, 1, 1], ["a", "b"]);

        Assert.Equal(1.0, validation.Purity);
        Assert.Equal(1.0, validation.AdjustedRandIndex, precision: 12);
    }
}
