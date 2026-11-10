using MathFuzzy.Core.Normalization;
using MathFuzzy.Core.ThreeWay;

namespace MathFuzzy.Core.Tests;

/// <summary>
/// Three-way data (book, chapter 5, "Clustering Three-Way Data"; Viattchenin 2013, sec. 3.3):
/// reading, normalisation (3.51)/(3.52) and the generalised distances (3.53)–(3.55).
/// </summary>
public class ThreeWayDataTests
{
    private static ThreeWayDataMatrix SatoSato()
        => ThreeWayDataMatrix.FromCsv(FuzzyNumberDataTests.DatasetPath("sato_sato_threeway.csv"));

    [Fact]
    public void SatoSatoFile_HasThirtyEightBoysFourAttributesThreeAges()
    {
        var data = SatoSato();

        Assert.Equal((38, 4, 3), (data.Objects, data.Attributes, data.Situations));
        Assert.Equal(["height", "weight", "chest_girth", "sitting_height"], data.AttributeNames);
        Assert.Equal(["13", "14", "15"], data.SituationNames);
        // boy 32 at 14 and 15 (Kacprzyk, Owsiński and Viattchenin 2014, Table 1 and Figs. 6–8)
        Assert.Equal((151.0, 42.0, 74.0, 77.0), (data[31, 0, 1], data[31, 1, 1], data[31, 2, 1], data[31, 3, 1]));
        Assert.Equal((159.0, 48.0, 79.0, 82.0), (data[31, 0, 2], data[31, 1, 2], data[31, 2, 2], data[31, 3, 2]));
    }

    [Fact]
    public void MinMaxNormalisation_OfTheThirteenthBoy_MatchesTable37()
    {
        // Viattchenin 2013, Table 3.7: object 13 after normalisation (3.52), rows = situations
        double[,] expected =
        {
            { 0.2286, 0.1897, 0.2857, 0.2273 },
            { 0.5143, 0.2931, 0.4048, 0.4091 },
            { 0.6857, 0.3621, 0.5000, 0.5455 },
        };
        var normalized = SatoSato().Normalize(MinMaxNormalization.Instance);
        for (int situation = 0; situation < 3; situation++)
            for (int attribute = 0; attribute < 4; attribute++)
                Assert.Equal(expected[situation, attribute], normalized[12, attribute, situation], precision: 4);
    }

    [Fact]
    public void MaxNormalisation_OfTheThirteenthBoy_MatchesTable36()
    {
        // Viattchenin 2013, Table 3.6: object 13 after normalisation (3.51)
        double[,] expected =
        {
            { 0.8457, 0.4659, 0.7059, 0.8211 },
            { 0.9029, 0.5341, 0.7549, 0.8632 },
            { 0.9371, 0.5795, 0.7941, 0.8947 },
        };
        var normalized = SatoSato().Normalize(MaxNormalization.Instance);
        for (int situation = 0; situation < 3; situation++)
            for (int attribute = 0; attribute < 4; attribute++)
                Assert.Equal(expected[situation, attribute], normalized[12, attribute, situation], precision: 4);
    }

    [Fact]
    public void GeneralisedDistances_OnASmallCase_MatchHandComputation()
    {
        // one attribute, two situations: x1 = (0, 1), x2 = (1, 1)
        // pairs for (x1, x2): |0−1|, |0−1|, |1−1|, |1−1| → Hamming 2/4, squared 2/4
        // pairs for (x1, x1): |0−0|, |0−1|, |1−0|, |1−1| → 2/4: the self-distance is not 0
        var data = new ThreeWayDataMatrix(new double[,,] { { { 0, 1 } }, { { 1, 1 } } });

        Assert.Equal(0.5, data.Distance(0, 1, ThreeWayDistance.Hamming), precision: 12);
        Assert.Equal(0.5, data.Distance(0, 1, ThreeWayDistance.SquaredEuclidean), precision: 12);
        Assert.Equal(System.Math.Sqrt(0.5), data.Distance(0, 1, ThreeWayDistance.Euclidean), precision: 12);
        Assert.Equal(0.5, data.Distance(0, 0, ThreeWayDistance.Hamming), precision: 12);
        Assert.Equal(0.0, data.Distance(1, 1, ThreeWayDistance.Hamming), precision: 12);

        var tolerance = data.BuildTolerance(NoNormalization.Instance, ThreeWayDistance.SquaredEuclidean);
        Assert.Equal(1.0, tolerance[0, 0]);
        Assert.Equal(0.5, tolerance[0, 1], precision: 12);
        var relation = data.BuildTolerance(NoNormalization.Instance, ThreeWayDistance.SquaredEuclidean, keepSelfSimilarity: true);
        Assert.Equal(0.5, relation[0, 0], precision: 12);
        Assert.Equal(1.0, relation[1, 1], precision: 12);
    }

    [Fact]
    public void GeneralisedDistances_ForOneSituation_AreTheUsualDistances()
    {
        // S = 1: the usual normalised Hamming and (squared) Euclidean distances (Viattchenin 2013, p. 157)
        var data = new ThreeWayDataMatrix(new double[,,] { { { 0.2 }, { 0.9 } }, { { 0.6 }, { 0.4 } } });
        Assert.Equal((0.4 + 0.5) / 2, data.Distance(0, 1, ThreeWayDistance.Hamming), precision: 12);
        Assert.Equal((0.16 + 0.25) / 2, data.Distance(0, 1, ThreeWayDistance.SquaredEuclidean), precision: 12);
        Assert.Equal(0.0, data.Distance(0, 0, ThreeWayDistance.SquaredEuclidean), precision: 12);
    }

    [Fact]
    public void Tolerance_OfSatoSato_IsSymmetricWithValuesInTheUnitInterval()
    {
        var tolerance = SatoSato().BuildTolerance(MinMaxNormalization.Instance, ThreeWayDistance.SquaredEuclidean);
        for (int row = 0; row < 38; row++)
        {
            Assert.Equal(1.0, tolerance[row, row]);
            for (int column = 0; column < 38; column++)
            {
                Assert.Equal(tolerance[row, column], tolerance[column, row]);
                Assert.InRange(tolerance[row, column], 0.0, 1.0);
            }
        }
    }

    [Fact]
    public void Parse_ReadsLongFormatAndChecksCompleteness()
    {
        var data = ThreeWayDataMatrix.Parse("age,id,a,b\n1,p,1,2\n1,q,3,4\n2,p,5,6\n2,q,7,8\n",
            objectColumn: "id", situationColumn: "age");
        Assert.Equal(["p", "q"], data.ObjectNames);
        Assert.Equal(["a", "b"], data.AttributeNames);
        Assert.Equal(["1", "2"], data.SituationNames);
        Assert.Equal(6.0, data[0, 1, 1]);

        Assert.Throws<FormatException>(() => ThreeWayDataMatrix.Parse("id,age,a\np,1,1\np,2,2\nq,1,3\n"));          // q has no row for 2
        Assert.Throws<FormatException>(() => ThreeWayDataMatrix.Parse("id,age,a\np,1,1\np,1,2\n"));                 // duplicate row
        Assert.Throws<FormatException>(() => ThreeWayDataMatrix.Parse("id,age,a\np,1,1\n", objectColumn: "name"));  // no such column
    }
}
