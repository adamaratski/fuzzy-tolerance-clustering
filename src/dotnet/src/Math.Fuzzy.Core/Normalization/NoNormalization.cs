using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Normalization;

/// <summary>Pass-through normalization — data is returned unchanged.</summary>
public sealed class NoNormalization : INormalization
{
    public static readonly NoNormalization Instance = new();

    public RawDataMatrix Apply(RawDataMatrix data) => data;
}
