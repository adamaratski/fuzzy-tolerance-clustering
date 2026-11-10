using MathFuzzy.Core.Asymmetric;

namespace MathFuzzy.Core.Tests;

/// <summary>
/// Asymmetric proximity data (book, chapter 5, "Asymmetric Data"; Viattchenin 2013, sec. 4.3):
/// normalisation by the maximum, the weak fuzzy preference relation, and the quasi-equivalence and
/// likeness relations.
/// </summary>
public class AsymmetricDataTests
{
    [Fact]
    public void ChildrenFile_IsTheAntireflexiveSixteenBySixteenMatrixOfTable46()
    {
        var children = AsymmetricProximityMatrix.FromCsv(FuzzyNumberDataTests.DatasetPath("children_dislike.csv"));

        Assert.Equal(16, children.Size);
        Assert.True(children.IsAntireflexive);
        Assert.Equal("x1", children.ObjectNames[0]);
        Assert.Equal(2.0, children[0, 1]);   // x1 dislikes x2 with degree 2 …
        Assert.Equal(6.0, children[1, 0]);   // … and x2 dislikes x1 with degree 6
        Assert.Equal(1.0, children[8, 11]);  // x9 → x12
    }

    [Fact]
    public void QuasiEquivalence_OfTheChildrenData_IsTheMinimumOfTheTwoPreferences()
    {
        var children = AsymmetricProximityMatrix.FromCsv(FuzzyNumberDataTests.DatasetPath("children_dislike.csv"));
        var quasiEquivalence = children.BuildTolerance(SymmetrizationMethod.QuasiEquivalence);

        // R = 1 − A/6; Q = min(R_ij, R_ji)
        Assert.Equal(5.0 / 6.0, quasiEquivalence[0, 4], precision: 12);   // A = 1 both ways
        Assert.Equal(4.0 / 6.0, quasiEquivalence[0, 6], precision: 12);   // A(1,7) = 2, A(7,1) = 1
        Assert.Equal(0.0, quasiEquivalence[0, 1], precision: 12);         // A(2,1) = 6
        Assert.Equal(1.0, quasiEquivalence[3, 3]);
    }

    [Fact]
    public void Likeness_IsOneForMutualMaximalDislike()
    {
        // L = max(min(1 − R_ij, 1 − R_ji), min(R_ij, R_ji))
        var matrix = new AsymmetricProximityMatrix(new double[,]
        {
            { 0, 6, 2 },
            { 6, 0, 3 },
            { 4, 1, 0 },
        });
        var likeness = matrix.BuildTolerance(SymmetrizationMethod.Likeness);
        var quasiEquivalence = matrix.BuildTolerance(SymmetrizationMethod.QuasiEquivalence);

        Assert.Equal(1.0, likeness[0, 1], precision: 12);           // R = 0 both ways → min(1, 1)
        Assert.Equal(0.0, quasiEquivalence[0, 1], precision: 12);
        // (1, 3): R = 2/3 and 1/3 → max(min(1/3, 2/3), min(2/3, 1/3)) = 1/3
        Assert.Equal(1.0 / 3.0, likeness[0, 2], precision: 12);
        Assert.Equal(1.0 / 3.0, quasiEquivalence[0, 2], precision: 12);
        // (2, 3): R = 1/2 and 5/6 → max(min(1/2, 1/6), min(1/2, 5/6)) = 1/2
        Assert.Equal(0.5, likeness[1, 2], precision: 12);
    }

    [Fact]
    public void WeakPreference_FollowsTheDiagonal()
    {
        var similarity = new AsymmetricProximityMatrix(new double[,] { { 4, 2 }, { 1, 4 } });
        var preference = similarity.WeakPreference();          // reflexive: used as it is
        Assert.Equal(0.5, preference[0, 1], precision: 12);
        Assert.Equal(0.25, similarity.BuildTolerance()[0, 1], precision: 12);

        var dissimilarity = new AsymmetricProximityMatrix(new double[,] { { 0, 2 }, { 1, 0 } });
        Assert.Equal(0.0, dissimilarity.WeakPreference()[0, 1], precision: 12);   // 1 − 2/2
        Assert.Equal(0.5, dissimilarity.WeakPreference()[1, 0], precision: 12);   // 1 − 1/2

        var ambiguous = new AsymmetricProximityMatrix(new double[,] { { 1, 2 }, { 1, 0 } });
        Assert.Throws<InvalidOperationException>(() => ambiguous.WeakPreference());
        Assert.Equal(0.5, ambiguous.WeakPreference(AsymmetricProximityKind.Dissimilarity)[1, 0], precision: 12);
    }

    [Fact]
    public void Parse_AcceptsAsymmetricValuesAboveOne_AndRejectsNegativeOnes()
    {
        var matrix = AsymmetricProximityMatrix.Parse("a,b\n0,3\n5,0\n");
        Assert.Equal(["a", "b"], matrix.ObjectNames);
        Assert.Equal(5.0, matrix[1, 0]);

        Assert.Throws<FormatException>(() => AsymmetricProximityMatrix.Parse("0,-1\n1,0\n"));
        Assert.Throws<FormatException>(() => AsymmetricProximityMatrix.Parse("0,1,2\n1,0,2\n"));
    }
}
