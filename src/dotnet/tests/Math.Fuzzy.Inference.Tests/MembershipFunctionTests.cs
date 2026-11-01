using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;

namespace MathFuzzy.Inference.Tests;

public class MembershipFunctionTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.5)]
    [InlineData(2.0, 1.0)]
    [InlineData(3.0, 0.75)]
    [InlineData(6.0, 0.0)]
    [InlineData(-1.0, 0.0)]
    public void Triangular_IsPiecewiseLinear(double value, double expected) =>
        Assert.Equal(expected, new TriangularMf(0, 2, 6).Evaluate(value), 12);

    [Fact]
    public void Triangular_WithVerticalSide_IsOneAtApex()
    {
        var mf = new TriangularMf(2, 2, 4);
        Assert.Equal(1, mf.Evaluate(2));
        Assert.Equal(0.5, mf.Evaluate(3), 12);
        Assert.Equal(0, mf.Evaluate(1.999));
    }

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(1.5, 0.5)]
    [InlineData(2.0, 1.0)]
    [InlineData(3.0, 1.0)]
    [InlineData(4.0, 0.5)]
    [InlineData(5.0, 0.0)]
    public void Trapezoidal_HasFlatCore(double value, double expected) =>
        Assert.Equal(expected, new TrapezoidalMf(1, 2, 3, 5).Evaluate(value), 12);

    [Fact]
    public void Gaussian_HalfMaximumAtSigmaSqrt2Ln2()
    {
        var mf = new GaussianMf(1, 2);
        Assert.Equal(1, mf.Evaluate(1), 12);
        Assert.Equal(0.5, mf.Evaluate(1 + 2 * System.Math.Sqrt(2 * System.Math.Log(2))), 12);
    }

    [Fact]
    public void WithParameters_RepairsOrderAndSigma()
    {
        Assert.Equal(new TriangularMf(1, 2, 3), new TriangularMf(0, 0, 0).WithParameters([3, 1, 2]));
        Assert.Equal(new TrapezoidalMf(0, 1, 2, 3), new TrapezoidalMf(0, 1, 2, 3).WithParameter(1, 1));
        var gaussian = (GaussianMf)new GaussianMf(0, 1).WithParameter(1, -0.5);
        Assert.Equal(0.5, gaussian.Sigma);
        Assert.Throws<ArgumentException>(() => new TriangularMf(0, 1, 2).WithParameters([0, 1]));
    }

    [Fact]
    public void AlphaCut_IsTheIntervalWhereMembershipReachesAlpha()
    {
        MembershipFunction[] shapes = [new TriangularMf(0, 2, 6), new TrapezoidalMf(1, 2, 3, 5), new GaussianMf(1, 2)];
        foreach (var mf in shapes)
        foreach (var alpha in new[] { 0.25, 0.5, 1.0 })
        {
            var (lower, upper) = mf.AlphaCut(alpha);
            Assert.Equal(alpha, mf.Evaluate(lower), 9);
            Assert.Equal(alpha, mf.Evaluate(upper), 9);
            Assert.True(mf.Evaluate((lower + upper) / 2) >= alpha - 1e-12);
        }
        Assert.Equal((2.0, 3.0), new TrapezoidalMf(1, 2, 3, 5).AlphaCut(1));   // α = 1: the core
        Assert.Throws<ArgumentOutOfRangeException>(() => new TriangularMf(0, 1, 2).AlphaCut(0));
    }

    [Fact]
    public void Area_MatchesNumericalIntegral()
    {
        MembershipFunction[] shapes = [new TriangularMf(0, 2, 6), new TrapezoidalMf(1, 2, 3, 5), new GaussianMf(1, 2)];
        foreach (var mf in shapes)
        {
            const int pointCount = 200001;
            double lower = -15, upper = 15, step = (upper - lower) / (pointCount - 1), integral = 0;
            for (int point = 1; point < pointCount; point++)
                integral += step * (mf.Evaluate(lower + point * step) + mf.Evaluate(lower + (point - 1) * step)) / 2;
            Assert.Equal(integral, mf.Area, 6);
        }
    }

    [Fact]
    public void TNorms_HaveOneAsIdentity_AndSNormsZero()
    {
        foreach (var norm in Enum.GetValues<TNorm>())
        foreach (var degree in new[] { 0.0, 0.3, 0.8, 1.0 })
        {
            Assert.Equal(degree, norm.Apply(degree, 1), 12);
            Assert.Equal(degree, norm.Dual().Apply(degree, 0), 12);
            Assert.True(norm.Apply(degree, 0.6) <= System.Math.Min(degree, 0.6) + 1e-12);          // T ≤ min
            Assert.True(norm.Dual().Apply(degree, 0.6) >= System.Math.Max(degree, 0.6) - 1e-12);   // S ≥ max
        }
    }

    [Fact]
    public void SNormDual_SatisfiesDeMorgan()
    {
        foreach (var norm in Enum.GetValues<TNorm>())
        foreach (var (left, right) in new[] { (0.2, 0.7), (0.5, 0.5), (0.9, 0.4) })
            Assert.Equal(1 - norm.Apply(1 - left, 1 - right), norm.Dual().Apply(left, right), 12);
    }
}

public class DefuzzificationTests
{
    private static (double[] Grid, double[] Memberships) Sample(Func<double, double> membership, double lower, double upper, int pointCount = 2001)
    {
        var grid = Enumerable.Range(0, pointCount).Select(point => lower + (upper - lower) * point / (pointCount - 1)).ToArray();
        return (grid, grid.Select(membership).ToArray());
    }

    [Fact]
    public void Centroid_OfTrapezoid_MatchesClosedForm()
    {
        var mf = new TrapezoidalMf(0.2, 0.4, 0.5, 0.9);
        var (grid, memberships) = Sample(mf.Evaluate, 0, 1);
        // centroid of trapezoid (a, b, c, d): (d² + cd + c² − a² − ab − b²) / (3(d + c − a − b))
        double expected = (0.81 + 0.45 + 0.25 - 0.04 - 0.08 - 0.16) / (3 * (0.9 + 0.5 - 0.2 - 0.4));
        Assert.Equal(expected, Defuzzification.Centroid.Defuzzify(grid, memberships), 6);
    }

    [Fact]
    public void Bisector_SplitsAreaInHalf()
    {
        var mf = new TriangularMf(0, 0, 1);              // area 1/2, half-area point 1 − 1/√2
        var (grid, memberships) = Sample(mf.Evaluate, 0, 1);
        Assert.Equal(1 - 1 / System.Math.Sqrt(2), Defuzzification.Bisector.Defuzzify(grid, memberships), 4);
    }

    [Fact]
    public void MaximumMethods_UseThePlateau()
    {
        var mf = new TrapezoidalMf(0, 0.25, 0.75, 1);
        var (grid, memberships) = Sample(mf.Evaluate, 0, 1, 101);
        Assert.Equal(0.25, Defuzzification.SmallestOfMaximum.Defuzzify(grid, memberships), 12);
        Assert.Equal(0.75, Defuzzification.LargestOfMaximum.Defuzzify(grid, memberships), 12);
        Assert.Equal(0.5, Defuzzification.MeanOfMaximum.Defuzzify(grid, memberships), 12);
    }

    [Fact]
    public void EmptySet_GivesNaN()
    {
        var (grid, memberships) = Sample(_ => 0, 0, 1, 11);
        foreach (var method in Enum.GetValues<Defuzzification>())
            Assert.True(double.IsNaN(method.Defuzzify(grid, memberships)));
    }
}
