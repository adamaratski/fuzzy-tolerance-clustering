using MathFuzzy.Core.Matrix;

namespace Math.Fuzzy.Core.Tests;

/// <summary>
/// The spanning-tree closure must equal the definition T̃(x, y) = max over paths of the
/// smallest relation on the path, computed here by the plain O(n³) max-min iteration.
/// </summary>
public class TransitiveClosureTests
{
    private static FuzzyToleranceMatrix RandomTolerance(int size, int seed, double? step = null, double zeroShare = 0.0)
    {
        var random = new Random(seed);
        var matrix = new FuzzyToleranceMatrix(size);
        for (int row = 0; row < size; row++)
        for (int column = row + 1; column < size; column++)
        {
            double value = random.NextDouble() < zeroShare ? 0.0 : random.NextDouble();
            // a coarse step produces many equal entries, i.e. ties in the spanning tree
            matrix[row, column] = step is double width ? System.Math.Round(value / width) * width : value;
        }
        return matrix;
    }

    private static FuzzyToleranceMatrix ReferenceClosure(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = input.Clone();
        for (int pivot = 0; pivot < size; pivot++)
        for (int row = 0; row < size; row++)
        for (int column = 0; column < size; column++)
        {
            double throughPivot = System.Math.Min(result.GetRow(row)[pivot], result.GetRow(pivot)[column]);
            if (throughPivot > result.GetRow(row)[column]) result.GetRowMutable(row)[column] = throughPivot;
        }
        return result;
    }

    [Theory]
    [InlineData(2, 1, null, 0.0)]
    [InlineData(17, 2, null, 0.0)]
    [InlineData(60, 3, 0.1, 0.0)]     // many ties
    [InlineData(60, 4, 0.25, 0.5)]    // ties and a sparse relation with zeros
    [InlineData(150, 5, null, 0.0)]
    public void Closure_EqualsMaxMinIteration_BitForBit(int size, int seed, double? step, double zeroShare)
    {
        var input = RandomTolerance(size, seed, step, zeroShare);

        var closure = new MaxMinTransitiveClosure().Compute(input);
        var reference = ReferenceClosure(input);

        for (int row = 0; row < size; row++)
            Assert.Equal(reference.GetRow(row).ToArray(), closure.GetRow(row).ToArray());
    }

    [Fact]
    public void Closure_IsTransitiveAndIdempotent()
    {
        var closure = new MaxMinTransitiveClosure().Compute(RandomTolerance(40, 11, 0.05));

        for (int first = 0; first < closure.Size; first++)
        for (int middle = 0; middle < closure.Size; middle++)
        for (int last = 0; last < closure.Size; last++)
            Assert.True(closure[first, last] >= System.Math.Min(closure[first, middle], closure[middle, last]));

        var twice = new MaxMinTransitiveClosure().Compute(closure);
        for (int row = 0; row < closure.Size; row++)
            Assert.Equal(closure.GetRow(row).ToArray(), twice.GetRow(row).ToArray());
    }

    [Fact]
    public void Closure_OfNonReflexiveMatrix_FallsBackToMaxMinIteration()
    {
        var input = RandomTolerance(12, 21);
        input.GetRowMutable(3)[3] = 0.4;   // not a tolerance any more

        var closure = new MaxMinTransitiveClosure().Compute(input);
        var reference = ReferenceClosure(input);

        for (int row = 0; row < input.Size; row++)
            Assert.Equal(reference.GetRow(row).ToArray(), closure.GetRow(row).ToArray());
    }
}
