using MathFuzzy.Core.Matrix;

namespace Math.Fuzzy.Core.Tests;

/// <summary>
/// The transitive opening (a fuzzy equivalence contained in T): properties on random tolerances,
/// the three-object example of Garmendia et al. (2009, p. 179) and the Sneath–Sokal construction
/// of the book (table of merges, levels, maximality).
/// </summary>
public class TransitiveOpeningTests
{
    private static readonly TransitiveOpeningMethod[] Methods =
        [TransitiveOpeningMethod.CompleteLinkage, TransitiveOpeningMethod.SingleLinkageOrder];

    public static TheoryData<TransitiveOpeningMethod> AllMethods => new(Methods);

    private static FuzzyToleranceMatrix RandomTolerance(int size, int seed, double? step = null)
    {
        var random = new Random(seed);
        var matrix = new FuzzyToleranceMatrix(size);
        for (int row = 0; row < size; row++)
            for (int column = row + 1; column < size; column++)
            {
                double value = random.NextDouble();
                // a coarse step produces many equal entries, i.e. ties in the merge order
                matrix[row, column] = step is double width ? System.Math.Round(value / width) * width : value;
            }
        return matrix;
    }

    private static void AssertFuzzyEquivalenceBelow(FuzzyToleranceMatrix opening, FuzzyToleranceMatrix tolerance, string context)
    {
        int size = opening.Size;
        for (int first = 0; first < size; first++)
        {
            Assert.Equal(1.0, opening.GetRow(first)[first]);
            for (int second = 0; second < size; second++)
            {
                Assert.Equal(opening.GetRow(first)[second], opening.GetRow(second)[first]);
                Assert.True(opening[first, second] <= tolerance[first, second], $"{context}: above T at ({first},{second})");
                for (int third = 0; third < size; third++)
                    Assert.True(
                        opening[first, third] >= System.Math.Min(opening[first, second], opening[second, third]),
                        $"{context}: not min-transitive at ({first},{second},{third})");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllMethods))]
    public void Opening_OfRandomTolerances_IsAFuzzyEquivalenceBelowT(TransitiveOpeningMethod method)
    {
        for (int trial = 0; trial < 40; trial++)
        {
            int size = 2 + trial % 13;
            var tolerance = RandomTolerance(size, 100 + trial, trial % 2 == 0 ? 0.1 : null);
            var opening = new TransitiveOpening(method).Compute(tolerance);
            AssertFuzzyEquivalenceBelow(opening, tolerance, $"{method}, trial {trial}");
        }
    }

    [Theory]
    [MemberData(nameof(AllMethods))]
    public void Opening_OfAFuzzyEquivalence_IsTheRelationItself(TransitiveOpeningMethod method)
    {
        for (int trial = 0; trial < 20; trial++)
        {
            var equivalence = new MaxMinTransitiveClosure().Compute(RandomTolerance(3 + trial, 200 + trial, trial % 2 == 0 ? 0.05 : null));
            var opening = new TransitiveOpening(method).Compute(equivalence);
            for (int row = 0; row < equivalence.Size; row++)
                Assert.Equal(equivalence.GetRow(row).ToArray(), opening.GetRow(row).ToArray());
        }
    }

    [Fact]
    public void Opening_HasOneMergeFewerThanObjects()
    {
        var result = new TransitiveOpening().ComputeWithMerges(RandomTolerance(12, 7));
        Assert.Equal(11, result.Merges.Count);
        Assert.Equal(12, result.Merges[^1].FirstGroup.Length + result.Merges[^1].SecondGroup.Length);
    }

    // ---- three objects (Garmendia et al. 2009, p. 179; book eq. opening-nonunique) ----

    private static readonly double[,] ThreeObjects =
    {
        { 1.0, 0.9, 0.7 },
        { 0.9, 1.0, 0.2 },
        { 0.7, 0.2, 1.0 },
    };

    [Fact]
    public void ThreeObjects_CompleteLinkage_ReturnsTheFirstOpening()
    {
        // merge x1, x2 at 0.9, then x3 at min(0.7, 0.2) = 0.2 (source report, test vector 2)
        var opening = new TransitiveOpening().Compute(FuzzyToleranceMatrix.FromArray(ThreeObjects));
        double[,] expected = { { 1.0, 0.9, 0.2 }, { 0.9, 1.0, 0.2 }, { 0.2, 0.2, 1.0 } };
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                Assert.Equal(expected[row, column], opening[row, column]);
    }

    [Fact]
    public void ThreeObjects_BothPublishedOpenings_AreMaximal()
    {
        var tolerance = FuzzyToleranceMatrix.FromArray(ThreeObjects);
        var first = FuzzyToleranceMatrix.FromArray(new[,] { { 1.0, 0.9, 0.2 }, { 0.9, 1.0, 0.2 }, { 0.2, 0.2, 1.0 } });
        var second = FuzzyToleranceMatrix.FromArray(new[,] { { 1.0, 0.2, 0.7 }, { 0.2, 1.0, 0.2 }, { 0.7, 0.2, 1.0 } });

        Assert.Empty(RaisablePairs(first, tolerance));
        Assert.Empty(RaisablePairs(second, tolerance));
    }

    // ---- Sneath–Sokal (book sec. opening-example) ----

    private static readonly (int X, int Y)[] SneathSokalPoints =
    [
        (0, 4), (0, 3), (1, 5), (2, 4), (3, 3), (2, 2), (2, 1), (1, 0),
        (5, 5), (6, 5), (7, 6), (5, 3), (7, 3), (6, 2), (6, 1), (8, 1),
    ];

    /// <summary>T_ij = 1 − (9 Δ₁² + 16 Δ₂²) / 1152: maximum normalisation (maxima 8 and 6), squared normalised Euclidean distance.</summary>
    internal static FuzzyToleranceMatrix SneathSokalTolerance()
    {
        int size = SneathSokalPoints.Length;
        var tolerance = new FuzzyToleranceMatrix(size);
        for (int row = 0; row < size; row++)
            for (int column = row + 1; column < size; column++)
            {
                int horizontal = SneathSokalPoints[row].X - SneathSokalPoints[column].X;
                int vertical = SneathSokalPoints[row].Y - SneathSokalPoints[column].Y;
                tolerance[row, column] = 1.0 - (9 * horizontal * horizontal + 16 * vertical * vertical) / 1152.0;
            }
        return tolerance;
    }

    private static int[] Objects(params int[] oneBased) => [.. oneBased.Select(number => number - 1)];
    private static int[] Range(int firstOneBased, int lastOneBased)
        => [.. Enumerable.Range(firstOneBased - 1, lastOneBased - firstOneBased + 1)];

    [Fact]
    public void SneathSokal_CompleteLinkage_ReproducesTheTableOfMerges()
    {
        // book table tbl-opening-merges; exact values from the source report (implementation notes)
        (int[] First, int[] Second, double Value)[] expected =
        [
            (Objects(9), Objects(10), 127.0 / 128),
            (Objects(14), Objects(15), 71.0 / 72),
            (Objects(6), Objects(7), 71.0 / 72),
            (Objects(1), Objects(2), 71.0 / 72),
            (Objects(4), Objects(5), 1127.0 / 1152),
            (Objects(12), Objects(13), 31.0 / 32),
            (Objects(14, 15), Objects(16), 275.0 / 288),
            (Objects(9, 10), Objects(11), 275.0 / 288),
            (Objects(6, 7), Objects(8), 1079.0 / 1152),
            (Objects(1, 2), Objects(3), 1079.0 / 1152),
            (Range(1, 3), Range(4, 5), 263.0 / 288),
            (Range(12, 13), Range(14, 16), 1007.0 / 1152),
            (Range(1, 5), Range(6, 8), 47.0 / 72),
            (Range(9, 11), Range(12, 16), 743.0 / 1152),
            (Range(1, 8), Range(9, 16), 7.0 / 32),
        ];

        var merges = new TransitiveOpening().ComputeWithMerges(SneathSokalTolerance()).Merges;

        Assert.Equal(expected.Length, merges.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].First, merges[index].FirstGroup);
            Assert.Equal(expected[index].Second, merges[index].SecondGroup);
            Assert.Equal(expected[index].Value, merges[index].Value, precision: 12);
        }
    }

    [Fact]
    public void SneathSokal_CompleteLinkage_HasElevenLevels()
    {
        double[] expectedAscending =
        [
            7.0 / 32, 743.0 / 1152, 47.0 / 72, 1007.0 / 1152, 263.0 / 288, 1079.0 / 1152,
            275.0 / 288, 31.0 / 32, 1127.0 / 1152, 71.0 / 72, 127.0 / 128,
        ];
        var levels = new TransitiveOpening().Compute(SneathSokalTolerance()).GetDistinctValues().Reverse().ToArray();

        Assert.Equal(expectedAscending.Length, levels.Length);
        for (int index = 0; index < levels.Length; index++)
            Assert.Equal(expectedAscending[index], levels[index], precision: 12);
    }

    [Fact]
    public void SneathSokal_CompleteLinkage_RowsMatchFigure2OfTheSource()
    {
        var opening = new TransitiveOpening().Compute(SneathSokalTolerance());
        // rows of x2 and x13 (source report, implementation notes, six decimals)
        double[] rowOfSecond = [0.986111, 1, 0.936632, 0.913194, 0.913194, 0.652778, 0.652778, 0.652778,
                                0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875];
        double[] rowOfThirteenth = [0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875, 0.21875,
                                    0.644965, 0.644965, 0.644965, 0.968750, 1, 0.874132, 0.874132, 0.874132];
        for (int column = 0; column < 16; column++)
        {
            Assert.Equal(rowOfSecond[column], opening[1, column], precision: 6);
            Assert.Equal(rowOfThirteenth[column], opening[12, column], precision: 6);
        }
    }

    [Fact]
    public void SneathSokal_CompleteLinkage_IsMaximal()
    {
        var tolerance = SneathSokalTolerance();
        var opening = new TransitiveOpening().Compute(tolerance);
        AssertFuzzyEquivalenceBelow(opening, tolerance, "Sneath–Sokal");
        Assert.Empty(RaisablePairs(opening, tolerance));
    }

    [Fact]
    public void SneathSokal_SingleLinkageOrder_IsAnEquivalenceButNotMaximal()
    {
        var tolerance = SneathSokalTolerance();
        var opening = new TransitiveOpening(TransitiveOpeningMethod.SingleLinkageOrder).Compute(tolerance);

        AssertFuzzyEquivalenceBelow(opening, tolerance, "single-linkage order");
        // book: the entries of (x1,x4), (x2,x4), (x3,x4) and (x12,x13) can still be raised
        Assert.Equal([(1, 4), (2, 4), (3, 4), (12, 13)], RaisablePairs(opening, tolerance));
    }

    [Fact]
    public void SneathSokal_SingleLinkageOrder_Levels()
    {
        // source report, implementation notes: the corrected single-linkage-order variant (six decimals)
        double[] expectedAscending = [0.21875, 0.375, 0.582465, 0.652778, 0.769965, 0.913194, 0.936632, 0.954861, 0.986111, 0.992188];
        var levels = new TransitiveOpening(TransitiveOpeningMethod.SingleLinkageOrder)
            .Compute(SneathSokalTolerance()).GetDistinctValues().Reverse().ToArray();

        Assert.Equal(expectedAscending.Length, levels.Length);
        for (int index = 0; index < levels.Length; index++)
            Assert.Equal(expectedAscending[index], levels[index], precision: 6);
    }

    /// <summary>
    /// The pairs (1-based) whose entry can be raised without leaving the fuzzy equivalences below
    /// T: the entry is raised by 10⁻⁹, the max-min closure is taken, and the pair passes when the
    /// closure is still contained in T (exact for data whose distinct values are further apart).
    /// </summary>
    private static List<(int, int)> RaisablePairs(FuzzyToleranceMatrix opening, FuzzyToleranceMatrix tolerance)
    {
        const double raise = 1e-9;
        var closure = new MaxMinTransitiveClosure();
        var raisable = new List<(int, int)>();
        for (int row = 0; row < opening.Size; row++)
            for (int column = row + 1; column < opening.Size; column++)
            {
                if (opening[row, column] + raise > tolerance[row, column]) continue;
                var raised = opening.Clone();
                raised[row, column] = opening[row, column] + raise;
                var closed = closure.Compute(raised);
                bool contained = true;
                for (int first = 0; first < opening.Size && contained; first++)
                    for (int second = 0; second < opening.Size && contained; second++)
                        contained = closed[first, second] <= tolerance[first, second];
                if (contained) raisable.Add((row + 1, column + 1));
            }
        return raisable;
    }
}
