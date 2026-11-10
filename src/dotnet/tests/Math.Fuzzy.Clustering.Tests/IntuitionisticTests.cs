using MathFuzzy.Clustering.Algorithms.Intuitionistic;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Intuitionistic;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// The intuitionistic algorithms against the published examples: Hung's ten-object relation
/// (Viattchenin 2010; 2013, sec. 3.1; Kacprzyk et al. 2016), the seven-object relation of the
/// 2010 paper, and the car data of Viattchenin and Shyrai (2015). Expected criterion values are
/// exact fractions recomputed from the printed data.
/// </summary>
public class IntuitionisticTests
{
    /// <summary>A relation from its lower triangle of (μ, ν) pairs; the diagonal is (1, 0).</summary>
    internal static IntuitionisticToleranceMatrix Relation(double[][] lowerTriangle)
    {
        int size = lowerTriangle.Length + 1;
        var membership = new double[size, size];
        var nonMembership = new double[size, size];
        for (int row = 1; row < size; row++)
            for (int column = 0; column < row; column++)
            {
                membership[row, column] = membership[column, row] = lowerTriangle[row - 1][2 * column];
                nonMembership[row, column] = nonMembership[column, row] = lowerTriangle[row - 1][2 * column + 1];
            }
        return new IntuitionisticToleranceMatrix(membership, nonMembership);
    }

    /// <summary>Hung, Lee and Fuh's intuitionistic fuzzy tolerance on ten objects.</summary>
    internal static IntuitionisticToleranceMatrix Hung() => Relation(
    [
        [0.2, 0.7],
        [0.5, 0.5, 0.3, 0.6],
        [0.8, 0.1, 0.6, 0.4, 0.5, 0.4],
        [0.6, 0.3, 0.7, 0.2, 0.3, 0.6, 0.7, 0.2],
        [0.2, 0.7, 0.9, 0.1, 0.4, 0.5, 0.3, 0.6, 0.2, 0.7],
        [0.3, 0.7, 0.2, 0.7, 0.1, 0.9, 0.5, 0.4, 0.4, 0.5, 0.1, 0.7],
        [0.9, 0.1, 0.8, 0.2, 0.3, 0.6, 0.4, 0.6, 0.5, 0.5, 0.3, 0.7, 0.6, 0.3],
        [0.4, 0.5, 0.3, 0.7, 0.7, 0.2, 0.1, 0.8, 0.8, 0.1, 0.7, 0.2, 0.1, 0.8, 0.0, 0.9],
        [0.3, 0.7, 0.2, 0.7, 0.6, 0.3, 0.3, 0.7, 0.9, 0.1, 0.2, 0.7, 0.3, 0.7, 0.2, 0.8, 0.1, 0.8],
    ]);

    private static IntuitionisticToleranceMatrix SevenObjects() => Relation(
    [
        [0.75, 0.20],
        [0.25, 0.70, 0.50, 0.50],
        [0.00, 0.90, 0.25, 0.70, 0.75, 0.20],
        [0.25, 0.70, 0.50, 0.50, 0.50, 0.50, 0.75, 0.20],
        [0.25, 0.70, 0.00, 0.90, 0.50, 0.50, 0.75, 0.20, 0.50, 0.50],
        [0.50, 0.50, 0.75, 0.20, 0.75, 0.20, 0.50, 0.50, 0.75, 0.20, 0.25, 0.70],
    ]);

    /// <summary>Intuitionistic attribute data from rows of (μ, ν) pairs.</summary>
    internal static IntuitionisticDataMatrix Data(double[][] rows)
    {
        int objects = rows.Length, attributes = rows[0].Length / 2;
        var membership = new double[objects, attributes];
        var nonMembership = new double[objects, attributes];
        for (int row = 0; row < objects; row++)
            for (int attribute = 0; attribute < attributes; attribute++)
            {
                membership[row, attribute] = rows[row][2 * attribute];
                nonMembership[row, attribute] = rows[row][2 * attribute + 1];
            }
        return new IntuitionisticDataMatrix(membership, nonMembership);
    }

    internal static IntuitionisticDataMatrix FiveCars() => Data(
    [
        [0.3, 0.5, 0.6, 0.1, 0.4, 0.3, 0.8, 0.1, 0.1, 0.6, 0.5, 0.4],
        [0.6, 0.3, 0.5, 0.2, 0.6, 0.1, 0.7, 0.1, 0.3, 0.6, 0.4, 0.3],
        [0.4, 0.4, 0.8, 0.1, 0.5, 0.1, 0.6, 0.2, 0.4, 0.5, 0.3, 0.2],
        [0.2, 0.4, 0.4, 0.1, 0.9, 0.0, 0.8, 0.1, 0.2, 0.5, 0.7, 0.1],
        [0.5, 0.2, 0.3, 0.6, 0.6, 0.3, 0.7, 0.1, 0.6, 0.2, 0.5, 0.3],
    ]);

    internal static IntuitionisticDataMatrix TenCars() => Data(
    [
        [0.8, 0.1, 0.4, 0.1, 0.6, 0.1, 0.7, 0.3, 0.6, 0.2, 0.5, 0.0],
        [0.0, 0.3, 0.1, 0.3, 0.0, 0.6, 0.0, 0.5, 0.5, 0.3, 0.4, 0.2],
        [0.2, 0.0, 0.9, 0.1, 0.0, 0.7, 0.0, 0.1, 0.3, 0.2, 0.8, 0.2],
        [0.0, 0.5, 0.3, 0.0, 0.7, 0.1, 0.6, 0.1, 0.0, 0.7, 0.7, 0.2],
        [0.4, 0.6, 0.2, 0.4, 0.9, 0.1, 0.6, 0.1, 0.7, 0.2, 0.7, 0.3],
        [0.0, 0.2, 0.0, 0.0, 0.5, 0.4, 0.5, 0.4, 0.3, 0.6, 0.0, 0.0],
        [0.8, 0.1, 0.2, 0.1, 0.1, 0.0, 0.7, 0.0, 0.6, 0.4, 0.0, 0.6],
        [0.1, 0.7, 0.0, 0.5, 0.8, 0.1, 0.7, 0.1, 0.7, 0.1, 0.0, 0.0],
        [0.0, 0.1, 0.5, 0.1, 0.3, 0.1, 0.7, 0.3, 0.1, 0.3, 0.7, 0.2],
        [0.3, 0.2, 0.7, 0.1, 0.2, 0.2, 0.2, 0.0, 0.1, 0.9, 0.9, 0.1],
    ]);

    private static int[] OneBased(int[] indices) => [.. indices.Select(index => index + 1)];

    private static IntuitionisticOptions With(ThresholdScanOrder order) => new(ScanOrder: order);

    [Fact]
    public void ThresholdSequences_OfTheThreeObjectExample()
    {
        var relation = Relation([[0.80, 0.20], [0.70, 0.25, 0.20, 0.75]]);

        Assert.Equal([0.20, 0.70, 0.80, 1.00], relation.AlphaSequence());
        Assert.Equal([0.75, 0.25, 0.20, 0.00], relation.BetaSequence());
    }

    [Theory]
    [InlineData(ThresholdScanOrder.BetaInner)]
    [InlineData(ThresholdScanOrder.BetaOuter)]
    public void DPaifc_ThreeObjects_TwoTiedAllotments(ThresholdScanOrder order)
    {
        var relation = Relation([[0.80, 0.20], [0.70, 0.25, 0.20, 0.75]]);

        var found = new DPaifcAlgorithm(With(order)).Run(relation, TestContext.Current.CancellationToken);

        Assert.Equal(2, found.Count);
        Assert.All(found, result =>
        {
            Assert.Equal(0.20, result.Alpha);
            Assert.Equal(0.20, result.Beta);
            Assert.Equal(2, result.ClusterCount);
            Assert.Equal(9.0 / 5.0, result.Score, precision: 12);
        });
        Assert.Equal(found[0].Score, found[1].Score);   // a tie is visible as equal values
    }

    [Fact]
    public void DPaifc_Hung_BetaOuter_ReproducesThePublishedAllotments()
    {
        var found = new DPaifcAlgorithm(With(ThresholdScanOrder.BetaOuter)).Run(Hung(), TestContext.Current.CancellationToken);

        Assert.Equal(2, found.Count);
        Assert.Equal((0.8, 0.2), (found[0].Alpha, found[0].Beta!.Value));
        Assert.Equal([1, 3, 5, 6, 7], OneBased(found[0].Centers));
        Assert.Equal(47.0 / 30.0, found[0].Score, precision: 12);
        Assert.Equal([2, 3, 4, 5, 7], OneBased(found[1].Centers));
        Assert.Equal(89.0 / 60.0, found[1].Score, precision: 12);

        // cluster of x1: x1 (1, 0), x4 (0.8, 0.1), x8 (0.9, 0.1)
        Assert.Equal([0, 3, 7], found[0].MemberSets[0]);
        Assert.Equal(0.8, found[0].Membership[3, 0]);
        Assert.Equal(0.1, found[0].NonMembership![3, 0]);
        Assert.Equal(0.9, found[0].Membership[7, 0]);
    }

    [Fact]
    public void DPaifc_Hung_BetaInner_GivesTheFirstPairOfThePrintedOrder()
    {
        var found = new DPaifcAlgorithm(With(ThresholdScanOrder.BetaInner)).Run(Hung(), TestContext.Current.CancellationToken);

        Assert.Equal(2, found.Count);
        Assert.All(found, result =>
        {
            Assert.Equal((0.1, 0.1), (result.Alpha, result.Beta!.Value));
            Assert.Equal(5, result.ClusterCount);
            Assert.Equal(137.0 / 30.0, result.Score, precision: 12);
        });
    }

    [Theory]
    [InlineData(ThresholdScanOrder.BetaInner)]
    [InlineData(ThresholdScanOrder.BetaOuter)]
    public void DPaifc_SevenObjects(ThresholdScanOrder order)
    {
        var found = new DPaifcAlgorithm(With(order)).Run(SevenObjects(), TestContext.Current.CancellationToken);

        var best = Assert.Single(found);
        Assert.Equal((0.25, 0.5), (best.Alpha, best.Beta!.Value));
        Assert.Equal([1, 6], OneBased(best.Centers));
        Assert.Equal([[0, 1, 6], [2, 3, 4, 5]], best.MemberSets);
        Assert.Equal(337.0 / 240.0, best.Score, precision: 12);
    }

    [Theory]
    // c, α, β, typical points (1-based), F, number of allotments
    [InlineData(2, 0.1, 0.5, new[] { 3, 8 }, 41.0 / 25.0, 1)]
    [InlineData(3, 0.4, 0.6, new[] { 6, 8, 10 }, 34.0 / 15.0, 1)]
    [InlineData(4, 0.1, 0.3, new[] { 4, 6, 8, 10 }, 147.0 / 40.0, 7)]
    public void DAifc_Hung_ReproducesThePublishedResults(
        int clusterCount, double alpha, double beta, int[] typicalPoints, double criterionValue, int allotments)
    {
        var found = new DAifcAlgorithm(clusterCount).Run(Hung(), TestContext.Current.CancellationToken);

        Assert.Equal(allotments, found.Count);
        Assert.Equal((alpha, beta), (found[0].Alpha, found[0].Beta!.Value));
        Assert.Equal(typicalPoints, OneBased(found[0].Centers));
        Assert.Equal(criterionValue, found[0].Score, precision: 12);
        Assert.Equal(1, found[0].IntersectionBound);   // no fully separated allotment exists
    }

    [Fact]
    public void DAifc_ClusterCountOutOfRange_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DAifcAlgorithm(10).Run(Hung(), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new DAifcAlgorithm(1).Run(Hung(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HammingMeasure_FiveCars_GivesThePublishedTolerance()
    {
        var tolerance = IntuitionisticSimilarity.BuildTolerance(FiveCars(), IntuitionisticMeasure.Hamming);

        Assert.Equal(4.0 / 5.0, tolerance.Membership(1, 0), precision: 12);
        Assert.Equal(1.0 / 10.0, tolerance.NonMembership(1, 0), precision: 12);
        Assert.Equal(43.0 / 60.0, tolerance.Membership(2, 0), precision: 12);
        Assert.Equal(7.0 / 60.0, tolerance.NonMembership(2, 0), precision: 12);
        Assert.Equal(19.0 / 30.0, tolerance.Membership(4, 3), precision: 12);
        Assert.Equal(1.0 / 4.0, tolerance.NonMembership(4, 3), precision: 12);
    }

    [Fact]
    public void EuclideanMeasure_FiveCars_FirstPair()
    {
        var tolerance = IntuitionisticSimilarity.BuildTolerance(FiveCars(), IntuitionisticMeasure.Euclidean);

        Assert.Equal(0.912732, tolerance.Membership(1, 0), precision: 6);
        Assert.Equal(0.05, tolerance.NonMembership(1, 0), precision: 12);
    }

    [Fact]
    public void TransitiveClosure_FiveCars()
    {
        var closure = IntuitionisticSimilarity.TransitiveClosure(
            IntuitionisticSimilarity.BuildTolerance(FiveCars(), IntuitionisticMeasure.Hamming));

        Assert.Equal(4.0 / 5.0, closure.Membership(2, 0), precision: 12);     // raised from 43/60
        Assert.Equal(1.0 / 10.0, closure.NonMembership(2, 0), precision: 12); // lowered from 7/60
        Assert.Equal(3.0 / 4.0, closure.Membership(3, 2), precision: 12);
        Assert.Equal(1.0 / 20.0, closure.NonMembership(3, 2), precision: 12);
        for (int other = 0; other < 4; other++)
        {
            Assert.Equal(41.0 / 60.0, closure.Membership(4, other), precision: 12);
            Assert.Equal(11.0 / 60.0, closure.NonMembership(4, other), precision: 12);
        }
        Assert.Equal(5, closure.AlphaSequence().Count);
        Assert.Equal(5, closure.BetaSequence().Count);
    }

    [Fact]
    public void DPaifcTc_FiveCars_ReproducesThePublishedPartition()
    {
        var found = new DPaifcTcAlgorithm().Run(FiveCars(), TestContext.Current.CancellationToken);

        Assert.Equal(4, found.Count);
        var best = found[0];
        Assert.Equal(41.0 / 60.0, best.Alpha, precision: 12);
        Assert.Equal(1.0 / 10.0, best.Beta!.Value, precision: 12);
        Assert.Equal([3, 5], OneBased(best.Centers));
        Assert.Equal([[0, 1, 2, 3], [4]], best.MemberSets);
        Assert.Equal(37.0 / 60.0, best.Score, precision: 12);
        Assert.Equal(4.0 / 5.0, best.Membership[0, 0], precision: 12);   // x1 in the cluster of x3
        Assert.Equal(1.0 / 10.0, best.NonMembership![0, 0], precision: 12);
    }

    [Fact]
    public void DPaifcTc_TenCars_PrototypesResolveTheTie()
    {
        var found = new DPaifcTcAlgorithm(options: new IntuitionisticOptions(MaxResults: 100))
            .Run(TenCars(), TestContext.Current.CancellationToken);

        Assert.Equal(20, found.Count);
        var best = found[0];
        Assert.Equal(29.0 / 60.0, best.Alpha, precision: 12);
        Assert.Equal(1.0 / 6.0, best.Beta!.Value, precision: 12);
        Assert.Equal(4, best.ClusterCount);
        Assert.Equal(1.73, best.Score, precision: 12);
        Assert.Equal(8, found.Count(result => result.Score == best.Score));   // eight allotments share the largest value
        Assert.Equal(new[] { "0,2,5,6,8", "1", "3,9", "4,7" },
                     best.MemberSets.Select(members => string.Join(",", members)).Order());
        Assert.Contains(0, best.Centers);   // x1 is nearest to the prototype of the large class
    }

    [Fact]
    public void DPaifc_RoundedFiveCarTable()
    {
        // the published table of the five-car tolerance, two decimals
        var relation = Relation(
        [
            [0.80, 0.10],
            [0.72, 0.12, 0.82, 0.08],
            [0.75, 0.13, 0.72, 0.10, 0.70, 0.05],
            [0.65, 0.22, 0.68, 0.18, 0.63, 0.23, 0.63, 0.25],
        ]);

        var principal = new DPaifcAlgorithm().Run(relation, TestContext.Current.CancellationToken);
        var three = new DAifcAlgorithm(3).Run(relation, TestContext.Current.CancellationToken);

        Assert.Equal((0.63, 0.13), (principal[0].Alpha, principal[0].Beta!.Value));
        Assert.Equal([2, 5], OneBased(principal[0].Centers));
        Assert.Equal(0.765, principal[0].Score, precision: 12);

        Assert.Equal((0.63, 0.08), (three[0].Alpha, three[0].Beta!.Value));
        Assert.Equal(0, three[0].IntersectionBound);
        Assert.Equal([1, 3, 5], OneBased(three[0].Centers));
        Assert.Equal(1.146667, three[0].Score, precision: 6);
    }

    [Fact]
    public void Relation_WithMembershipPlusNonMembershipAboveOne_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Relation([[0.8, 0.3]]));
    }

    // ---- the order-free definition: all admissible pairs, least number of clusters, quality Q ----

    private static (IReadOnlyList<ClusterResult> Found, SearchDiagnostics Diagnostics) OrderFree(IntuitionisticToleranceMatrix relation)
    {
        var diagnostics = new SearchDiagnostics();
        var found = new DPaifcAlgorithm(new IntuitionisticOptions(ThresholdScanOrder.OrderFree, Diagnostics: diagnostics))
            .Run(relation, TestContext.Current.CancellationToken);
        return (found, diagnostics);
    }

    private static (double Alpha, double Beta, int Count)[] Frontier(SearchDiagnostics diagnostics) =>
        [.. diagnostics.AdmissiblePairs.Where(pair => pair.OnFrontier).OrderBy(pair => pair.Alpha)
            .Select(pair => (System.Math.Round(pair.Alpha, 4), System.Math.Round(pair.Beta, 4), pair.ClusterCount))];

    [Fact]
    public void OrderFree_Hung_SameClassesAsBothScans_OwnThresholds()
    {
        var (found, diagnostics) = OrderFree(Hung());

        Assert.Equal(3, found.Count);
        Assert.Equal(5, found[0].ClusterCount);
        Assert.Equal((0.8, 0.1), (found[0].Alpha, found[0].Beta!.Value));
        Assert.Equal(137.0 / 30.0, found[0].Score, precision: 12);
        Assert.Equal("Q_IFS", found[0].CriterionName);
        Assert.Equal(new[] { "0,3,7", "1,5", "2", "4,8,9", "6" },
                     found[0].MemberSets.Select(set => string.Join(',', set)).Order(StringComparer.Ordinal));
        // the two scan orders stop at the two ends of the frontier
        Assert.Equal([(0.1, 0.1, 5), (0.8, 0.2, 5)], Frontier(diagnostics));
        // the class of two objects has two equally good typical points
        Assert.Contains("the clusters are the same", ClusterResult.DescribeTie(found));
    }

    [Fact]
    public void OrderFree_SevenObjects_SameClasses_BetterTypicalPoints()
    {
        var (found, diagnostics) = OrderFree(SevenObjects());

        Assert.Equal([(0.25, 0.5, 2)], Frontier(diagnostics));
        Assert.Equal([[0, 1, 6], [2, 3, 4, 5]], found[0].MemberSets);
        Assert.Equal([2, 4], OneBased(found[0].Centers));          // the scans return x1, x6 at (0.25, 0.5)
        Assert.Equal((0.75, 0.2), (found[0].Alpha, found[0].Beta!.Value));
        Assert.Equal(1.3625, found[0].Score, precision: 12);
    }

    [Fact]
    public void OrderFree_FiveCars_PublishedTypicalPoints()
    {
        var (found, diagnostics) = OrderFree(IntuitionisticSimilarity.BuildTolerance(FiveCars(), IntuitionisticMeasure.Hamming));

        Assert.Equal([2, 5], OneBased(found[0].Centers));
        Assert.Equal(1.7625, found[0].Score, precision: 10);
        Assert.Equal(0.716667, found[0].Alpha, precision: 6);
        Assert.Equal(0.1, found[0].Beta!.Value, precision: 12);
        Assert.Equal([(0.6333, 0.1333, 2), (0.7, 0.25, 2)], Frontier(diagnostics));
    }

    [Fact]
    public void OrderFree_TenCars_FewerClustersThanThePrintedScan()
    {
        var tolerance = IntuitionisticSimilarity.BuildTolerance(TenCars(), IntuitionisticMeasure.Hamming);
        var printed = new DPaifcAlgorithm().Run(tolerance, TestContext.Current.CancellationToken);
        var (found, diagnostics) = OrderFree(tolerance);

        Assert.Equal(6, printed[0].ClusterCount);                  // the first pair of the printed order
        Assert.Equal(4, found[0].ClusterCount);                    // the least number of clusters
        Assert.Equal([1, 3, 6, 10], OneBased(found[0].Centers));
        Assert.Equal(3.194444, found[0].Score, precision: 6);
        Assert.Equal([6, 5, 4, 4], Frontier(diagnostics).Select(pair => pair.Count));
        // the result lists the least number first, then the larger numbers of the frontier
        Assert.Equal([4, 5, 6], found.Select(result => result.ClusterCount).Distinct());
    }

    [Fact]
    public void OrderFree_TenCarsClosure_TwoClusters_PublishedFourOnTheFrontier()
    {
        var diagnostics = new SearchDiagnostics();
        var found = new DPaifcTcAlgorithm(options: new IntuitionisticOptions(ThresholdScanOrder.OrderFree, Diagnostics: diagnostics))
            .Run(TenCars(), TestContext.Current.CancellationToken);

        Assert.Equal(2, found[0].ClusterCount);
        Assert.Equal([1, 9], found[0].MemberSets.Select(set => set.Length).Order());
        Assert.Equal([(0.4833, 0.1667, 4), (0.5167, 0.1833, 2)], Frontier(diagnostics));

        var four = found.First(result => result.ClusterCount == 4);
        Assert.Equal(new[] { "0,2,5,6,8", "1", "3,9", "4,7" },
                     four.MemberSets.Select(set => string.Join(',', set)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OrderFree_IsNotDefinedForAGivenNumberOfClusters()
    {
        Assert.Throws<ArgumentException>(() =>
            new DAifcAlgorithm(3, new IntuitionisticOptions(ThresholdScanOrder.OrderFree))
                .Run(Hung(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The statements behind the order-free definition, on random relations: the number of clusters
    /// does not grow when the pair becomes looser; the printed scan stops at the frontier pair with
    /// the least α and the opposite scan at the one with the largest β; the least number of clusters
    /// is attained on the frontier; an allotment exists at its own thresholds.
    /// </summary>
    [Fact]
    public void OrderFree_RandomRelations_MonotoneCount_ScansStopOnTheFrontier()
    {
        var random = new Random(20261110);
        int withAllotments = 0, scanAboveLeast = 0;

        for (int trial = 0; trial < 300; trial++)
        {
            const int size = 7;
            var lowerTriangle = new double[size - 1][];
            for (int row = 1; row < size; row++)
            {
                lowerTriangle[row - 1] = new double[2 * row];
                for (int column = 0; column < row; column++)
                {
                    int membershipTenths = random.Next(0, 11);
                    int nonMembershipTenths = random.Next(0, 11 - membershipTenths);
                    lowerTriangle[row - 1][2 * column] = membershipTenths / 10.0;
                    lowerTriangle[row - 1][2 * column + 1] = nonMembershipTenths / 10.0;
                }
            }
            var relation = Relation(lowerTriangle);

            var (found, diagnostics) = OrderFree(relation);
            var inner = new DPaifcAlgorithm(With(ThresholdScanOrder.BetaInner)).Run(relation, TestContext.Current.CancellationToken);
            var outer = new DPaifcAlgorithm(With(ThresholdScanOrder.BetaOuter)).Run(relation, TestContext.Current.CancellationToken);
            var pairs = diagnostics.AdmissiblePairs;

            if (found.Count == 0)
            {
                Assert.Empty(inner);
                Assert.Empty(outer);
                continue;
            }
            withAllotments++;

            foreach (var looser in pairs)
                foreach (var stricter in pairs)
                    if (looser.Alpha <= stricter.Alpha && looser.Beta >= stricter.Beta)
                        Assert.True(looser.ClusterCount <= stricter.ClusterCount);

            var frontier = pairs.Where(pair => pair.OnFrontier).ToList();
            var leastAlpha = frontier.MinBy(pair => pair.Alpha)!;
            var largestBeta = frontier.MaxBy(pair => pair.Beta)!;
            Assert.Equal((leastAlpha.Alpha, leastAlpha.Beta, leastAlpha.ClusterCount),
                         (inner[0].Alpha, inner[0].Beta!.Value, inner[0].ClusterCount));
            Assert.Equal((largestBeta.Alpha, largestBeta.Beta, largestBeta.ClusterCount),
                         (outer[0].Alpha, outer[0].Beta!.Value, outer[0].ClusterCount));

            int least = pairs.Min(pair => pair.ClusterCount);
            Assert.Equal(least, found[0].ClusterCount);
            Assert.Equal(least, frontier.Min(pair => pair.ClusterCount));
            if (inner[0].ClusterCount > least || outer[0].ClusterCount > least) scanAboveLeast++;

            // the allotment exists at its own thresholds, with the same number of clusters
            Assert.Contains(pairs, pair => pair.Alpha == found[0].Alpha && pair.Beta == found[0].Beta!.Value
                                           && pair.ClusterCount == found[0].ClusterCount);
        }

        Assert.True(withAllotments >= 30, $"only {withAllotments} relations had allotments");
        Assert.True(scanAboveLeast > 0, "no relation on which a scan returns more than the least number of clusters");
    }

    [Fact]
    public void TieDescription_TellsEqualClustersFromDifferentAllotments()
    {
        static ClusterResult Result(int[] centers, int[][] members, double score) =>
            new(centers.Length, 0.5, centers, members, new double[4, centers.Length], score, "F_IFS");

        var sameClusters = new[]
        {
            Result([0, 2], [[0, 1], [2, 3]], 1.0),
            Result([1, 3], [[1, 0], [3, 2]], 1.0)
        };
        var otherClusters = new[]
        {
            Result([0, 2], [[0, 1], [2, 3]], 1.0),
            Result([0, 3], [[0, 1, 2], [3]], 1.0)
        };
        var unique = new[]
        {
            Result([0, 2], [[0, 1], [2, 3]], 1.0),
            Result([0, 3], [[0, 1, 2], [3]], 0.9)
        };

        Assert.True(sameClusters[0].HasSameClusters(sameClusters[1]));
        Assert.False(otherClusters[0].HasSameClusters(otherClusters[1]));
        Assert.Contains("the clusters are the same", ClusterResult.DescribeTie(sameClusters));
        Assert.Contains("not unique", ClusterResult.DescribeTie(otherClusters));
        Assert.Null(ClusterResult.DescribeTie(unique));
    }

    [Fact]
    public void Search_NodeBudget_IsReported()
    {
        var diagnostics = new SearchDiagnostics();
        new DAifcAlgorithm(4, new IntuitionisticOptions(MaxSearchNodes: 3, Diagnostics: diagnostics))
            .Run(Hung(), TestContext.Current.CancellationToken);

        Assert.True(diagnostics.BudgetExhausted);
    }
}
