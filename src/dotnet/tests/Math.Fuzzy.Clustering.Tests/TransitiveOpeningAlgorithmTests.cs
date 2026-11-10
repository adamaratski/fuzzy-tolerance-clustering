using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// D-AFC-TO, D-AFC-TO(α) and D-PAFC-TO (Viattchenin and Tati 2013) on the Sneath–Sokal example of
/// the book (sec. opening-example) and in comparison with the closure and the TAGA family.
/// </summary>
public class TransitiveOpeningAlgorithmTests
{
    private static ToleranceMatrixBuilder Builder()
        => new(SquaredEuclideanDistance.Instance, MaxNormalization.Instance);

    private static DAfcToAlgorithm DAfcTo(
        TransitiveOpeningMethod method = TransitiveOpeningMethod.CompleteLinkage,
        double? leastAlpha = null, LeapForm leapForm = LeapForm.Ratio)
        => new(Builder(), new TransitiveOpening(method), F1Criterion.Instance, leastAlpha: leastAlpha, leapForm: leapForm);

    private static int[] Range(int firstOneBased, int lastOneBased)
        => [.. Enumerable.Range(firstOneBased - 1, lastOneBased - firstOneBased + 1)];

    private const double SelectedThreshold = 743.0 / 1152;   // 0.644965, the value reported in the source

    [Fact]
    public void SneathSokal_DAfcTo_ReproducesThePublishedThresholdAndClasses()
    {
        var best = DAfcTo().Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken)[0];

        Assert.Equal(SelectedThreshold, best.Alpha, precision: 12);
        Assert.Equal(2, best.ClusterCount);
        Assert.Equal(Range(1, 8), best.MemberSets[0].Order().ToArray());
        Assert.Equal(Range(9, 16), best.MemberSets[1].Order().ToArray());
    }

    [Fact]
    public void SneathSokal_DAfcTo_TypicalPointsAndCriterion()
    {
        var best = DAfcTo().Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken)[0];

        // x2 as published (tie with x1 at 0.83843, resolved by the prototype rule). The source
        // reports x13 for the second cluster; the book recomputes x14 (contribution 0.82802
        // against 0.81576 for x13, tie with x15 resolved by the prototype rule).
        Assert.Equal([2, 14], best.Centers.Select(center => center + 1).ToArray());
        Assert.Equal(1735.0 / 4608, best.Score, precision: 12);   // F1 = 0.37652

        // membership function of the first cluster: Figure 2 of the source
        double[] firstCluster = [0.98611, 1, 0.93663, 0.91319, 0.91319, 0.65278, 0.65278, 0.65278];
        for (int objectIndex = 0; objectIndex < 8; objectIndex++)
            Assert.Equal(firstCluster[objectIndex], best.Membership[objectIndex, 0], precision: 5);
    }

    [Fact]
    public void SneathSokal_PublishedTypicalPointX13_HasTheSmallerCriterion()
    {
        var ranked = DAfcTo().Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken);
        var withThirteenth = ranked.First(result => result.Centers.SequenceEqual([1, 12]));

        // book: with x13 the allotment has F1 = 0.36426, with x14 it has 0.37652
        Assert.Equal(0.36426, withThirteenth.Score, precision: 5);
        Assert.True(withThirteenth.Score < ranked[0].Score);

        // Figure 2 of the source: the memberships of the second cluster are the row of x13
        double[] rowOfThirteenth = [0.64497, 0.64497, 0.64497, 0.96875, 1, 0.87413, 0.87413, 0.87413];
        for (int position = 0; position < 8; position++)
            Assert.Equal(rowOfThirteenth[position], withThirteenth.Membership[8 + position, 1], precision: 5);
    }

    [Fact]
    public void SneathSokal_RatioAndDifferenceLeaps_SelectTheSameLevel()
    {
        var data = BookExampleTests.SneathSokal();
        var ct = TestContext.Current.CancellationToken;
        var ratio = DAfcTo(leapForm: LeapForm.Ratio).Run(data, ct)[0];
        var difference = DAfcTo(leapForm: LeapForm.Difference).Run(data, ct)[0];

        Assert.Equal(SelectedThreshold, ratio.Alpha, precision: 12);
        Assert.Equal(ratio.Alpha, difference.Alpha);
        Assert.Equal(ratio.Centers, difference.Centers);
    }

    [Fact]
    public void SneathSokal_DPafcTo_ReturnsTheSameAllotment()
    {
        var data = BookExampleTests.SneathSokal();
        var ct = TestContext.Current.CancellationToken;
        var principal = new DPafcToAlgorithm(Builder(), new TransitiveOpening(), F1Criterion.Instance).Run(data, ct)[0];
        var direct = DAfcTo().Run(data, ct)[0];

        // α* = α₁ on these data, so D-PAFC-TO returns the allotment of D-AFC-TO
        Assert.Equal(SelectedThreshold, principal.Alpha, precision: 12);
        Assert.True(principal.HasSameClusters(direct));
        Assert.Equal(direct.Centers, principal.Centers);
        Assert.Equal(direct.Score, principal.Score);
    }

    [Fact]
    public void SneathSokal_ComparedWithClosureAndTagaMinimum()
    {
        var data = BookExampleTests.SneathSokal();
        var ct = TestContext.Current.CancellationToken;
        var opening = DAfcTo().Run(data, ct)[0];
        var tagaMinimum = new DAfcTagaAlgorithm(Builder(), new TAGAProcessor(MinimumTAGA.Instance), F1Criterion.Instance).Run(data, ct)[0];
        var closure = new DAfcTcAlgorithm(Builder(), new MaxMinTransitiveClosure(), F1Criterion.Instance).Run(data, ct)[0];

        // book: D-AFC-TAGA with the minimum gives the same threshold and classes (tbl-taga-sneath-ties)
        Assert.Equal(opening.Alpha, tagaMinimum.Alpha, precision: 12);
        Assert.True(opening.HasSameClusters(tagaMinimum));

        // D-AFC-TC: α* = 0.96875, classes of 13 and 3 objects, typical points x6 and x10
        Assert.Equal(0.96875, closure.Alpha, precision: 12);
        Assert.Equal([13, 3], closure.MemberSets.Select(members => members.Length).ToArray());
        Assert.Equal([6, 10], closure.Centers.Select(center => center + 1).ToArray());
    }

    [Fact]
    public void SneathSokal_SingleLinkageOrderOpening_GivesThePartitionOfTheClosure()
    {
        // book, sec. "Relation to the Closure and to the TAGA Family": ratio leap selects 0.375
        // with the classes {x1..x8, x12..x16} and {x9, x10, x11}
        var best = DAfcTo(TransitiveOpeningMethod.SingleLinkageOrder)
            .Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken)[0];

        Assert.Equal(0.375, best.Alpha, precision: 12);
        var classes = best.MemberSets.Select(members => members.Order().ToArray()).OrderBy(members => members[0]).ToArray();
        Assert.Equal([.. Range(1, 8), .. Range(12, 16)], classes[0]);
        Assert.Equal(Range(9, 11), classes[1]);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(0.97)]
    public void LeastThreshold_IsRespected(double leastAlpha)
    {
        var data = BookExampleTests.SneathSokal();
        var ct = TestContext.Current.CancellationToken;
        var best = DAfcTo(leastAlpha: leastAlpha).Run(data, ct)[0];
        var levels = DAfcTo().BuildOpeningMatrix(data).GetDistinctValues();

        Assert.True(best.Alpha >= leastAlpha);
        Assert.Contains(best.Alpha, levels);
        if (leastAlpha <= SelectedThreshold)   // the unconstrained choice already satisfies the bound
            Assert.Equal(SelectedThreshold, best.Alpha, precision: 12);
    }

    [Fact]
    public void LeastThreshold_AboveEveryLevel_GivesNoResult()
    {
        var found = DAfcTo(leastAlpha: 0.995).Run(BookExampleTests.SneathSokal(), TestContext.Current.CancellationToken);
        Assert.Empty(found);
    }

    [Fact]
    public void GivenToleranceMatrix_AgreesWithAttributeData()
    {
        var data = BookExampleTests.SneathSokal();
        var ct = TestContext.Current.CancellationToken;
        var builder = Builder();
        var algorithm = DAfcTo();

        var fromData = algorithm.Run(data, ct)[0];
        var fromMatrix = algorithm.Run(builder.Build(data), builder.Normalize(data), ct)[0];
        var withoutAttributes = algorithm.Run(builder.Build(data), null, ct)[0];

        Assert.Equal(fromData.Centers, fromMatrix.Centers);
        Assert.Equal(fromData.Score, fromMatrix.Score);
        // without attribute data the threshold and the classes are the same; ties go by object index
        Assert.Equal(fromData.Alpha, withoutAttributes.Alpha);
        Assert.True(fromData.HasSameClusters(withoutAttributes));
        Assert.Equal(fromData.Score, withoutAttributes.Score, precision: 12);
        Assert.Null(withoutAttributes.TiebreakerScore);
    }
}
