using System.Collections.Concurrent;

namespace MathFuzzy.Clustering.Combinations;

/// <summary>
/// Enumerates all C(n,k) combinations of k indices from [0,n).
/// Uses Gosper's hack for efficient iteration.
/// </summary>
public static class CombinationEnumerator
{
    public static IEnumerable<int[]> Enumerate(int n, int k)
    {
        if (k <= 0 || k > n) yield break;

        var indices = Enumerable.Range(0, k).ToArray();
        yield return (int[])indices.Clone();

        while (true)
        {
            int i = k - 1;
            while (i >= 0 && indices[i] == i + n - k) i--;
            if (i < 0) yield break;

            indices[i]++;
            for (int j = i + 1; j < k; j++)
                indices[j] = indices[j - 1] + 1;

            yield return (int[])indices.Clone();
        }
    }

    /// <summary>
    /// Like <see cref="Enumerate"/> but yields the same array on every iteration
    /// without cloning. The caller must consume values before advancing the enumerator.
    /// Use only in sequential hot loops where the reference is never retained.
    /// </summary>
    public static IEnumerable<int[]> EnumerateReuse(int n, int k)
    {
        if (k <= 0 || k > n) yield break;

        var indices = Enumerable.Range(0, k).ToArray();
        yield return indices;

        while (true)
        {
            int i = k - 1;
            while (i >= 0 && indices[i] == i + n - k) i--;
            if (i < 0) yield break;

            indices[i]++;
            for (int j = i + 1; j < k; j++)
                indices[j] = indices[j - 1] + 1;

            yield return indices;
        }
    }

    public static OrderablePartitioner<int[]> CreatePartitioner(int n, int k)
        => Partitioner.Create(Enumerate(n, k), EnumerablePartitionerOptions.NoBuffering);

    public static long Count(int n, int k)
    {
        if (k > n || k < 0) return 0;
        if (k == 0 || k == n) return 1;
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 0; i < k; i++)
        {
            result = result * (n - i) / (i + 1);
        }
        return result;
    }
}
