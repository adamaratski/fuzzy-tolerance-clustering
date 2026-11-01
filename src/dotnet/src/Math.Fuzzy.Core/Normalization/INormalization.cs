using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Normalization;

public interface INormalization
{
    RawDataMatrix Apply(RawDataMatrix data);
}
