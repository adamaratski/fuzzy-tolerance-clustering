using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Normalization;

/// <summary>
/// Column-wise normalisation by the maximum: v' = v / max|column|. For non-negative data this is
/// v / max(column), formula (2.11) of the monograph; a column of zeros stays zero.
/// </summary>
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
                if (Math.Abs(data[i, j]) > max) max = Math.Abs(data[i, j]);

            for (int i = 0; i < data.Rows; i++)
                result[i, j] = max < 1e-12 ? 0.0 : data[i, j] / max;
        }
        return result;
    }
}
