using MathFuzzy.Clustering;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// End-to-end smoke tests: every algorithm key must return non-null results
/// without throwing on well-formed two-group data.
/// </summary>
public class AlgorithmFactoryTests
{
    // 8 objects in 2D: clear 2-group structure
    private static RawDataMatrix TwoGroupData() => RawDataMatrix.FromArray(new double[,]
    {
        { 0.05, 0.05 }, { 0.10, 0.08 }, { 0.08, 0.12 }, { 0.06, 0.09 },
        { 0.92, 0.95 }, { 0.95, 0.90 }, { 0.88, 0.93 }, { 0.91, 0.88 },
    });

    private static readonly AlgorithmOptions DefaultOptions = new(
        Distance: "euclidean", Normalization: "minmax");

    private static readonly AlgorithmOptions WithClusters2 = DefaultOptions with { ClusterCount = 2 };
    private static readonly AlgorithmOptions WithAlpha = DefaultOptions with { UserAlpha = 0.4 };

    [Theory]
    [InlineData("dpafc")]
    [InlineData("dafc-tc")]
    [InlineData("dpafc-tc")]
    [InlineData("dafc-taga")]
    [InlineData("dpafc-taga")]
    [InlineData("hafc-tc")]
    [InlineData("dpafc-dr")]
    [InlineData("dpafc-tc-dr")]
    [InlineData("dpafc-taga-dr")]
    public void Run_AutoCluster_ReturnsNonNullList(string algorithm)
    {
        var results = AlgorithmFactory.Run(algorithm, TwoGroupData(), DefaultOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(results);
    }

    [Theory]
    [InlineData("dafc")]
    [InlineData("dafc-tc")]
    public void Run_WithClusterCount_ReturnsResults(string algorithm)
    {
        var results = AlgorithmFactory.Run(algorithm, TwoGroupData(), WithClusters2, TestContext.Current.CancellationToken);
        Assert.NotNull(results);
    }

    [Fact]
    public void Run_DafcTcAlpha_WithUserAlpha_ReturnsResults()
    {
        var results = AlgorithmFactory.Run("dafc-tc-alpha", TwoGroupData(), WithAlpha, TestContext.Current.CancellationToken);
        Assert.NotNull(results);
    }

    [Theory]
    [InlineData("euclidean")]
    [InlineData("hamming")]
    public void Run_DistanceVariants_DoNotThrow(string distance)
    {
        var opts = DefaultOptions with { Distance = distance };
        var ex = Record.Exception(() => AlgorithmFactory.Run("dpafc", TwoGroupData(), opts, TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("minmax")]
    [InlineData("max")]
    [InlineData("none")]
    public void Run_NormalizationVariants_DoNotThrow(string norm)
    {
        var opts = DefaultOptions with { Normalization = norm };
        var ex = Record.Exception(() => AlgorithmFactory.Run("dpafc", TwoGroupData(), opts, TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("max")]
    [InlineData("min")]
    [InlineData("mean")]
    [InlineData("median")]
    [InlineData("umedian")]
    [InlineData("dmedian")]
    public void Run_TagaOperators_DoNotThrow(string op)
    {
        var opts = DefaultOptions with { TagaOperator = op };
        var ex = Record.Exception(() => AlgorithmFactory.Run("dpafc-taga", TwoGroupData(), opts, TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }

    [Fact]
    public void Run_UnknownAlgorithm_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AlgorithmFactory.Run("unknown-algo", TwoGroupData(), DefaultOptions, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Run_DafcWithoutClusterCount_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AlgorithmFactory.Run("dafc", TwoGroupData(), DefaultOptions, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Run_AllResults_HaveValidClusterCount()
    {
        var results = AlgorithmFactory.Run("dpafc-tc", TwoGroupData(), DefaultOptions, TestContext.Current.CancellationToken);
        foreach (var r in results)
            Assert.True(r.ClusterCount >= 2 && r.ClusterCount <= TwoGroupData().Rows,
                $"ClusterCount={r.ClusterCount} out of expected range");
    }
}
