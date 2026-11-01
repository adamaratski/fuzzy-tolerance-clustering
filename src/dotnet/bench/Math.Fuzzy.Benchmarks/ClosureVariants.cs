using System.Runtime.Intrinsics;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Benchmarks;

/// <summary>
/// Implementations of the max-min transitive closure T̃ that differ in one technique
/// at a time. All of them return the same matrix; <see cref="Program"/> checks this.
/// </summary>
public static class ClosureVariants
{
    /// <summary>Reference: the pivot loop of the max-min Floyd–Warshall scheme, scalar, one thread.</summary>
    public static FuzzyToleranceMatrix ScalarSequential(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = input.Clone();
        var pivotRow = new double[size];
        for (int pivot = 0; pivot < size; pivot++)
        {
            result.GetRow(pivot).CopyTo(pivotRow);
            for (int row = 0; row < size; row++)
                RelaxRowScalar(result.GetRowMutable(row), result[row, pivot], pivotRow);
        }
        return result;
    }

    /// <summary>The same scalar loop with the rows of each pivot step distributed over all cores.</summary>
    public static FuzzyToleranceMatrix ScalarParallel(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = input.Clone();
        for (int pivot = 0; pivot < size; pivot++)
        {
            double[] pivotRow = result.GetRow(pivot).ToArray();
            Parallel.For(0, size, row => RelaxRowScalar(result.GetRowMutable(row), result[row, pivot], pivotRow));
        }
        return result;
    }

    /// <summary>
    /// The former library closure: the pivot loop with the inner loop on Vector&lt;double&gt;
    /// (256 bits here) and the rows distributed over all cores. Every vector is stored back,
    /// whether or not an entry changed.
    /// </summary>
    public static FuzzyToleranceMatrix VectorParallel(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = input.Clone();
        for (int pivot = 0; pivot < size; pivot++)
        {
            double[] pivotRow = result.GetRow(pivot).ToArray();
            Parallel.For(0, size, row => RelaxRow256(result.GetRowMutable(row), result[row, pivot], pivotRow));
        }
        return result;
    }

    /// <summary>One thread, the inner loop on 128-, 256- or 512-bit vectors.</summary>
    public static FuzzyToleranceMatrix VectorSequential(FuzzyToleranceMatrix input, int vectorBits)
    {
        int size = input.Size;
        var result = input.Clone();
        var pivotRow = new double[size];
        for (int pivot = 0; pivot < size; pivot++)
        {
            result.GetRow(pivot).CopyTo(pivotRow);
            for (int row = 0; row < size; row++)
            {
                var target = result.GetRowMutable(row);
                double viaPivot = result[row, pivot];
                switch (vectorBits)
                {
                    case 128: RelaxRow128(target, viaPivot, pivotRow); break;
                    case 256: RelaxRow256(target, viaPivot, pivotRow); break;
                    default:  RelaxRow512(target, viaPivot, pivotRow); break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// O(n²) closure: for a symmetric reflexive fuzzy relation, T̃(x_i, x_j) is the
    /// largest bottleneck (the smallest edge on the path) over all paths, attained on a
    /// maximum spanning tree. Prim's algorithm on the dense matrix gives the tree in
    /// O(n²); one traversal per source gives every bottleneck, O(n) per source.
    /// </summary>
    public static FuzzyToleranceMatrix SpanningTree(FuzzyToleranceMatrix input, bool parallel)
    {
        int size = input.Size;
        var treeParent = MaximumSpanningTree(input);

        var neighbours = new List<int>[size];
        for (int vertex = 0; vertex < size; vertex++) neighbours[vertex] = [];
        for (int vertex = 1; vertex < size; vertex++)
        {
            neighbours[vertex].Add(treeParent[vertex]);
            neighbours[treeParent[vertex]].Add(vertex);
        }

        var result = new FuzzyToleranceMatrix(size);
        void BottlenecksFrom(int source)
        {
            var row = result.GetRowMutable(source);
            var stack = new Stack<int>();
            var visited = new bool[size];
            row[source] = input[source, source];
            visited[source] = true;
            stack.Push(source);
            while (stack.Count > 0)
            {
                int vertex = stack.Pop();
                foreach (int next in neighbours[vertex])
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    double edge = input[vertex, next];
                    row[next] = vertex == source ? edge : Math.Min(row[vertex], edge);
                    stack.Push(next);
                }
            }
        }

        if (parallel) Parallel.For(0, size, BottlenecksFrom);
        else for (int source = 0; source < size; source++) BottlenecksFrom(source);
        return result;
    }

    /// <summary>Prim's algorithm for the maximum spanning tree of the complete graph with weights T.</summary>
    private static int[] MaximumSpanningTree(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var parent = new int[size];
        var bestLink = new double[size];
        var inTree = new bool[size];
        Array.Fill(bestLink, double.NegativeInfinity);
        bestLink[0] = double.PositiveInfinity;

        for (int added = 0; added < size; added++)
        {
            int next = -1;
            for (int vertex = 0; vertex < size; vertex++)
                if (!inTree[vertex] && (next < 0 || bestLink[vertex] > bestLink[next]))
                    next = vertex;
            inTree[next] = true;

            var row = input.GetRow(next);
            for (int vertex = 0; vertex < size; vertex++)
            {
                if (inTree[vertex] || row[vertex] <= bestLink[vertex]) continue;
                bestLink[vertex] = row[vertex];
                parent[vertex] = next;
            }
        }
        return parent;
    }

    private static void RelaxRowScalar(Span<double> target, double viaPivot, double[] pivotRow)
    {
        if (viaPivot <= 0.0) return;
        for (int column = 0; column < target.Length; column++)
        {
            double candidate = Math.Min(viaPivot, pivotRow[column]);
            if (candidate > target[column]) target[column] = candidate;
        }
    }

    private static void RelaxRow128(Span<double> target, double viaPivot, double[] pivotRow)
    {
        if (viaPivot <= 0.0) return;
        int width = Vector128<double>.Count, column = 0;
        var broadcast = Vector128.Create(viaPivot);
        for (; column <= target.Length - width; column += width)
        {
            var current = Vector128.Create((ReadOnlySpan<double>)target.Slice(column, width));
            var through = Vector128.Create((ReadOnlySpan<double>)pivotRow.AsSpan(column, width));
            Vector128.Max(current, Vector128.Min(broadcast, through)).CopyTo(target.Slice(column, width));
        }
        RelaxTail(target, viaPivot, pivotRow, column);
    }

    private static void RelaxRow256(Span<double> target, double viaPivot, double[] pivotRow)
    {
        if (viaPivot <= 0.0) return;
        int width = Vector256<double>.Count, column = 0;
        var broadcast = Vector256.Create(viaPivot);
        for (; column <= target.Length - width; column += width)
        {
            var current = Vector256.Create((ReadOnlySpan<double>)target.Slice(column, width));
            var through = Vector256.Create((ReadOnlySpan<double>)pivotRow.AsSpan(column, width));
            Vector256.Max(current, Vector256.Min(broadcast, through)).CopyTo(target.Slice(column, width));
        }
        RelaxTail(target, viaPivot, pivotRow, column);
    }

    private static void RelaxRow512(Span<double> target, double viaPivot, double[] pivotRow)
    {
        if (viaPivot <= 0.0) return;
        int width = Vector512<double>.Count, column = 0;
        var broadcast = Vector512.Create(viaPivot);
        for (; column <= target.Length - width; column += width)
        {
            var current = Vector512.Create((ReadOnlySpan<double>)target.Slice(column, width));
            var through = Vector512.Create((ReadOnlySpan<double>)pivotRow.AsSpan(column, width));
            Vector512.Max(current, Vector512.Min(broadcast, through)).CopyTo(target.Slice(column, width));
        }
        RelaxTail(target, viaPivot, pivotRow, column);
    }

    private static void RelaxTail(Span<double> target, double viaPivot, double[] pivotRow, int fromColumn)
    {
        for (int column = fromColumn; column < target.Length; column++)
        {
            double candidate = Math.Min(viaPivot, pivotRow[column]);
            if (candidate > target[column]) target[column] = candidate;
        }
    }

    /// <summary>Largest absolute difference between two matrices of equal size.</summary>
    public static double MaxDifference(FuzzyToleranceMatrix first, FuzzyToleranceMatrix second)
    {
        double largest = 0.0;
        for (int row = 0; row < first.Size; row++)
        {
            var firstRow = first.GetRow(row);
            var secondRow = second.GetRow(row);
            for (int column = 0; column < first.Size; column++)
                largest = Math.Max(largest, Math.Abs(firstRow[column] - secondRow[column]));
        }
        return largest;
    }
}
