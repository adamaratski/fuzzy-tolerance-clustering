using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace Math.Fuzzy.Core.Tests;

public class FuzzyToleranceMatrixTests
{
    [Fact]
    public void Symmetry_IsEnforced_OnSet()
    {
        var m = new FuzzyToleranceMatrix(4);
        m[0, 2] = 0.7;
        Assert.Equal(0.7, m[2, 0]);
    }

    [Fact]
    public void Diagonal_IsOne_ByDefault()
    {
        var m = new FuzzyToleranceMatrix(5);
        for (int i = 0; i < 5; i++)
            Assert.Equal(1.0, m[i, i]);
    }

    [Fact]
    public void GetDistinctValues_ExcludesDiagonalAndBoundary()
    {
        var m = new FuzzyToleranceMatrix(3);
        m[0, 1] = 0.6;
        m[0, 2] = 0.8;
        m[1, 2] = 0.6;

        var values = m.GetDistinctValues();

        Assert.DoesNotContain(0.0, values);
        Assert.DoesNotContain(1.0, values);
        Assert.Contains(0.6, values);
        Assert.Contains(0.8, values);
        // Descending order
        Assert.True(values[0] >= values[^1]);
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var m = new FuzzyToleranceMatrix(3);
        m[0, 1] = 0.5;
        var clone = m.Clone();
        clone[0, 1] = 0.9;

        Assert.Equal(0.5, m[0, 1]); // original unchanged
    }
}

public class ToleranceMatrixBuilderTests
{
    [Fact]
    public void Build_ProducesSymmetricMatrix_WithDiagonalOnes()
    {
        // 4 objects, 2 features
        var data = RawDataMatrix.FromArray(new double[,]
        {
            { 1.0, 0.0 },
            { 0.9, 0.1 },
            { 0.0, 1.0 },
            { 0.1, 0.9 },
        });

        var builder = new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance);
        var matrix = builder.Build(data);

        Assert.Equal(4, matrix.Size);

        for (int i = 0; i < 4; i++)
            Assert.Equal(1.0, matrix[i, i]);

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
            Assert.Equal(matrix[i, j], matrix[j, i]);
    }

    [Fact]
    public void Build_SimilarObjects_HaveHighRelation()
    {
        // objects 0&1 are close, objects 2&3 are close, groups are far apart
        var data = RawDataMatrix.FromArray(new double[,]
        {
            { 0.0, 0.0 },
            { 0.1, 0.1 },
            { 1.0, 1.0 },
            { 0.9, 0.9 },
        });

        var builder = new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance);
        var matrix = builder.Build(data);

        // 0↔1 should be more similar than 0↔2
        Assert.True(matrix[0, 1] > matrix[0, 2]);
        Assert.True(matrix[2, 3] > matrix[1, 2]);
    }
}

public class MinMaxNormalizationTests
{
    [Fact]
    public void Apply_ScalesColumnsToZeroOne()
    {
        var data = RawDataMatrix.FromArray(new double[,]
        {
            { 2.0, 10.0 },
            { 4.0, 20.0 },
            { 6.0, 30.0 },
        });

        var result = MinMaxNormalization.Instance.Apply(data);

        Assert.Equal(0.0, result[0, 0], precision: 10);
        Assert.Equal(0.5, result[1, 0], precision: 10);
        Assert.Equal(1.0, result[2, 0], precision: 10);

        Assert.Equal(0.0, result[0, 1], precision: 10);
        Assert.Equal(1.0, result[2, 1], precision: 10);
    }
}

public class LeapHeuristicTests
{
    [Fact]
    public void SelectsUpperEndpoint_OfMaximalLeap()
    {
        // Off-diagonal levels ascending α₀=0.4, α₁=0.5, α₂=0.85, α₃=0.9 (α₄ = 1 implicit).
        // Leaps compared: 0.1, 0.35, 0.05 → maximal 0.35 → α* = 0.85 (upper endpoint).
        var m = new FuzzyToleranceMatrix(4);
        m[0, 1] = 0.9;
        m[0, 2] = 0.5;
        m[0, 3] = 0.4;
        m[1, 2] = 0.5;
        m[1, 3] = 0.4;
        m[2, 3] = 0.85;

        var result = LeapHeuristicAlphaSelector.Instance.Select(m);

        Assert.Single(result);
        Assert.Equal(0.85, result[0], precision: 10);
    }

    [Fact]
    public void BookExample_ClosureLevels_GiveAlphaStar08()
    {
        // Levels of the closure of the five-object example: 0.4 < 0.5 < 0.8 < 0.9
        // leaps 0.1, 0.3, 0.1 → maximal 0.3 → α* = 0.8.
        var alpha = LeapHeuristicAlphaSelector.SelectFrom([0.9, 0.8, 0.5, 0.4]);
        Assert.Equal(0.8, alpha!.Value, precision: 10);
    }

    [Fact]
    public void PublishedResults_YangWu_And_SneathSokal()
    {
        // Closure levels of the Yang–Wu data (squared Euclidean, max normalisation):
        // 0.96875 < 0.98 < 0.99944 < 0.99969 → maximal leap 0.98→0.99944 → α* = 0.99944 (c = 4).
        Assert.Equal(0.99944, LeapHeuristicAlphaSelector.SelectFrom([0.99969, 0.99944, 0.98, 0.96875])!.Value, precision: 10);
        // Sneath–Sokal: 0.94444 < 0.96875 < 0.9783 < 0.98611 < 0.99219 → α* = 0.96875 (c = 2).
        Assert.Equal(0.96875, LeapHeuristicAlphaSelector.SelectFrom([0.99219, 0.98611, 0.9783, 0.96875, 0.94444])!.Value, precision: 10);
    }

    [Fact]
    public void TiesResolveToSmallestIndex()
    {
        // ascending 0.1, 0.2, 0.5, 0.6, 0.9: leaps 0.1, 0.3, 0.1, 0.3 → first maximal → α₂ = 0.5
        var alpha = LeapHeuristicAlphaSelector.SelectFrom([0.9, 0.6, 0.5, 0.2, 0.1]);
        Assert.Equal(0.5, alpha!.Value, precision: 10);
    }

    [Fact]
    public void DegenerateSequences()
    {
        Assert.Equal(0.7, LeapHeuristicAlphaSelector.SelectFrom([0.7, 0.3])!.Value, precision: 10);
        Assert.Equal(0.7, LeapHeuristicAlphaSelector.SelectFrom([0.7])!.Value, precision: 10);
        Assert.Null(LeapHeuristicAlphaSelector.SelectFrom([]));
    }
}

public class HammingDistanceTests
{
    [Fact]
    public void IsNormalisedL1Mean_NotBinaryCount()
    {
        double[] a = [0.0, 0.5, 1.0];
        double[] b = [0.2, 0.5, 0.4];
        // (0.2 + 0 + 0.6) / 3 = 0.26667
        Assert.Equal(0.8 / 3.0, HammingDistance.Instance.Compute(a, b), precision: 10);
    }
}

public class MaxMinTransitiveClosureTests
{
    [Fact]
    public void Compute_SatisfiesTransitivity()
    {
        var m = new FuzzyToleranceMatrix(3);
        m[0, 1] = 0.8;
        m[1, 2] = 0.6;
        m[0, 2] = 0.3;

        var closure = new MaxMinTransitiveClosure().Compute(m);

        // T̃[0,2] >= min(T[0,1], T[1,2]) = min(0.8, 0.6) = 0.6
        Assert.True(closure[0, 2] >= 0.6 - 1e-10);
    }

    [Fact]
    public void Compute_PreservesDiagonalOnes()
    {
        var m = new FuzzyToleranceMatrix(4);
        m[0, 1] = 0.7;
        m[2, 3] = 0.5;

        var closure = new MaxMinTransitiveClosure().Compute(m);

        for (int i = 0; i < 4; i++)
            Assert.Equal(1.0, closure[i, i]);
    }

    [Fact]
    public void Compute_RemainSymmetric()
    {
        var m = new FuzzyToleranceMatrix(4);
        m[0, 1] = 0.9;
        m[0, 2] = 0.4;
        m[1, 3] = 0.7;

        var closure = new MaxMinTransitiveClosure().Compute(m);

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
            Assert.Equal(closure[i, j], closure[j, i], precision: 10);
    }
}

public class SquaredEuclideanDistanceTests
{
    [Fact]
    public void IsMeanOfSquaredDifferences()
    {
        double[] a = [0.0, 0.5, 1.0];
        double[] b = [0.2, 0.5, 0.4];
        // (0.04 + 0 + 0.36) / 3
        Assert.Equal(0.4 / 3.0, SquaredEuclideanDistance.Instance.Compute(a, b), precision: 10);
    }
}

public class QuantizeTests
{
    [Fact]
    public void RoundsToMultiplesOfEpsilon_KeepsDiagonalAndSymmetry()
    {
        var m = new FuzzyToleranceMatrix(3);
        m[0, 1] = 0.96418; m[0, 2] = 0.96423; m[1, 2] = 0.12345;
        var q = m.Quantize(0.0001);
        Assert.Equal(1.0, q[0, 0]);
        Assert.Equal(0.9642, q[0, 1], precision: 10);
        Assert.Equal(0.9642, q[0, 2], precision: 10);   // merged into one level
        Assert.Equal(0.1235, q[1, 2], precision: 10);   // 0.12345 rounds away from zero
        Assert.Equal(q[0, 1], q[1, 0]);
        Assert.Equal(2, q.GetDistinctValues().Count);
        Assert.Equal(3, m.GetDistinctValues().Count);   // original untouched
    }

    [Fact]
    public void Builder_AppliesAccuracy()
    {
        var data = RawDataMatrix.FromArray(new double[,] { { 0.0 }, { 0.33333 }, { 1.0 } });
        var builder = new ToleranceMatrixBuilder(HammingDistance.Instance, NoNormalization.Instance, accuracy: 0.1);
        var t = builder.Build(data);
        Assert.Equal(0.7, t[0, 1], precision: 10);  // 1 − 0.33333 = 0.66667 → 0.7
        Assert.Equal(0.0, t[0, 2], precision: 10);
        Assert.Equal(0.3, t[1, 2], precision: 10);  // 1 − 0.66667 = 0.33333 → 0.3
    }
}

public class MaxNormalizationTests
{
    [Fact]
    public void Apply_DividesByLargestAbsoluteValue_SoNegativeColumnsAreKept()
    {
        var data = new RawDataMatrix(2, 2);
        data[0, 0] = 2;   data[1, 0] = 4;     // non-negative column: v / max
        data[0, 1] = -10; data[1, 1] = -1;    // negative column: v / max|v|, not zeroed

        var normalised = MaxNormalization.Instance.Apply(data);

        Assert.Equal(0.5, normalised[0, 0], precision: 12);
        Assert.Equal(1.0, normalised[1, 0], precision: 12);
        Assert.Equal(-1.0, normalised[0, 1], precision: 12);
        Assert.Equal(-0.1, normalised[1, 1], precision: 12);
    }
}

public class LabeledDatasetTests
{
    [Theory]
    [InlineData("class")]
    [InlineData("group")]
    [InlineData("variety")]
    public void TryFromCsv_RecognisesANumericLabelColumnByItsHeader(string labelHeader)
    {
        string path = Path.Combine(Path.GetTempPath(), $"labelled-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, $"x,y,{labelHeader}\n1,2,1\n1.5,2.5,1\n8,9,2\n");
        try
        {
            var dataset = LabeledDataset.TryFromCsv(path);

            Assert.NotNull(dataset);
            Assert.Equal(2, dataset.Data.Cols);       // the label column is not an attribute
            Assert.Equal(2, dataset.ClassCount);
            Assert.Equal(new[] { 0, 0, 1 }, dataset.TrueLabels);
        }
        finally { File.Delete(path); }
    }
}
