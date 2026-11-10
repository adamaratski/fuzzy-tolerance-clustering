using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.AlphaSelection;

/// <summary>
/// Leap heuristic for the tolerance threshold of the closure-based algorithms
/// (Viattchenin 2007, 2013 sec. 2.3.2).
///
/// For the ascending sequence 0 &lt; α₀ &lt; α₁ &lt; … &lt; α_Z = 1 of levels built by
/// formulae (1.56)–(1.58) — the distinct off-diagonal values of the relation plus its
/// projection α_Z = 1 — the leaps g_ℓ = α_ℓ − α_{ℓ−1}, ℓ = 1, …, Z−1, are compared
/// (the leap up to α_Z = 1 is not), and α* = α_{ℓ*} is the upper endpoint of the
/// selected leap; on ties the smallest index wins.
///
/// The printed description of the heuristic says "minimal leap", but the published
/// results it accompanies (Yang–Wu: c = 4 at α* = 0.9994; Sneath–Sokal: c = 2 at
/// α* = 0.96875; Iris: c = 2 at α* = 0.9929) are reproduced only by the MAXIMAL leap;
/// this implementation therefore selects the maximal leap. The ratio form α_{ℓ−1}/α_ℓ (minimal ratio)
/// gives the same selection on all three data sets.
/// </summary>
public enum LeapForm
{
    /// <summary>The largest difference α_ℓ − α_{ℓ−1} (the form used for the closure-based algorithms).</summary>
    Difference,

    /// <summary>The smallest ratio α_{ℓ−1} / α_ℓ (the form printed for the TAGA family, Viattchenin and Damaratski 2013).</summary>
    Ratio
}

public sealed class LeapHeuristicAlphaSelector : IAlphaSelector
{
    public static readonly LeapHeuristicAlphaSelector Instance = new();

    public IReadOnlyList<double> Select(FuzzyToleranceMatrix matrix)
        => SelectFrom(matrix.GetDistinctValues()) is double alpha ? [alpha] : [];

    /// <summary>
    /// Applies the leap heuristic to a DESCENDING list of the distinct off-diagonal
    /// levels of a relation (values in (0,1)). Returns null when the list is empty and
    /// the single level when there is only one.
    /// </summary>
    public static double? SelectFrom(IReadOnlyList<double> descendingValues) =>
        SelectFrom(descendingValues, LeapForm.Difference);

    /// <summary>
    /// The leap heuristic in the given form. The two forms agree on the published examples but
    /// not in general: the ratio form favours leaps between small levels.
    /// </summary>
    public static double? SelectFrom(IReadOnlyList<double> descendingValues, LeapForm form)
    {
        int count = descendingValues.Count;
        if (count == 0) return null;
        if (count == 1) return descendingValues[0];

        // ascending[ℓ] = α_ℓ, ℓ = 0..Z−1 (α_Z = 1 is implicit and its leap is not compared)
        var ascending = new double[count];
        for (int i = 0; i < count; i++)
            ascending[i] = descendingValues[count - 1 - i];

        // Leaps that differ by less than this are treated as equal, so that the
        // smallest index wins as the heuristic prescribes.
        const double tieTolerance = 1e-12;

        double maxLeap = double.NegativeInfinity;
        int best = 1;
        for (int l = 1; l < count; l++)
        {
            // both forms are written as "larger is better": a difference, or one minus a ratio
            double leap = form == LeapForm.Difference
                ? ascending[l] - ascending[l - 1]
                : 1.0 - ascending[l - 1] / ascending[l];
            if (leap > maxLeap + tieTolerance)
            {
                maxLeap = leap;
                best = l;
            }
        }
        return ascending[best];
    }
}
