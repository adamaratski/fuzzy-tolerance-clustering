using MathFuzzy.Core.FuzzyData;

namespace MathFuzzy.Core.Tests;

/// <summary>
/// Distances between fuzzy numbers and the fuzzy tolerance of fuzzy data (book, chapter 5,
/// "Clustering Fuzzy Data"; Viattchenin 2013, sec. 3.2). The published clustering results of the
/// Yang–Ko data are not reproduced from the printed numbers (see the report of this branch), so the
/// distances are tested on cases computed by hand.
/// </summary>
public class FuzzyNumberDataTests
{
    [Fact]
    public void YangKoDistance_OfTwoTriangularNumbers_MatchesHandComputation()
    {
        // V1 = (3.34, 1.46, 1.30), V2 = (9.56, 0.27, 1.00):
        // Δm = −6.22; Δm − ½Δa = −6.22 − 0.595 = −6.815; Δm + ½Δb = −6.22 + 0.15 = −6.07;
        // d² = 38.6884 + 46.444225 + 36.8449 = 121.977525.
        double distance = FuzzyNumberDistances.TriangularSquared(3.34, 1.46, 1.30, 9.56, 0.27, 1.00);
        Assert.Equal(121.977525, distance, precision: 9);
    }

    [Fact]
    public void LeftRightDistance_WithOtherShapeIntegrals_MatchesHandComputation()
    {
        // (0, 1, 1) and (1, 0, 2) with l = r = 1 (shape exp(−u)):
        // Δm = −1; (0 − 1) − (1 − 0) = −2; (0 + 1) − (1 + 2) = −2; d² = 1 + 4 + 4 = 9.
        Assert.Equal(9.0, FuzzyNumberDistances.LeftRightSquared(0, 1, 1, 1, 0, 2, 1.0, 1.0), precision: 12);
        // with l = r = ½ it is the Yang–Ko distance
        Assert.Equal(
            FuzzyNumberDistances.TriangularSquared(0, 1, 1, 1, 0, 2),
            FuzzyNumberDistances.LeftRightSquared(0, 1, 1, 1, 0, 2, 0.5, 0.5), precision: 12);
    }

    [Fact]
    public void TrapezoidalIntervalDistance_MatchesHandComputation()
    {
        // (1, 2, 1, 1) and (0, 3, 0, 2), l = r = ½:
        // Δm̲ = 1, Δm̄ = −1, (1 − 0.5) − (0 − 0) = 0.5, (2 + 0.5) − (3 + 1) = −1.5; d² = 1 + 1 + 0.25 + 2.25.
        Assert.Equal(4.5, FuzzyNumberDistances.LeftRightIntervalSquared(1, 2, 1, 1, 0, 3, 0, 2, 0.5, 0.5), precision: 12);
    }

    [Fact]
    public void GaussianDistance_MatchesHandComputation()
    {
        // 3·(1 − 3)² + (π/2)·(2 − 1)²
        Assert.Equal(12.0 + System.Math.PI / 2.0, FuzzyNumberDistances.GaussianSquared(1, 2, 3, 1), precision: 12);
    }

    [Fact]
    public void Tolerance_OfThreeCrispTriangularNumbers_IsOneMinusNormalisedSquaredDistance()
    {
        // (0,0,0), (1,0,0), (3,0,0): d² = 3·Δm² = 3, 27, 12 → T = 1 − d²/27
        var data = FuzzyNumberDataMatrix.Parse("v_m,v_a,v_b\n0,0,0\n1,0,0\n3,0,0\n");
        var tolerance = data.BuildTolerance();

        Assert.Equal(FuzzyNumberKind.FuzzyNumber, data.Kind);
        Assert.Equal(8.0 / 9.0, tolerance[0, 1], precision: 12);
        Assert.Equal(0.0, tolerance[0, 2], precision: 12);
        Assert.Equal(5.0 / 9.0, tolerance[1, 2], precision: 12);
        Assert.Equal(1.0, tolerance[1, 1]);
    }

    [Fact]
    public void Dissimilarity_OfVectors_IsTheAverageOfTheSquaredComponentDistances()
    {
        // two attributes; component distances 3 (first) and 12 (second) → D = 7.5, no square root
        var data = FuzzyNumberDataMatrix.Parse("p_m,p_a,p_b,q_m,q_a,q_b\n0,0,0,0,0,0\n1,0,0,2,0,0\n");
        Assert.Equal(2, data.Attributes);
        Assert.Equal(["p", "q"], data.AttributeNames);
        Assert.Equal(7.5, data.Dissimilarity(0, 1), precision: 12);
    }

    [Fact]
    public void Parse_RecognisesTheKindFromTheSuffixes_InAnyColumnOrder()
    {
        var interval = FuzzyNumberDataMatrix.Parse("name,w_a,w_ml,w_b,w_mr\nfirst,1,1,1,2\nsecond,0,0,2,3\n");
        Assert.Equal(FuzzyNumberKind.FuzzyInterval, interval.Kind);
        Assert.Equal(["first", "second"], interval.ObjectNames!);
        Assert.Equal(4.5, interval.Dissimilarity(0, 1), precision: 12);   // the trapezoidal case above

        var gaussian = FuzzyNumberDataMatrix.Parse("g_sigma,g_m\n2,1\n1,3\n");
        Assert.Equal(FuzzyNumberKind.Gaussian, gaussian.Kind);
        Assert.Equal(12.0 + System.Math.PI / 2.0, gaussian.Dissimilarity(0, 1), precision: 12);
    }

    [Fact]
    public void Parse_RejectsMixedKindsAndIncompleteAttributes()
    {
        Assert.Throws<FormatException>(() => FuzzyNumberDataMatrix.Parse("p_m,p_a,p_b,q_m,q_sigma\n0,0,0,0,0\n"));
        Assert.Throws<FormatException>(() => FuzzyNumberDataMatrix.Parse("p_m,p_a\n0,0\n"));
        Assert.Throws<FormatException>(() => FuzzyNumberDataMatrix.Parse("p_m,p_a,p_b\n0,-1,0\n"));
        Assert.Throws<FormatException>(() => FuzzyNumberDataMatrix.Parse("1,2,3\n4,5,6\n"));   // no header
    }

    [Fact]
    public void YangKoDataFiles_AreReadAsPrintedInTable35()
    {
        // Viattchenin 2013, Table 3.5: ten objects described by three triangular fuzzy numbers;
        // read column by column they are the thirty numbers V1…V30 of the Yang–Ko example, whose
        // typical points V1, V15, V28 and V30 the monograph prints (pp. 149–151).
        var vectors = FuzzyNumberDataMatrix.FromCsv(DatasetPath("yang_ko_vectors.csv"));
        var numbers = FuzzyNumberDataMatrix.FromCsv(DatasetPath("yang_ko_30.csv"));

        Assert.Equal((10, 3), (vectors.Objects, vectors.Attributes));
        Assert.Equal((30, 1), (numbers.Objects, numbers.Attributes));
        Assert.Equal((3.34, 1.46, 1.30), Triple(numbers, 0));
        Assert.Equal((23.47, 0.81, 0.51), Triple(numbers, 14));
        Assert.Equal((43.56, 0.92, 0.63), Triple(numbers, 27));
        Assert.Equal((45.77, 1.71, 0.79), Triple(numbers, 29));
        for (int objectIndex = 0; objectIndex < 10; objectIndex++)
            for (int attribute = 0; attribute < 3; attribute++)
                for (int parameter = 0; parameter < 3; parameter++)
                    Assert.Equal(
                        vectors.Parameter(objectIndex, attribute, parameter),
                        numbers.Parameter(attribute * 10 + objectIndex, 0, parameter));

        // properties of the tolerance: symmetric, unit diagonal, values in [0, 1], 0 at the farthest pair
        var tolerance = numbers.BuildTolerance();
        double smallest = 1.0;
        for (int row = 0; row < 30; row++)
        {
            Assert.Equal(1.0, tolerance[row, row]);
            for (int column = 0; column < 30; column++)
            {
                Assert.Equal(tolerance[row, column], tolerance[column, row]);
                Assert.InRange(tolerance[row, column], 0.0, 1.0);
                smallest = System.Math.Min(smallest, tolerance[row, column]);
            }
        }
        Assert.Equal(0.0, smallest, precision: 12);
    }

    private static (double, double, double) Triple(FuzzyNumberDataMatrix data, int objectIndex)
        => (data.Parameter(objectIndex, 0, 0), data.Parameter(objectIndex, 0, 1), data.Parameter(objectIndex, 0, 2));

    internal static string DatasetPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }
}
