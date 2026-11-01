using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Aggregation.Taga;

/// <summary>
/// Applies TAGA aggregation to a fuzzy tolerance matrix.
/// Iteratively finds max-value pairs, aggregates their neighbors, rebuilds matrix.
/// </summary>
public sealed class TAGAProcessor(ITAGAOperator op)
{
    public FuzzyToleranceMatrix Apply(FuzzyToleranceMatrix input)
    {
        var result = input.Clone();
        int n = result.Size;
        var processed = new bool[n, n];

        while (true)
        {
            // Find max unprocessed off-diagonal value
            double maxVal = -1;
            int maxI = -1, maxJ = -1;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (!processed[i, j] && result[i, j] > maxVal)
                {
                    maxVal = result[i, j];
                    maxI = i; maxJ = j;
                }
            }
            if (maxI < 0) break;

            // Collect related values (row i and row j, excluding diagonal)
            var related = new List<double>();
            for (int k = 0; k < n; k++)
            {
                if (k != maxI) related.Add(result[maxI, k]);
                if (k != maxJ && k != maxI) related.Add(result[maxJ, k]);
            }
            related.Sort();

            double aggregated = op.Aggregate(related);
            result[maxI, maxJ] = aggregated;
            processed[maxI, maxJ] = processed[maxJ, maxI] = true;
        }

        return result;
    }
}
