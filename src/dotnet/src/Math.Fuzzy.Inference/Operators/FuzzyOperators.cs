namespace MathFuzzy.Inference.Operators;

/// <summary>Triangular norms (fuzzy AND / intersection).</summary>
public enum TNorm
{
    /// <summary>T(a, b) = min(a, b) (Zadeh).</summary>
    Minimum,
    /// <summary>T(a, b) = a·b.</summary>
    Product,
    /// <summary>T(a, b) = max(0, a + b − 1).</summary>
    Lukasiewicz,
    /// <summary>T(a, b) = ab / (2 − (a + b − ab)).</summary>
    Einstein,
    /// <summary>T(a, b) = b if a = 1, a if b = 1, 0 otherwise.</summary>
    Drastic,
}

/// <summary>Triangular conorms (fuzzy OR / union, rule aggregation).</summary>
public enum SNorm
{
    /// <summary>S(a, b) = max(a, b) (Zadeh).</summary>
    Maximum,
    /// <summary>S(a, b) = a + b − ab (dual of the product).</summary>
    ProbabilisticSum,
    /// <summary>S(a, b) = min(1, a + b) (dual of Łukasiewicz).</summary>
    BoundedSum,
    /// <summary>S(a, b) = (a + b) / (1 + ab).</summary>
    Einstein,
    /// <summary>S(a, b) = b if a = 0, a if b = 0, 1 otherwise.</summary>
    Drastic,
}

/// <summary>Mamdani implication: how the firing strength shapes the consequent set.</summary>
public enum Implication
{
    /// <summary>μ'(y) = min(w, μ(y)) — clipping (Mamdani).</summary>
    Minimum,
    /// <summary>μ'(y) = w·μ(y) — scaling (Larsen).</summary>
    Product,
}

/// <summary>Defuzzification of the aggregated Mamdani output set.</summary>
public enum Defuzzification
{
    /// <summary>Centre of gravity ∫yμ(y)dy / ∫μ(y)dy.</summary>
    Centroid,
    /// <summary>Point splitting the area under μ into two equal halves.</summary>
    Bisector,
    /// <summary>Mean of the points where μ attains its maximum.</summary>
    MeanOfMaximum,
    /// <summary>Smallest point where μ attains its maximum.</summary>
    SmallestOfMaximum,
    /// <summary>Largest point where μ attains its maximum.</summary>
    LargestOfMaximum,
}

public static class FuzzyOperators
{
    public static double Apply(this TNorm norm, double left, double right) => norm switch
    {
        TNorm.Minimum     => System.Math.Min(left, right),
        TNorm.Product     => left * right,
        TNorm.Lukasiewicz => System.Math.Max(0, left + right - 1),
        TNorm.Einstein    => left * right / (2 - (left + right - left * right)),
        TNorm.Drastic     => left == 1 ? right : right == 1 ? left : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(norm)),
    };

    public static double Apply(this SNorm conorm, double left, double right) => conorm switch
    {
        SNorm.Maximum          => System.Math.Max(left, right),
        SNorm.ProbabilisticSum => left + right - left * right,
        SNorm.BoundedSum       => System.Math.Min(1, left + right),
        SNorm.Einstein         => (left + right) / (1 + left * right),
        SNorm.Drastic          => left == 0 ? right : right == 0 ? left : 1,
        _ => throw new ArgumentOutOfRangeException(nameof(conorm)),
    };

    public static double Apply(this Implication implication, double firing, double membership) => implication switch
    {
        Implication.Minimum => System.Math.Min(firing, membership),
        Implication.Product => firing * membership,
        _ => throw new ArgumentOutOfRangeException(nameof(implication)),
    };

    /// <summary>The S-norm dual to a T-norm: S(a, b) = 1 − T(1 − a, 1 − b).</summary>
    public static SNorm Dual(this TNorm norm) => norm switch
    {
        TNorm.Minimum     => SNorm.Maximum,
        TNorm.Product     => SNorm.ProbabilisticSum,
        TNorm.Lukasiewicz => SNorm.BoundedSum,
        TNorm.Einstein    => SNorm.Einstein,
        TNorm.Drastic     => SNorm.Drastic,
        _ => throw new ArgumentOutOfRangeException(nameof(norm)),
    };

    /// <summary>
    /// Defuzzifies a sampled membership function μ(grid[i]) = memberships[i] on an increasing,
    /// uniformly spaced grid. Returns NaN when μ ≡ 0 (no rule fired).
    /// </summary>
    public static double Defuzzify(this Defuzzification method, ReadOnlySpan<double> grid, ReadOnlySpan<double> memberships)
    {
        if (grid.Length != memberships.Length) throw new ArgumentException("Grid and values differ in length.");
        if (grid.Length == 0) return double.NaN;

        double height = 0;
        foreach (var membership in memberships) if (membership > height) height = membership;
        if (height <= 0) return double.NaN;

        switch (method)
        {
            case Defuzzification.Centroid:
            {
                // trapezoidal rule for ∫yμ and ∫μ; exact for piecewise-linear μ sampled at its breakpoints
                double moment = 0, area = 0;
                for (int point = 1; point < grid.Length; point++)
                {
                    double step = grid[point] - grid[point - 1];
                    area   += step * (memberships[point] + memberships[point - 1]) / 2;
                    moment += step * (grid[point] * memberships[point] + grid[point - 1] * memberships[point - 1]) / 2;
                }
                return area > 0 ? moment / area : grid[IndexOfMax(memberships)];
            }
            case Defuzzification.Bisector:
            {
                double totalArea = 0;
                for (int point = 1; point < grid.Length; point++)
                    totalArea += (grid[point] - grid[point - 1]) * (memberships[point] + memberships[point - 1]) / 2;
                if (totalArea <= 0) return grid[IndexOfMax(memberships)];
                double accumulated = 0;
                for (int point = 1; point < grid.Length; point++)
                {
                    double segmentArea = (grid[point] - grid[point - 1]) * (memberships[point] + memberships[point - 1]) / 2;
                    if (accumulated + segmentArea >= totalArea / 2)
                    {
                        // linear interpolation inside the segment
                        double fraction = segmentArea > 0 ? (totalArea / 2 - accumulated) / segmentArea : 0;
                        return grid[point - 1] + fraction * (grid[point] - grid[point - 1]);
                    }
                    accumulated += segmentArea;
                }
                return grid[^1];
            }
            default:
            {
                const double tolerance = 1e-12;
                int first = -1, last = -1, count = 0;
                double sum = 0;
                for (int point = 0; point < grid.Length; point++)
                {
                    if (memberships[point] < height - tolerance) continue;
                    if (first < 0) first = point;
                    last = point;
                    sum += grid[point];
                    count++;
                }
                return method switch
                {
                    Defuzzification.SmallestOfMaximum => grid[first],
                    Defuzzification.LargestOfMaximum  => grid[last],
                    _                                 => sum / count,
                };
            }
        }
    }

    private static int IndexOfMax(ReadOnlySpan<double> values)
    {
        int best = 0;
        for (int index = 1; index < values.Length; index++) if (values[index] > values[best]) best = index;
        return best;
    }
}
