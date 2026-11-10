using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// The transitive approximation (TAGA) and the D-AFC-TAGA family, checked on small matrices
/// worked out by hand and on the published Sneath–Sokal table (Viattchenin and Damaratski 2013).
/// </summary>
public class TransitiveApproximationTests
{
    private static ITAGAOperator Operator(string name) => name switch
    {
        "max" => MaximumTAGA.Instance,
        "min" => MinimumTAGA.Instance,
        "mean" => MeanTAGA.Instance,
        "median" => MedianTAGA.Instance,
        "umedian" => UpperMedianTAGA.Instance,
        _ => LowerMedianTAGA.Instance
    };

    private static FuzzyToleranceMatrix Matrix(double[,] values) => FuzzyToleranceMatrix.FromArray(values);

    private static void AssertMatrix(double[,] expected, FuzzyToleranceMatrix actual)
    {
        for (int row = 0; row < actual.Size; row++)
            for (int column = 0; column < actual.Size; column++)
                Assert.Equal(expected[row, column], actual[row, column], precision: 12);
    }

    /// <summary>The five-object example of the book.</summary>
    private static readonly double[,] BookExample =
    {
        { 1.0, 0.8, 0.0, 0.1, 0.2 },
        { 0.8, 1.0, 0.4, 0.0, 0.9 },
        { 0.0, 0.4, 1.0, 0.0, 0.0 },
        { 0.1, 0.0, 0.0, 1.0, 0.5 },
        { 0.2, 0.9, 0.0, 0.5, 1.0 },
    };

    [Fact]
    public void BookExample_Maximum_IsTheTransitiveClosure()
    {
        var approximation = new TAGAProcessor(MaximumTAGA.Instance).ApplyWithDetails(Matrix(BookExample));

        AssertMatrix(new[,]
        {
            { 1.0, 0.8, 0.4, 0.5, 0.8 },
            { 0.8, 1.0, 0.4, 0.5, 0.9 },
            { 0.4, 0.4, 1.0, 0.4, 0.4 },
            { 0.5, 0.5, 0.4, 1.0, 0.5 },
            { 0.8, 0.9, 0.4, 0.5, 1.0 },
        }, approximation.Approximation);
        Assert.Equal(5.4, approximation.Distance, precision: 12);

        var closure = new MaxMinTransitiveClosure().Compute(Matrix(BookExample));
        Assert.Equal(0.0, TAGAProcessor.Distance(closure, approximation.Approximation), precision: 12);
    }

    [Fact]
    public void BookExample_Mean()
    {
        var approximation = new TAGAProcessor(MeanTAGA.Instance).ApplyWithDetails(Matrix(BookExample));

        AssertMatrix(new[,]
        {
            { 1.0, 0.5, 0.1, 0.2, 0.5 },
            { 0.5, 1.0, 0.1, 0.2, 0.9 },
            { 0.1, 0.1, 1.0, 0.1, 0.1 },
            { 0.2, 0.2, 0.1, 1.0, 0.2 },
            { 0.5, 0.9, 0.1, 0.2, 1.0 },
        }, approximation.Approximation);
        Assert.Equal(3.6, approximation.Distance, precision: 12);
    }

    [Theory]
    [InlineData("min", 3.2)]
    [InlineData("median", 4.1)]
    [InlineData("umedian", 5.4)]
    [InlineData("dmedian", 3.2)]
    public void BookExample_Distances(string operatorName, double distance)
    {
        var approximation = new TAGAProcessor(Operator(operatorName)).ApplyWithDetails(Matrix(BookExample));
        Assert.Equal(distance, approximation.Distance, precision: 12);
    }

    [Fact]
    public void BookExample_AutomaticChoice_TakesTheNearestApproximation()
    {
        // distances: max 5.4, min 3.2, mean 3.6, median 4.1, upmedian 5.4, downmedian 3.2 → minimum (first of the two nearest)
        var chosen = TAGAProcessor.Automatic.ApplyWithDetails(Matrix(BookExample));

        Assert.Same(MinimumTAGA.Instance, chosen.Operator);
        Assert.Equal(3.2, chosen.Distance, precision: 12);
    }

    [Fact]
    public void GroupThatDoesNotClose_IsMetAgainAtALowerLevel()
    {
        // With the mean the group {1,2,3,4} at level 0.6 aggregates to 0.36 and stays open.
        var tolerance = Matrix(new[,]
        {
            { 1.0, 0.8, 0.0, 0.6 },
            { 0.8, 1.0, 0.0, 0.6 },
            { 0.0, 0.0, 1.0, 0.6 },
            { 0.6, 0.6, 0.6, 1.0 },
        });

        var mean = new TAGAProcessor(MeanTAGA.Instance).ApplyWithDetails(tolerance);
        var minimum = new TAGAProcessor(MinimumTAGA.Instance).ApplyWithDetails(tolerance);

        AssertMatrix(new[,]
        {
            { 1.0, 0.8, 0.36, 0.36 },
            { 0.8, 1.0, 0.36, 0.36 },
            { 0.36, 0.36, 1.0, 0.36 },
            { 0.36, 0.36, 0.36, 1.0 },
        }, mean.Approximation);
        Assert.Equal(2.88, mean.Distance, precision: 12);
        Assert.Equal(3.6, minimum.Distance, precision: 12);
    }

    [Theory]
    // value between the blocks {1,5} and {2,3,4}; the median depends on the scanning order
    [InlineData("max", 0.9, 6.9)]
    [InlineData("mean", 0.4, 5.2)]
    [InlineData("median", 0.4, 4.9)]
    [InlineData("umedian", 0.7, 5.3)]
    [InlineData("dmedian", 0.1, 4.9)]
    public void TwoGroupsAtTheTopLevel_AreProcessedInScanningOrder(string operatorName, double between, double distance)
    {
        var tolerance = Matrix(new[,]
        {
            { 1.0, 0.9, 0.0, 0.7, 0.95 },
            { 0.9, 1.0, 0.5, 0.95, 0.1 },
            { 0.0, 0.5, 1.0, 0.95, 0.1 },
            { 0.7, 0.95, 0.95, 1.0, 0.6 },
            { 0.95, 0.1, 0.1, 0.6, 1.0 },
        });

        var approximation = new TAGAProcessor(Operator(operatorName)).ApplyWithDetails(tolerance);

        Assert.Equal(0.95, approximation.Approximation[0, 4], precision: 12);
        Assert.Equal(between, approximation.Approximation[0, 1], precision: 12);
        Assert.Equal(between, approximation.Approximation[4, 2], precision: 12);
        Assert.Equal(distance, approximation.Distance, precision: 12);
    }

    [Theory]
    [InlineData("max")]
    [InlineData("min")]
    [InlineData("mean")]
    [InlineData("median")]
    [InlineData("umedian")]
    [InlineData("dmedian")]
    public void Approximation_OfRandomTolerances_IsMinTransitive(string operatorName)
    {
        var random = new Random(operatorName.Length * 31 + operatorName[0]);
        for (int trial = 0; trial < 40; trial++)
        {
            int objectCount = 3 + trial % 9;
            var tolerance = new FuzzyToleranceMatrix(objectCount);
            for (int row = 0; row < objectCount; row++)
                for (int column = row + 1; column < objectCount; column++)
                    tolerance[row, column] = System.Math.Round(random.NextDouble(), trial % 2 == 0 ? 1 : 3);

            var approximation = new TAGAProcessor(Operator(operatorName)).Apply(tolerance);

            for (int first = 0; first < objectCount; first++)
                for (int second = 0; second < objectCount; second++)
                    for (int third = 0; third < objectCount; third++)
                        Assert.True(
                            approximation[first, third] >= System.Math.Min(approximation[first, second], approximation[second, third]) - 1e-9,
                            $"{operatorName}: not transitive at ({first},{second},{third}) in trial {trial}");
        }
    }

    /// <summary>
    /// T of the Sneath–Sokal data computed with the plain loop of the 2013 paper and no merging
    /// of levels: two mathematically equal similarities then differ in the last bit, which is
    /// the arithmetic the published table was computed in.
    /// </summary>
    private static FuzzyToleranceMatrix SneathSokalAsPublished()
    {
        var data = BookExampleTests.SneathSokal();
        int objectCount = data.Rows, attributeCount = data.Cols;
        var normalized = new double[objectCount, attributeCount];
        for (int attribute = 0; attribute < attributeCount; attribute++)
        {
            double largest = Enumerable.Range(0, objectCount).Max(row => data[row, attribute]);
            for (int row = 0; row < objectCount; row++)
                normalized[row, attribute] = data[row, attribute] / largest;
        }

        var tolerance = new double[objectCount, objectCount];
        for (int first = 0; first < objectCount; first++)
            for (int second = 0; second < objectCount; second++)
            {
                double sum = 0.0;
                for (int attribute = 0; attribute < attributeCount; attribute++)
                {
                    double difference = normalized[first, attribute] - normalized[second, attribute];
                    sum = sum + difference * difference;
                }
                tolerance[first, second] = 1.0 - sum / attributeCount;
            }
        return FuzzyToleranceMatrix.FromArray(tolerance);
    }

    [Theory]
    // operator, distance and threshold of Table 1 of the paper (five printed decimals, truncated), sizes of the two clusters
    [InlineData("max", 39.43923, 0.96875, 13, 3)]
    [InlineData("min", 76.63888, 0.64496, 8, 8)]
    [InlineData("mean", 21.03686, 0.81689, 8, 8)]
    [InlineData("median", 23.42621, 0.82248, 8, 8)]
    [InlineData("umedian", 29.02951, 0.93663, 8, 8)]
    [InlineData("dmedian", 31.55729, 0.76996, 5, 11)]
    public void SneathSokal_ExactComparison_ReproducesThePublishedTable(
        string operatorName, double distance, double threshold, int firstSize, int secondSize)
    {
        var result = new TAGAProcessor(Operator(operatorName), tieTolerance: 0.0).ApplyWithDetails(SneathSokalAsPublished());
        double alpha = LeapHeuristicAlphaSelector.SelectFrom(result.Approximation.GetDistinctValues(), LeapForm.Ratio)!.Value;

        static double Truncate(double value) => System.Math.Floor(value * 1e5) / 1e5;
        Assert.Equal(distance, Truncate(result.Distance), precision: 9);
        Assert.Equal(threshold, Truncate(alpha), precision: 9);

        int firstClass = Enumerable.Range(0, 16).Count(member => result.Approximation[0, member] >= alpha);
        Assert.Equal(firstSize, firstClass);
        Assert.Equal(secondSize, 16 - firstClass);
    }

    [Fact]
    public void SneathSokal_ExactComparison_ChoosesTheMeanAutomatically()
    {
        var chosen = new TAGAProcessor(null, tieTolerance: 0.0).ApplyWithDetails(SneathSokalAsPublished());
        Assert.Same(MeanTAGA.Instance, chosen.Operator);
    }

    [Theory]
    // with equal similarities recognised as equal: distance, threshold, sizes, typical points (1-based), F1
    [InlineData("max", 39.43923611, 0.96875000, new[] { 13, 3 }, new[] { 6, 10 }, 0.02955840)]
    [InlineData("min", 77.52256944, 0.64496528, new[] { 8, 8 }, new[] { 2, 14 }, 0.35796441)]
    [InlineData("mean", 21.09806713, 0.81689815, new[] { 8, 8 }, new[] { 2, 14 }, 0.20553711)]
    [InlineData("median", 24.76215278, 0.88845486, new[] { 11, 5 }, new[] { 5, 14 }, 0.12893782)]
    [InlineData("umedian", 25.15625000, 0.93663194, new[] { 8, 8 }, new[] { 6, 14 }, 0.05631510)]
    [InlineData("dmedian", 23.53645833, 0.86111111, new[] { 11, 5 }, new[] { 5, 14 }, 0.15318813)]
    public void SneathSokal_TiesRecognised(
        string operatorName, double distance, double threshold, int[] sizes, int[] typicalPoints, double criterionValue)
    {
        var builder = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);
        var data = BookExampleTests.SneathSokal();
        var processor = new TAGAProcessor(Operator(operatorName));

        var details = processor.ApplyWithDetails(builder.Build(data));
        var best = new DAfcTagaAlgorithm(builder, processor, F1Criterion.Instance)
            .Run(data, TestContext.Current.CancellationToken)[0];

        Assert.Equal(distance, details.Distance, precision: 8);
        Assert.Equal(threshold, best.Alpha, precision: 8);
        Assert.Equal(sizes, best.MemberSets.Select(members => members.Length).ToArray());
        Assert.Equal(typicalPoints, best.Centers.Select(center => center + 1).ToArray());
        Assert.Equal(criterionValue, best.Score, precision: 8);
    }

    [Theory]
    // D-AFC-TAGA(α) with the mean: least threshold → selected level, number of clusters
    [InlineData(0.5, 0.81689815, 2)]
    [InlineData(0.85, 0.93164062, 4)]
    [InlineData(0.9, 0.98611111, 12)]
    public void SneathSokal_LeastThreshold(double leastAlpha, double threshold, int clusterCount)
    {
        var builder = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);
        var best = new DAfcTagaAlgorithm(builder, new TAGAProcessor(MeanTAGA.Instance), F1Criterion.Instance, leastAlpha: leastAlpha)
            .Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken)[0];

        Assert.Equal(threshold, best.Alpha, precision: 8);
        Assert.Equal(clusterCount, best.ClusterCount);
    }

    [Fact]
    public void LeapHeuristic_RatioAndDifferenceForms_CanDisagree()
    {
        // levels of the mean approximation of the book example: 0.1, 0.2, 0.5, 0.9
        double[] descending = [0.9, 0.5, 0.2, 0.1];

        Assert.Equal(0.9, LeapHeuristicAlphaSelector.SelectFrom(descending, LeapForm.Difference));
        Assert.Equal(0.5, LeapHeuristicAlphaSelector.SelectFrom(descending, LeapForm.Ratio));   // ratios 0.5, 0.4, 0.556
    }

    [Fact]
    public void Factory_RunsTheTagaFamily()
    {
        var data = BookExampleTests.SneathSokal();
        var options = new AlgorithmOptions(Distance: "squared-euclidean", Normalization: "max");
        var ct = TestContext.Current.CancellationToken;

        var automatic = AlgorithmFactory.Run("dafc-taga", data, options, ct)[0];
        var mean = AlgorithmFactory.Run("dafc-taga", data, options with { TagaOperator = "mean" }, ct)[0];
        var principal = AlgorithmFactory.Run("dpafc-taga", data, options with { TagaOperator = "max" }, ct)[0];
        var closurePrincipal = AlgorithmFactory.Run("dpafc-tc", data, options, ct)[0];
        var hierarchy = AlgorithmFactory.Run("hafc-taga", data, options with { TagaOperator = "mean" }, ct);

        Assert.Equal(mean.Alpha, automatic.Alpha);                 // the mean is the nearest approximation
        Assert.Equal(closurePrincipal.Alpha, principal.Alpha);     // maximum = transitive closure
        Assert.Equal(closurePrincipal.Centers, principal.Centers);
        Assert.Contains(hierarchy, level => level.Alpha == mean.Alpha && level.Centers.SequenceEqual(mean.Centers));
        for (int index = 1; index < hierarchy.Count; index++)
            Assert.True(hierarchy[index].ClusterCount >= hierarchy[index - 1].ClusterCount);
    }
}
