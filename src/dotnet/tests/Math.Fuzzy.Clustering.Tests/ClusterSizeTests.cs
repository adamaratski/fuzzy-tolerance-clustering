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
/// D-AFC(u) and the techniques built on it, checked against the published examples:
/// Tamura's portraits (Viattchenin, Yaroma, Damaratski 2014, Tables 2–5; Viattchenin 2016) and
/// the Sneath–Sokal data (Viattchenin 2016, Table 2; Viattchenin and Yaroma 2017).
/// </summary>
public class DAfcUTests
{
    /// <summary>Tamura's fuzzy tolerance on 16 portraits (lower triangle as printed).</summary>
    internal static FuzzyToleranceMatrix TamuraPortraits()
    {
        double[][] lowerTriangle =
        [
            [1.0],
            [0.0, 1.0],
            [0.0, 0.0, 1.0],
            [0.0, 0.0, 0.4, 1.0],
            [0.0, 0.8, 0.0, 0.0, 1.0],
            [0.5, 0.0, 0.2, 0.2, 0.0, 1.0],
            [0.0, 0.8, 0.0, 0.0, 0.4, 0.0, 1.0],
            [0.4, 0.2, 0.2, 0.5, 0.0, 0.8, 0.0, 1.0],
            [0.0, 0.4, 0.0, 0.8, 0.4, 0.2, 0.4, 0.0, 1.0],
            [0.0, 0.0, 0.2, 0.2, 0.0, 0.0, 0.2, 0.0, 0.2, 1.0],
            [0.0, 0.5, 0.2, 0.2, 0.0, 0.0, 0.8, 0.0, 0.4, 0.2, 1.0],
            [0.0, 0.0, 0.2, 0.8, 0.0, 0.0, 0.0, 0.0, 0.4, 0.8, 0.0, 1.0],
            [0.8, 0.0, 0.2, 0.4, 0.0, 0.4, 0.0, 0.4, 0.0, 0.0, 0.0, 0.0, 1.0],
            [0.0, 0.8, 0.0, 0.2, 0.4, 0.0, 0.8, 0.0, 0.2, 0.2, 0.6, 0.0, 0.0, 1.0],
            [0.0, 0.0, 0.4, 0.8, 0.0, 0.2, 0.0, 0.0, 0.2, 0.0, 0.0, 0.2, 0.2, 0.0, 1.0],
            [0.6, 0.0, 0.0, 0.2, 0.2, 0.8, 0.0, 0.4, 0.0, 0.0, 0.0, 0.0, 0.4, 0.2, 0.0, 1.0],
        ];
        var matrix = new FuzzyToleranceMatrix(16);
        for (int row = 0; row < 16; row++)
            for (int column = 0; column < row; column++)
                matrix[row, column] = lowerTriangle[row][column];
        return matrix;
    }

    private static FuzzyToleranceMatrix SneathSokalTolerance() =>
        new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance)
            .Build(BookExampleTests.SneathSokal());

    private static ClusterResult Best(FuzzyToleranceMatrix tolerance, int maxClusterPower) =>
        new DAfcUAlgorithm(maxClusterPower, F1Criterion.Instance).Run(tolerance, TestContext.Current.CancellationToken)[0];

    private static int[] OneBased(int[] indices) => [.. indices.Select(index => index + 1)];

    [Theory]
    // u, α*, w*, c, typical points (1-based), F1
    [InlineData(3, 0.6, 1, new[] { 3, 5, 6, 9, 10, 11, 13, 15 }, 2.366667)]
    [InlineData(4, 0.6, 1, new[] { 3, 4, 5, 6, 10, 11, 13 }, 2.016667)]
    [InlineData(5, 0.4, 1, new[] { 6, 12, 14, 15 }, 1.303333)]
    [InlineData(6, 0.4, 0, new[] { 2, 6, 10, 15 }, 1.45)]
    [InlineData(8, 0.4, 0, new[] { 2, 6, 10, 15 }, 1.45)]
    public void TamuraPortraits_ReproduceThePublishedAllotments(
        int maxClusterPower, double alpha, int intersections, int[] typicalPoints, double criterionValue)
    {
        var best = Best(TamuraPortraits(), maxClusterPower);

        Assert.Equal(alpha, best.Alpha, precision: 12);
        Assert.Equal(intersections, best.IntersectionCount);
        Assert.Equal(typicalPoints, OneBased(best.Centers));
        Assert.Equal(criterionValue, best.Score, precision: 6);
        Assert.All(best.MemberSets, members => Assert.True(members.Length <= maxClusterPower));
    }

    [Fact]
    public void TamuraPortraits_WithTwoObjectsPerCluster_HaveNoAllotment()
    {
        Assert.Empty(new DAfcUAlgorithm(2, F1Criterion.Instance)
            .Run(TamuraPortraits(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(4, 119.0 / 128.0, new[] { 4, 4, 4, 4 })]
    [InlineData(5, 263.0 / 288.0, new[] { 5, 3, 3, 5 })]
    [InlineData(9, 263.0 / 288.0, new[] { 5, 3, 3, 5 })]
    public void SneathSokal_ReproducesThePublishedAllotments(int maxClusterPower, double alpha, int[] sizes)
    {
        var best = Best(SneathSokalTolerance(), maxClusterPower);

        Assert.Equal(alpha, best.Alpha, precision: 12);
        Assert.Equal(0, best.IntersectionCount);
        Assert.Equal(sizes, best.MemberSets.Select(members => members.Length).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LargeEnoughBound_GivesThePrincipalAllotment(bool tamura)
    {
        var tolerance = tamura ? TamuraPortraits() : SneathSokalTolerance();
        var principal = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance)
            .Run(tolerance, TestContext.Current.CancellationToken)[0];

        var best = Best(tolerance, tolerance.Size - 1);

        Assert.Equal(principal.Alpha, best.Alpha);
        Assert.Equal(principal.Centers, best.Centers);
        Assert.Equal(principal.Score, best.Score);
    }

    [Fact]
    public void BoundNotBelowTheNumberOfObjects_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DAfcUAlgorithm(16, F1Criterion.Instance)
            .Run(TamuraPortraits(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Factory_RunsDAfcU()
    {
        var found = AlgorithmFactory.Run("dafc-u", TamuraPortraits(), new AlgorithmOptions(MaxClusterPower: 6),
                                         TestContext.Current.CancellationToken);

        Assert.Equal([2, 6, 10, 15], OneBased(found[0].Centers));
        Assert.Throws<ArgumentException>(() => AlgorithmFactory.EnsureOptionsApply("dafc-u", new AlgorithmOptions()));
    }
}

public class AllotmentMeasuresTests
{
    [Fact]
    public void SneathSokal_ClusterIndices_EqualThePublishedTable()
    {
        // Viattchenin 2016, Table 2: the allotment of D-AFC(5), clusters of 5, 3, 3 and 5 objects.
        var tolerance = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance)
            .Build(BookExampleTests.SneathSokal());
        var best = new DAfcUAlgorithm(5, F1Criterion.Instance).Run(tolerance, TestContext.Current.CancellationToken)[0];

        var measures = AllotmentMeasures.Compute(best);

        double[] linear    = [0.07743056, 0.05671297, 0.04456019, 0.06875000];
        double[] quadratic = [0.09997046, 0.07734306, 0.05783274, 0.08579271];
        double[] density   = [0.96128472, 0.97164352, 0.97771991, 0.96562500];
        for (int cluster = 0; cluster < 4; cluster++)
        {
            Assert.Equal(linear[cluster], measures.Clusters[cluster].LinearFuzziness, tolerance: 1.1e-8);
            Assert.Equal(quadratic[cluster], measures.Clusters[cluster].QuadraticFuzziness, tolerance: 1.1e-8);
            Assert.Equal(density[cluster], measures.Clusters[cluster].Density, tolerance: 1.1e-8);
        }
        Assert.Equal(0.247454, measures.LinearFuzziness, precision: 6);
        Assert.Equal(0.320939, measures.QuadraticFuzziness, precision: 6);
        Assert.Equal(0.055874, measures.SeparationCompactness, precision: 6);
    }

    [Theory]
    // u, V_LMF, V_QMF, V_MSC on Tamura's portraits
    [InlineData(3, 1.666667, 2.257210, 1.095833)]
    [InlineData(5, 1.800000, 2.198080, 0.725833)]
    [InlineData(6, 1.620000, 2.019758, 0.362500)]
    public void TamuraPortraits_Measures(int maxClusterPower, double linear, double quadratic, double separation)
    {
        var best = new DAfcUAlgorithm(maxClusterPower, F1Criterion.Instance)
            .Run(DAfcUTests.TamuraPortraits(), TestContext.Current.CancellationToken)[0];

        var measures = AllotmentMeasures.Compute(best);

        Assert.Equal(linear, measures.LinearFuzziness, precision: 6);
        Assert.Equal(quadratic, measures.QuadraticFuzziness, precision: 6);
        Assert.Equal(separation, measures.SeparationCompactness, precision: 6);
    }
}

public class ClusterSizeSelectionTests
{
    [Theory]
    [InlineData("lmf")]
    [InlineData("qmf")]
    [InlineData("msc")]
    public void TamuraPortraits_OptimalBoundIsSix(string measure)
    {
        var selection = ClusterSizeSelection.Scan(DAfcUTests.TamuraPortraits(), 3, 8, measure, F1Criterion.Instance,
                                                  ct: TestContext.Current.CancellationToken);

        Assert.Equal(6, selection.OptimalMaxClusterPower);
        Assert.Equal(6, selection.Steps.Count);
    }

    [Fact]
    public void SneathSokal_LeastBoundIsThree_OptimalBoundIsFive()
    {
        var builder = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);
        var data = BookExampleTests.SneathSokal();

        int? least = ClusterSizeSelection.LeastMaxClusterPower(data, builder, F1Criterion.Instance,
                                                               TestContext.Current.CancellationToken);
        var selection = ClusterSizeSelection.Scan(builder.Build(data), 3, 8, "lmf", F1Criterion.Instance,
                                                  ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, least);   // D-PAFC-TC: clusters of 13 and 3 objects
        Assert.Equal(5, selection.OptimalMaxClusterPower);
        Assert.True(selection.Steps[0].Tied);
    }

    [Fact]
    public void Scan_ValueWithoutAnAllotment_IsRecordedAndSkipped()
    {
        var selection = ClusterSizeSelection.Scan(DAfcUTests.TamuraPortraits(), 2, 4, "lmf", F1Criterion.Instance,
                                                  ct: TestContext.Current.CancellationToken);

        Assert.Null(selection.Steps[0].Allotment);
        Assert.NotNull(selection.Steps[1].Allotment);
        Assert.Null(selection.OptimalMaxClusterPower);
    }
}

public class ClusterCountBoundsTests
{
    [Fact]
    public void Iris_Triangular_EqualsThePublishedSet()
    {
        // Viattchenin, Yaroma, Damaratski 2016: n = 150, c = 2, α = 0.80384 → {2, …, 31}
        var bounds = ClusterCountBounds.Compute(150, 2, 0.80384, ClusterCountShape.Triangular);

        Assert.Equal(2, bounds.Least);
        Assert.Equal(31, bounds.Largest);
        Assert.Equal(0.8041, bounds.Possibility[^1], precision: 4);
    }

    [Fact]
    public void Iris_Gaussian_EqualsThePublishedSet()
    {
        var bounds = ClusterCountBounds.Compute(150, 2, 0.80384, ClusterCountShape.Gaussian);

        Assert.Equal(2, bounds.Least);
        Assert.Equal(12, bounds.Largest);
        Assert.Equal(0.9981, bounds.Possibility[1], precision: 4);
        Assert.Equal(0.8243, bounds.Possibility[^1], precision: 4);
    }

    [Fact]
    public void SneathSokal_Bounds()
    {
        var triangular = ClusterCountBounds.Compute(16, 4, 263.0 / 288.0, ClusterCountShape.Triangular);
        var gaussian = ClusterCountBounds.Compute(16, 4, 263.0 / 288.0, ClusterCountShape.Gaussian);

        Assert.Equal((4, 5), (triangular.Least, triangular.Largest));
        Assert.Equal((4, 4), (gaussian.Least, gaussian.Largest));
    }

    [Theory]
    [InlineData(150, 1, 0.5)]
    [InlineData(150, 150, 0.5)]
    [InlineData(150, 3, 0.0)]
    [InlineData(150, 3, 1.0)]
    public void InvalidArguments_AreRejected(int objectCount, int clusterCount, double alpha)
    {
        Assert.Throws<ArgumentException>(() =>
            ClusterCountBounds.Compute(objectCount, clusterCount, alpha, ClusterCountShape.Triangular));
    }
}
