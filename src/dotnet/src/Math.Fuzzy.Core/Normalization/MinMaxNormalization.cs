using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Normalization;

/// <summary>Column-wise min-max scaling to [0,1]: v' = (v - min) / (max - min).</summary>
public sealed class MinMaxNormalization : INormalization
{
    public static readonly MinMaxNormalization Instance = new();

    public RawDataMatrix Apply(RawDataMatrix data)
    {
        var result = new RawDataMatrix(data.Rows, data.Cols);
        for (int j = 0; j < data.Cols; j++)
        {
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < data.Rows; i++)
            {
                var v = data[i, j];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            double range = max - min;
            for (int i = 0; i < data.Rows; i++)
                result[i, j] = range < 1e-12 ? 0.0 : (data[i, j] - min) / range;
        }
        return result;
    }
}
