namespace MathFuzzy.Core.Matrix;

/// <summary>
/// Computes the max-min transitive closure T̃ of a fuzzy tolerance matrix:
/// T̃[i,j] = max over all paths i → j of the smallest relation value on the path.
///
/// Algorithm: maximum spanning tree, O(n²). For a symmetric relation the strongest
/// path between two objects runs along a maximum spanning tree of the complete graph
/// weighted by T (Gower and Ross 1969), so T̃[i,j] is the smallest edge on the tree
/// path from i to j. Prim's algorithm on the dense matrix builds the tree in O(n²);
/// one traversal of the tree from every object then yields a full row of T̃ in O(n),
/// the rows being independent and computed in parallel.
///
/// The values of T̃ are entries of T copied through min and max, so the result is
/// identical, bit for bit, to the O(n³) max-min Floyd–Warshall iteration, which is
/// kept for matrices that are not symmetric.
/// </summary>
public sealed class MaxMinTransitiveClosure
{
    public FuzzyToleranceMatrix Compute(FuzzyToleranceMatrix input)
    {
        return IsSymmetricAndReflexive(input) ? SpanningTreeClosure(input) : FloydWarshallClosure(input);
    }

    private static FuzzyToleranceMatrix SpanningTreeClosure(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = new FuzzyToleranceMatrix(size);
        if (size < 2) return result;

        var treeParent = MaximumSpanningTree(input);
        var treeNeighbours = new List<int>[size];
        for (int vertex = 0; vertex < size; vertex++) treeNeighbours[vertex] = [];
        for (int vertex = 1; vertex < size; vertex++)
        {
            treeNeighbours[vertex].Add(treeParent[vertex]);
            treeNeighbours[treeParent[vertex]].Add(vertex);
        }

        // Row `source` of T̃: the bottleneck (smallest edge) of the tree path to every object.
        Parallel.For(0, size, source =>
        {
            var row = result.GetRowMutable(source);
            var visited = new bool[size];
            var pending = new Stack<int>();
            row[source] = input[source, source];
            visited[source] = true;
            pending.Push(source);
            while (pending.Count > 0)
            {
                int vertex = pending.Pop();
                foreach (int next in treeNeighbours[vertex])
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    double edge = input[vertex, next];
                    row[next] = vertex == source ? edge : Math.Min(row[vertex], edge);
                    pending.Push(next);
                }
            }
        });
        return result;
    }

    /// <summary>Prim's algorithm: parent of every vertex in a maximum spanning tree rooted at vertex 0.</summary>
    private static int[] MaximumSpanningTree(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var parent = new int[size];
        var strongestLink = new double[size];
        var inTree = new bool[size];
        Array.Fill(strongestLink, double.NegativeInfinity);
        strongestLink[0] = double.PositiveInfinity;

        for (int added = 0; added < size; added++)
        {
            int next = -1;
            for (int vertex = 0; vertex < size; vertex++)
                if (!inTree[vertex] && (next < 0 || strongestLink[vertex] > strongestLink[next]))
                    next = vertex;
            inTree[next] = true;

            var row = input.GetRow(next);
            for (int vertex = 0; vertex < size; vertex++)
            {
                if (inTree[vertex] || row[vertex] <= strongestLink[vertex]) continue;
                strongestLink[vertex] = row[vertex];
                parent[vertex] = next;
            }
        }
        return parent;
    }

    /// <summary>
    /// The max-min Floyd–Warshall iteration, O(n³): after pivot k every entry holds the
    /// strongest path whose intermediate objects are among the first k + 1.
    /// </summary>
    private static FuzzyToleranceMatrix FloydWarshallClosure(FuzzyToleranceMatrix input)
    {
        int size = input.Size;
        var result = input.Clone();
        for (int pivot = 0; pivot < size; pivot++)
        {
            double[] pivotRow = result.GetRow(pivot).ToArray();
            Parallel.For(0, size, row =>
            {
                double viaPivot = result.GetRow(row)[pivot];
                if (viaPivot <= 0.0) return;
                var target = result.GetRowMutable(row);
                for (int column = 0; column < size; column++)
                {
                    double candidate = Math.Min(viaPivot, pivotRow[column]);
                    if (candidate > target[column]) target[column] = candidate;
                }
            });
        }
        return result;
    }

    /// <summary>The spanning-tree argument needs a tolerance: T(x, y) = T(y, x) and T(x, x) = 1.</summary>
    private static bool IsSymmetricAndReflexive(FuzzyToleranceMatrix matrix)
    {
        for (int row = 0; row < matrix.Size; row++)
        {
            var values = matrix.GetRow(row);
            if (values[row] != 1.0) return false;
            for (int column = row + 1; column < matrix.Size; column++)
                if (values[column] != matrix.GetRow(column)[row]) return false;
        }
        return true;
    }
}
