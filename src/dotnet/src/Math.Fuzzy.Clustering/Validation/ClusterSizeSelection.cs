using MathFuzzy.Clustering.Algorithms.Prototype;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Validation;

/// <summary>One row of the scan over the maximal number of objects u: the result of D-AFC(u) and its measures.</summary>
/// <param name="MaxClusterPower">The value of u.</param>
/// <param name="Allotment">The best allotment of D-AFC(u), or null when none exists for this u.</param>
/// <param name="Measures">Validity measures of the allotment, or null when there is none.</param>
/// <param name="Tied">True when another allotment of the class has the same criterion value.</param>
public sealed record ClusterSizeStep(int MaxClusterPower, ClusterResult? Allotment, AllotmentMeasures? Measures, bool Tied);

/// <summary>Result of the technique for the optimal maximal number of objects in fuzzy clusters.</summary>
/// <param name="OptimalMaxClusterPower">u*: the first u whose measure does not change at u + 1; null when the scan shows no plateau.</param>
/// <param name="Steps">Every scanned value of u, in order.</param>
public sealed record ClusterSizeSelectionResult(int? OptimalMaxClusterPower, IReadOnlyList<ClusterSizeStep> Steps)
{
    /// <summary>The allotment of u*, or null when there is no plateau.</summary>
    public ClusterResult? Allotment =>
        Steps.FirstOrDefault(step => step.MaxClusterPower == OptimalMaxClusterPower)?.Allotment;
}

/// <summary>
/// Choice of the maximal number of objects in a fuzzy cluster (Viattchenin 2016; Viattchenin and
/// Yaroma 2017). D-AFC(u) is run for u = u_min, u_min + 1, …; the optimal value u* is the first u
/// at which a validity measure of the allotment stops changing, i.e. V(u* + 1) = V(u*).
/// </summary>
public static class ClusterSizeSelection
{
    /// <summary>
    /// The least sensible value of u: the size of the smallest cluster of the principal allotment
    /// constructed by D-PAFC on the tolerance matrix. Null when there is no principal allotment.
    /// </summary>
    public static int? LeastMaxClusterPower(FuzzyToleranceMatrix tolerance, ICriterion criterion, CancellationToken ct = default)
    {
        var principal = new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, criterion).Run(tolerance, ct);
        return principal.Count == 0 ? null : principal[0].MemberSets.Min(members => members.Length);
    }

    /// <summary>
    /// The least sensible value of u for attribute data: the size of the smallest cluster of the
    /// principal allotment constructed by D-PAFC-TC. Null when there is no principal allotment.
    /// </summary>
    public static int? LeastMaxClusterPower(
        RawDataMatrix data, ToleranceMatrixBuilder builder, ICriterion criterion, CancellationToken ct = default)
    {
        var principal = new DPafcTcAlgorithm(builder, new MaxMinTransitiveClosure(), criterion).Run(data, ct);
        return principal.Count == 0 ? null : principal[0].MemberSets.Min(members => members.Length);
    }

    /// <summary>
    /// Scans u from <paramref name="firstMaxClusterPower"/> to <paramref name="lastMaxClusterPower"/>
    /// and returns the first u whose measure equals that of u + 1. A value of u for which D-AFC(u)
    /// has no allotment is recorded and skipped.
    /// </summary>
    /// <param name="measure">lmf, qmf or msc (see <see cref="AllotmentMeasures"/>).</param>
    /// <param name="tolerance">The fuzzy tolerance matrix.</param>
    public static ClusterSizeSelectionResult Scan(
        FuzzyToleranceMatrix tolerance, int firstMaxClusterPower, int lastMaxClusterPower,
        string measure, ICriterion criterion, SearchDiagnostics? diagnostics = null, CancellationToken ct = default)
    {
        if (firstMaxClusterPower < 1 || lastMaxClusterPower >= tolerance.Size || firstMaxClusterPower > lastMaxClusterPower)
            throw new ArgumentException(
                $"The range of u must satisfy 1 ≤ first ≤ last < n = {tolerance.Size}; got {firstMaxClusterPower}..{lastMaxClusterPower}.");
        _ = new AllotmentMeasures(0, 0, 0, []).Value(measure);   // an unknown name fails before the scan

        var steps = new List<ClusterSizeStep>();
        int? optimal = null;
        ClusterSizeStep? previous = null;

        for (int maxClusterPower = firstMaxClusterPower; maxClusterPower <= lastMaxClusterPower; maxClusterPower++)
        {
            ct.ThrowIfCancellationRequested();
            var found = new DAfcUAlgorithm(maxClusterPower, criterion, diagnostics: diagnostics).Run(tolerance, ct);

            ClusterSizeStep step;
            if (found.Count == 0)
                step = new ClusterSizeStep(maxClusterPower, null, null, false);
            else
            {
                bool tied = found.Count > 1 && Math.Abs(found[0].Score - found[1].Score) < 1e-12;
                step = new ClusterSizeStep(maxClusterPower, found[0], AllotmentMeasures.Compute(found[0]), tied);
            }
            steps.Add(step);

            if (optimal is null && previous?.Measures is not null && step.Measures is not null
                && Math.Abs(step.Measures.Value(measure) - previous.Measures.Value(measure)) <= 1e-12)
                optimal = previous.MaxClusterPower;
            previous = step;
        }

        return new ClusterSizeSelectionResult(optimal, steps);
    }
}
