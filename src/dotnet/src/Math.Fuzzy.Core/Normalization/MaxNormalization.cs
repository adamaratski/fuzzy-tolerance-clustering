using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Normalization;

/// <summary>Column-wise max normalization: v' = v / max(column).</summary>
public sealed class MaxNormalization : INormalization
{
    public static readonly MaxNormalization Instance = new();

    public RawDataMatrix Apply(RawDataMatrix data)
    {
        var result = new RawDataMatrix(data.Rows, data.Cols);
        for (int j = 0; j < data.Cols; j++)
        {
            double max = 0.0;
            for (int i = 0; i < data.Rows; i++)
                if (data[i, j] > max) max = data[i, j];

            for (int i = 0; i < data.Rows; i++)
                result[i, j] = max < 1e-12 ? 0.0 : data[i, j] / max;
        }
        return result;
    }
}
