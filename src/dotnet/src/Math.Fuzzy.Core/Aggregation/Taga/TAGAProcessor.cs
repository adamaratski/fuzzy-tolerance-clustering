using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Aggregation.Taga;

/// <summary>Result of a transitive approximation: the relation, the operator used and its distance to T.</summary>
/// <param name="Approximation">T^φ — a fuzzy equivalence (min-transitive, symmetric, reflexive).</param>
/// <param name="Operator">The aggregation operator φ.</param>
/// <param name="Distance">d(T, T^φ) = Σ_i Σ_j |T_ij − T^φ_ij| over all ordered pairs.</param>
public sealed record TransitiveApproximationResult(FuzzyToleranceMatrix Approximation, ITAGAOperator Operator, double Distance);

/// <summary>
/// Transitive approximation T^φ of a fuzzy tolerance T with an aggregation operator φ — the
/// TAGA procedure of Dawyndt, De Meyer and De Baets (2006) as it is used by the D-AFC-TAGA
/// family (Viattchenin and Damaratski 2013).
///
/// The relation is processed from its largest values downwards. All pairs start "open".
///   1. λ := the largest value among the open pairs; the seed is the first open pair with this
///      value in the scanning order (1,2), (1,3), (2,3), (1,4), …
///   2. The group P is everything reachable from the seed through pairs of value λ.
///   3. The values not above λ inside P are replaced by q = φ of their multiset.
///   4. If q = λ the pairs are closed, and every outside object whose largest value towards P
///      does not exceed λ is given one common value towards the whole group: φ of its values
///      towards P. If q &lt; λ the pairs stay open and are met again at a lower level.
/// With φ = max the result is the max-min transitive closure; with the mean it is close to the
/// average-linkage (UPGMA) ultrametric; for every operator of the family it is min-transitive.
///
/// The procedure groups objects by equality of values, so it is sensitive to values that are
/// equal mathematically but differ in the last bits: a tie tolerance
/// (<see cref="DefaultTieTolerance"/>) treats such values as equal. A tolerance of 0 compares
/// exactly, which is what reproduces results computed without tie handling.
///
/// Cost: O(n²) per iteration and, in practice, fewer than 2n iterations.
/// </summary>
public sealed class TAGAProcessor
{
    /// <summary>Values that differ by no more than this are treated as equal.</summary>
    public const double DefaultTieTolerance = 1e-12;

    /// <summary>The six operators of the family, in the order used to break ties of the automatic choice.</summary>
    public static IReadOnlyList<ITAGAOperator> Operators { get; } =
    [
        MaximumTAGA.Instance, MinimumTAGA.Instance, MeanTAGA.Instance,
        MedianTAGA.Instance, UpperMedianTAGA.Instance, LowerMedianTAGA.Instance,
    ];

    private readonly ITAGAOperator? _operator;
    private readonly double _tieTolerance;

    /// <param name="op">The aggregation operator; null selects, for every relation, the operator whose approximation is nearest to it.</param>
    /// <param name="tieTolerance">Tie tolerance; 0 for exact comparison.</param>
    public TAGAProcessor(ITAGAOperator? op, double tieTolerance = DefaultTieTolerance)
    {
        if (tieTolerance < 0.0) throw new ArgumentOutOfRangeException(nameof(tieTolerance));
        _operator = op;
        _tieTolerance = tieTolerance;
    }

    /// <summary>The processor that chooses the operator automatically.</summary>
    public static TAGAProcessor Automatic { get; } = new(null);

    /// <summary>The transitive approximation T^φ.</summary>
    public FuzzyToleranceMatrix Apply(FuzzyToleranceMatrix input) => ApplyWithDetails(input).Approximation;

    /// <summary>
    /// The transitive approximation with the operator used and the distance d(T, T^φ).
    /// With the automatic choice the six approximations are computed and the nearest one is
    /// returned; on equal distances the first operator in the order of <see cref="Operators"/> wins.
    /// </summary>
    public TransitiveApproximationResult ApplyWithDetails(FuzzyToleranceMatrix input)
    {
        if (_operator is not null)
        {
            var approximation = Approximate(input, _operator, _tieTolerance);
            return new TransitiveApproximationResult(approximation, _operator, Distance(input, approximation));
        }

        TransitiveApproximationResult? best = null;
        foreach (var candidate in Operators)
        {
            var approximation = Approximate(input, candidate, _tieTolerance);
            double distance = Distance(input, approximation);
            if (best is null || distance < best.Distance - _tieTolerance)
                best = new TransitiveApproximationResult(approximation, candidate, distance);
        }
        return best!;
    }

    /// <summary>d(T, R) = Σ_i Σ_j |T_ij − R_ij| over all ordered pairs (every unordered pair counts twice).</summary>
    public static double Distance(FuzzyToleranceMatrix first, FuzzyToleranceMatrix second)
    {
        if (first.Size != second.Size) throw new ArgumentException("The relations have different sizes.");
        double sum = 0.0;
        for (int row = 0; row < first.Size; row++)
        {
            var firstRow = first.GetRow(row);
            var secondRow = second.GetRow(row);
            for (int column = row + 1; column < first.Size; column++)
                sum += Math.Abs(firstRow[column] - secondRow[column]);
        }
        return 2.0 * sum;
    }

    private static FuzzyToleranceMatrix Approximate(FuzzyToleranceMatrix input, ITAGAOperator op, double tieTolerance)
    {
        int objectCount = input.Size;
        var relation = new double[objectCount, objectCount];
        for (int row = 0; row < objectCount; row++)
            for (int column = 0; column < objectCount; column++)
                relation[row, column] = input[row, column];

        var open = new bool[objectCount, objectCount];   // open[i, j] for i < j
        int openCount = objectCount * (objectCount - 1) / 2;
        for (int second = 1; second < objectCount; second++)
            for (int first = 0; first < second; first++)
                open[first, second] = true;

        var inGroup = new bool[objectCount];
        var group = new List<int>(objectCount);
        var stack = new Stack<int>();
        var values = new List<double>();
        var pairs = new List<(int First, int Second)>();
        long iterationLimit = 50L * objectCount * objectCount + 1000;

        for (long iteration = 0; openCount > 0; iteration++)
        {
            if (iteration > iterationLimit)
                throw new InvalidOperationException("The transitive approximation did not terminate.");

            // 1. the level λ and the seed: the first open pair with the largest value
            double level = double.NegativeInfinity;
            int seedFirst = -1, seedSecond = -1;
            for (int second = 1; second < objectCount; second++)
                for (int first = 0; first < second; first++)
                    if (open[first, second] && relation[first, second] > level)
                    {
                        level = relation[first, second];
                        seedFirst = first;
                        seedSecond = second;
                    }

            // 2. the group: everything linked to the seed through values equal to λ
            Array.Clear(inGroup);
            group.Clear();
            foreach (int seed in (int[])[seedFirst, seedSecond])
            {
                inGroup[seed] = true;
                group.Add(seed);
                stack.Push(seed);
            }
            while (stack.Count > 0)
            {
                int current = stack.Pop();
                for (int other = 0; other < objectCount; other++)
                {
                    if (inGroup[other] || Math.Abs(relation[current, other] - level) > tieTolerance) continue;
                    inGroup[other] = true;
                    group.Add(other);
                    stack.Push(other);
                }
            }
            group.Sort();

            // 3. aggregate the values not above λ inside the group
            pairs.Clear();
            values.Clear();
            for (int firstPosition = 0; firstPosition < group.Count; firstPosition++)
                for (int secondPosition = firstPosition + 1; secondPosition < group.Count; secondPosition++)
                {
                    int first = group[firstPosition], second = group[secondPosition];
                    if (relation[first, second] > level + tieTolerance) continue;
                    pairs.Add((first, second));
                    values.Add(relation[first, second]);
                }
            values.Sort();
            double aggregated = op.Aggregate(values);
            if (tieTolerance > 0.0 && Math.Abs(aggregated - level) <= tieTolerance) aggregated = level;
            foreach (var (first, second) in pairs)
                relation[first, second] = relation[second, first] = aggregated;

            if (aggregated != level) continue;   // the pairs stay open at their new common value

            // 4. close the pairs and give every admissible outside object one value towards the group
            foreach (var (first, second) in pairs)
            {
                if (!open[first, second]) continue;
                open[first, second] = false;
                openCount--;
            }
            for (int outside = 0; outside < objectCount; outside++)
            {
                if (inGroup[outside]) continue;
                values.Clear();
                foreach (int member in group) values.Add(relation[outside, member]);
                values.Sort();
                if (values[^1] > level + tieTolerance) continue;
                double common = op.Aggregate(values);
                foreach (int member in group)
                    relation[outside, member] = relation[member, outside] = common;
            }
        }

        var result = FuzzyToleranceMatrix.FromArray(relation);
        if (tieTolerance > 0.0) result.SnapLevels(tieTolerance);
        return result;
    }
}
