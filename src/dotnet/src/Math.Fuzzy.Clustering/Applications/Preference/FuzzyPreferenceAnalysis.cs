using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Preference;

namespace MathFuzzy.Clustering.Applications.Preference;

/// <summary>Settings of <see cref="FuzzyPreferenceAnalysis.Analyze"/>.</summary>
/// <param name="Clusterer">
/// Algorithm applied to the tolerance between the relations. Null = D-PAFC with all distinct
/// levels and the criterion F1, as in the source.
/// </param>
/// <param name="TieTolerance">
/// Values that differ by no more than this are equal when the smallest index of fuzziness and the
/// best alternatives are determined.
/// </param>
public sealed record PreferenceAnalysisOptions(
    IRelationalClusterer? Clusterer = null,
    double TieTolerance = FuzzyToleranceMatrix.DefaultLevelTolerance);

/// <summary>Result of the analysis of a set of fuzzy preference relations.</summary>
/// <param name="Distances">Kuzmin distances d(R_k, R_f) between the relations.</param>
/// <param name="Tolerance">The tolerance T^ℛ = 1 − d / max d on the relations, which is clustered.</param>
/// <param name="Allotment">The principal allotment of the relations (best result of the algorithm), or <see cref="ClusterResult.Empty"/>.</param>
/// <param name="AllotmentTie">
/// Description of a tie at the top of the ranked allotments (<see cref="ClusterResult.DescribeTie"/>), or null.
/// With a tie the first allotment in <see cref="ClusterResult.Order"/> is taken, i.e. the typical points
/// earliest in the order of the relations, as the monograph does.
/// </param>
/// <param name="TypicalRelations">
/// Typical points of the clusters, in the order of the clusters. When the algorithm finds no
/// allotment (or the set has one relation), every relation is a candidate for the next step.
/// </param>
/// <param name="FuzzinessIndices">Modified linear index of fuzziness l(R_f) of every relation.</param>
/// <param name="SelectedRelations">Typical relations with the smallest index of fuzziness (all of them on a tie), ascending.</param>
/// <param name="NonDominated">
/// Fuzzy set of non-dominated alternatives of the selected relation; for several selected relations,
/// the aggregated set of Orlovsky with equal weights (<see cref="FuzzyPreferenceAnalysis.AggregatedNonDominatedSet"/>).
/// </param>
/// <param name="Ranking">Alternatives by decreasing degree of non-dominance; ties keep the order of the alternatives.</param>
/// <param name="BestAlternatives">Alternatives with the largest degree of non-dominance, ascending.</param>
/// <param name="AcceptanceDegree">The largest degree of non-dominance.</param>
public sealed record PreferenceAnalysisResult(
    double[,] Distances,
    FuzzyToleranceMatrix Tolerance,
    ClusterResult Allotment,
    string? AllotmentTie,
    int[] TypicalRelations,
    double[] FuzzinessIndices,
    int[] SelectedRelations,
    double[] NonDominated,
    int[] Ranking,
    int[] BestAlternatives,
    double AcceptanceDegree);

/// <summary>
/// Selection of the most representative fuzzy preference relations of a group of experts by
/// clustering, and the choice of the most appropriate alternatives (Viattchenin 2013, sec. 4.4;
/// book chapter 6, Fuzzy Preference Relation Analysis):
/// <list type="number">
/// <item>the Kuzmin distances d(R_k, R_f) = Σ_{i,j} |μ_{R_k}(a_i,a_j) − μ_{R_f}(a_i,a_j)| between the relations;</item>
/// <item>the tolerance T^ℛ by division by the largest distance and complementation;</item>
/// <item>D-PAFC on T^ℛ; the typical points of the principal allotment are the appropriate relations;</item>
/// <item>the modified linear index of fuzziness of each typical relation; the smallest value selects
/// the relation (all of them on a tie);</item>
/// <item>Orlovsky's non-domination method on the selected relation(s).</item>
/// </list>
/// </summary>
public static class FuzzyPreferenceAnalysis
{
    /// <summary>The metric of Kuzmin, d(R_k, R_f) = Σ_{i,j} |μ_{R_k}(a_i,a_j) − μ_{R_f}(a_i,a_j)|.</summary>
    public static double KuzminDistance(double[,] first, double[,] second)
    {
        int size = CheckSquare(first);
        if (second.GetLength(0) != size || second.GetLength(1) != size)
            throw new ArgumentException("The relations must be defined on the same alternatives.");
        double sum = 0.0;
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
                sum += Math.Abs(first[row, column] - second[row, column]);
        return sum;
    }

    /// <summary>Matrix of the Kuzmin distances between all relations of the set.</summary>
    public static double[,] KuzminDistances(FuzzyPreferenceRelationSet relations)
    {
        int count = relations.ExpertCount;
        var all = relations.Relations;
        var distances = new double[count, count];
        for (int row = 0; row < count; row++)
            for (int column = row + 1; column < count; column++)
                distances[row, column] = distances[column, row] = KuzminDistance(all[row], all[column]);
        return distances;
    }

    /// <summary>
    /// The tolerance on the relations: μ_T(R_k, R_f) = 1 − d(R_k, R_f) / max d. When all relations are
    /// equal every entry is 1. Values that differ by rounding noise only are merged into one level.
    /// </summary>
    public static FuzzyToleranceMatrix RelationTolerance(double[,] distances)
    {
        int count = distances.GetLength(0);
        double largest = 0.0;
        foreach (double distance in distances) largest = Math.Max(largest, distance);
        var tolerance = new FuzzyToleranceMatrix(count);
        for (int row = 0; row < count; row++)
            for (int column = row + 1; column < count; column++)
                tolerance[row, column] = largest > 0.0 ? Math.Clamp(1.0 - distances[row, column] / largest, 0.0, 1.0) : 1.0;
        tolerance.SnapLevels();
        return tolerance;
    }

    /// <summary>
    /// The modified linear index of fuzziness l(R) = (2/n²) Σ_{i,j} |μ_R(a_i,a_j) − μ_R̲(a_i,a_j)|, where
    /// R̲ is the crisp relation nearest to R (μ_R̲ = 1 if μ_R &gt; 0.5, otherwise 0)
    /// (Viattchenin 2013, eq. (4.14), p. 200).
    /// </summary>
    public static double LinearIndexOfFuzziness(double[,] relation)
    {
        int size = CheckSquare(relation);
        double sum = 0.0;
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
            {
                double value = relation[row, column];
                sum += Math.Abs(value - (value > 0.5 ? 1.0 : 0.0));
            }
        return 2.0 * sum / ((double)size * size);
    }

    /// <summary>
    /// The fuzzy set of non-dominated alternatives of Orlovsky:
    /// μ^s(a_j, a_i) = max(μ(a_j,a_i) − μ(a_i,a_j), 0) is the strict preference and
    /// μ^ND(a_i) = 1 − max_j μ^s(a_j, a_i) (Orlovsky 1978/1981; Viattchenin 2013, eqs. (4.8)–(4.9)).
    /// </summary>
    public static double[] NonDominatedSet(double[,] relation)
    {
        int size = CheckSquare(relation);
        var result = new double[size];
        for (int alternative = 0; alternative < size; alternative++)
        {
            double strongest = 0.0;
            for (int other = 0; other < size; other++)
                strongest = Math.Max(strongest, relation[other, alternative] - relation[alternative, other]);
            result[alternative] = 1.0 - strongest;
        }
        return result;
    }

    /// <summary>
    /// Orlovsky's non-dominated set for several relations with weights λ_f (Σ λ_f = 1; null = equal):
    /// the intersection (minimum) of the non-dominated set of the intersection Q_1 = min_f R_f and
    /// that of the convolution Q_2 = Σ_f λ_f R_f (Viattchenin 2013, eqs. (4.10)–(4.11)). For a single
    /// relation it is the non-dominated set of that relation.
    /// </summary>
    public static double[] AggregatedNonDominatedSet(IReadOnlyList<double[,]> relations, IReadOnlyList<double>? weights = null)
    {
        if (relations.Count == 0) throw new ArgumentException("At least one relation is needed.", nameof(relations));
        int size = CheckSquare(relations[0]);
        if (weights is not null && weights.Count != relations.Count)
            throw new ArgumentException($"{weights.Count} weights for {relations.Count} relations.", nameof(weights));
        double weightSum = weights?.Sum() ?? 1.0;
        if (weights is not null && (weights.Any(weight => weight < 0.0) || Math.Abs(weightSum - 1.0) > 1e-9))
            throw new ArgumentException("The weights must be non-negative and sum to 1.", nameof(weights));

        var intersection = new double[size, size];
        var convolution = new double[size, size];
        for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
            {
                double minimum = double.PositiveInfinity, weighted = 0.0;
                for (int expert = 0; expert < relations.Count; expert++)
                {
                    double value = relations[expert][row, column];
                    minimum = Math.Min(minimum, value);
                    weighted += (weights?[expert] ?? 1.0 / relations.Count) * value;
                }
                intersection[row, column] = minimum;
                convolution[row, column] = weighted;
            }

        var fromIntersection = NonDominatedSet(intersection);
        var fromConvolution = NonDominatedSet(convolution);
        return [.. fromIntersection.Zip(fromConvolution, Math.Min)];
    }

    /// <summary>Alternatives by decreasing degree; degrees within <paramref name="tolerance"/> keep the order of the alternatives.</summary>
    public static int[] Rank(IReadOnlyList<double> degrees, double tolerance = FuzzyToleranceMatrix.DefaultLevelTolerance)
    {
        var order = Enumerable.Range(0, degrees.Count).ToList();
        // A stable insertion sort with a tolerant comparison keeps the result a total, deterministic order.
        for (int position = 1; position < order.Count; position++)
        {
            int current = order[position], target = position;
            while (target > 0 && degrees[current] > degrees[order[target - 1]] + tolerance)
            {
                order[target] = order[target - 1];
                target--;
            }
            order[target] = current;
        }
        return [.. order];
    }

    /// <summary>Runs the five steps of the method on the set of relations.</summary>
    public static PreferenceAnalysisResult Analyze(FuzzyPreferenceRelationSet relations, PreferenceAnalysisOptions? options = null,
                                                   CancellationToken ct = default)
    {
        options ??= new PreferenceAnalysisOptions();
        double tieTolerance = options.TieTolerance;
        var all = relations.Relations;

        var distances = KuzminDistances(relations);
        var tolerance = RelationTolerance(distances);
        var clusterer = options.Clusterer ?? new DPafcAlgorithm(AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
        IReadOnlyList<ClusterResult> ranked = relations.ExpertCount < 2 ? [] : clusterer.Run(tolerance, ct);

        var allotment = ranked.Count > 0 ? ranked[0] : ClusterResult.Empty;
        int[] typical = ranked.Count > 0 ? [.. allotment.Centers] : [.. Enumerable.Range(0, relations.ExpertCount)];

        var indices = all.Select(LinearIndexOfFuzziness).ToArray();
        double smallest = typical.Min(expert => indices[expert]);
        int[] selected = [.. typical.Where(expert => indices[expert] <= smallest + tieTolerance).Order()];

        var nonDominated = AggregatedNonDominatedSet([.. selected.Select(expert => all[expert])]);
        var ranking = Rank(nonDominated, tieTolerance);
        double acceptance = nonDominated.Max();
        int[] best = [.. Enumerable.Range(0, nonDominated.Length).Where(alternative => nonDominated[alternative] >= acceptance - tieTolerance)];

        return new PreferenceAnalysisResult(distances, tolerance, allotment, ClusterResult.DescribeTie(ranked),
                                            typical, indices, selected, nonDominated, ranking, best, acceptance);
    }

    private static int CheckSquare(double[,] relation)
    {
        int size = relation.GetLength(0);
        if (relation.GetLength(1) != size) throw new ArgumentException("A preference relation must be a square matrix.");
        return size;
    }
}
