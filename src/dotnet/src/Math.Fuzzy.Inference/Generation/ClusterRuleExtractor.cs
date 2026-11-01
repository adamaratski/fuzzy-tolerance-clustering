using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Generation;

/// <summary>What kind of rule base to build from a clustering result.</summary>
public enum RuleBaseKind
{
    /// <summary>
    /// Mamdani MIMO system of the monograph (Viattchenin 2013, sec. 4.5): one output yₗ per
    /// cluster on [0, 1], consequent HIGH for the own output and LOW for intersecting clusters.
    /// </summary>
    Allotment,
    /// <summary>Single-output classifier: the consequent of rule l is the class of cluster l.</summary>
    Classifier,
    /// <summary>Mamdani regression of a target column: one output term per cluster.</summary>
    Mamdani,
    /// <summary>Takagi–Sugeno with constant consequents fitted to a target column.</summary>
    SugenoZero,
    /// <summary>Takagi–Sugeno with linear consequents fitted to a target column.</summary>
    SugenoFirst,
}

public sealed record RuleExtractionOptions
{
    public RuleBaseKind Kind { get; init; } = RuleBaseKind.Allotment;
    /// <summary>Shape of the antecedent terms (and of Mamdani regression output terms).</summary>
    public MfShape Shape { get; init; } = MfShape.Trapezoidal;
    /// <summary>Column of the data that is the output for Mamdani / Sugeno regression; excluded from the inputs.</summary>
    public int TargetColumn { get; init; } = -1;
    /// <summary>Column names (all columns of the data, target included). Defaults to x1 … xm.</summary>
    public IReadOnlyList<string>? FeatureNames { get; init; }
    /// <summary>
    /// True class of every object (0-based) and the class names. When present, the classifier
    /// consequent of rule l is the majority class of cluster l, and allotment outputs are named
    /// after it; otherwise clusters themselves are the classes.
    /// </summary>
    public IReadOnlyList<int>? Labels { get; init; }
    public IReadOnlyList<string>? ClassNames { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Builds a fuzzy rule base from an allotment R*_c(X) = {A¹_(α), …, Aᶜ_(α)} produced by the
/// D-AFC family. One rule per fuzzy cluster; antecedent terms follow the monograph
/// (Viattchenin 2013, sec. 4.5.2, eqs. (4.19)–(4.26)):
/// <list type="bullet">
/// <item>support A^l_α = {xᵢ : μₗᵢ ≥ α} gives [x̂min, x̂max] per attribute (4.19)–(4.20);</item>
/// <item>kernel K(A^l) — the typical points, μₗᵢ = h(A^l) — gives the core [m̲, m̄] (4.23)–(4.26);</item>
/// <item>the feet a̲, ā lie on the lines through (m̲, 1), (x̂min, 1 − α) and (m̄, 1), (x̂max, 1 − α)
/// (4.21)–(4.22), i.e. a̲ = m̲ − (m̲ − x̂min)/α, ā = m̄ + (x̂max − m̄)/α.</item>
/// </list>
/// The trapezoid (a̲, m̲, m̄, ā) degenerates to a triangle when the kernel holds one point.
/// Consequents of the allotment system (p. 211, Figs. 4.10–4.11): HIGH Cˡₗ = (α, μ̲ₗ, μ̄ₗ, 1),
/// μ̲ₗ / μ̄ₗ the least / largest membership over A^l_α; LOW Cˡₘ = (0, 1 − μ̄ₘ, 1 − μ̲ₘ, 1 − α)
/// when A^l and A^m intersect (w ≠ 0 in (2.7)), no clause for y_m otherwise.
/// <see cref="MfShape.Triangular"/> forces the apex to the cluster's typical point and
/// <see cref="MfShape.Gaussian"/> puts a Gaussian on the core midpoint with μ(x̂min or x̂max) = 1 − α;
/// both are extensions not defined in the monograph. Antecedent terms keep height 1 even
/// for subnormal clusters: the monograph requires the height to be "taken into account" but
/// gives no formula.
/// </summary>
public static class ClusterRuleExtractor
{
    public static FuzzyInferenceSystem Extract(RawDataMatrix data, ClusterResult result, RuleExtractionOptions options)
    {
        int objectCount = data.Rows, columnCount = data.Cols, clusterCount = result.ClusterCount;
        if (clusterCount == 0) throw new ArgumentException("The clustering result is empty.", nameof(result));
        if (result.Membership.GetLength(0) != objectCount || result.Membership.GetLength(1) != clusterCount)
            throw new ArgumentException(
                $"Membership matrix is {result.Membership.GetLength(0)}×{result.Membership.GetLength(1)}, expected {objectCount}×{clusterCount} (objects × clusters).",
                nameof(result));
        double alpha = result.Alpha;
        if (!(alpha > 0 && alpha <= 1)) throw new ArgumentException($"α must lie in (0, 1], got {alpha}.", nameof(result));

        bool regression = options.Kind is RuleBaseKind.Mamdani or RuleBaseKind.SugenoZero or RuleBaseKind.SugenoFirst;
        int targetColumn = options.TargetColumn;
        if (regression && (targetColumn < 0 || targetColumn >= columnCount))
            throw new ArgumentException("A target column is required for Mamdani / Sugeno regression.", nameof(options));
        if (options.Labels is { } labels && (labels.Count != objectCount || options.ClassNames is null))
            throw new ArgumentException("Labels need one entry per object and class names.", nameof(options));

        var columnNames = options.FeatureNames is { } featureNames && featureNames.Count == columnCount
            ? featureNames.ToArray()
            : Enumerable.Range(1, columnCount).Select(column => $"x{column}").ToArray();
        int[] inputColumns = Enumerable.Range(0, columnCount).Where(column => !regression || column != targetColumn).ToArray();

        var clusters = Enumerable.Range(0, clusterCount).Select(cluster => Describe(result, cluster)).ToArray();
        var keptClusters = Enumerable.Range(0, clusterCount).Where(cluster => clusters[cluster].Support.Length > 0).ToArray();
        if (keptClusters.Length == 0) throw new ArgumentException("Every α-level set is empty.", nameof(result));

        var fis = new FuzzyInferenceSystem
        {
            Name = options.Name ?? "fis",
            Kind = options.Kind switch
            {
                RuleBaseKind.Classifier => FisKind.Classifier,
                RuleBaseKind.SugenoZero or RuleBaseKind.SugenoFirst => FisKind.Sugeno,
                _ => FisKind.Mamdani,
            },
            SugenoOrder = options.Kind == RuleBaseKind.SugenoFirst ? 1 : 0,
        };

        // ── inputs: one term per cluster ────────────────────────────────────
        foreach (int column in inputColumns)
        {
            var variable = new FuzzyVariable { Name = columnNames[column] };
            foreach (int cluster in keptClusters)
                variable.Terms.Add(new FuzzyTerm
                {
                    Name = $"C{cluster + 1}",
                    Mf = FitTerm(data, column, clusters[cluster], result.Centers[cluster], alpha, options.Shape),
                });
            SetUniverse(variable, data, column);
            fis.Inputs.Add(variable);
        }

        var clusterClasses = ClusterClasses(result, clusters, options);

        // ── outputs ─────────────────────────────────────────────────────────
        switch (options.Kind)
        {
            case RuleBaseKind.Allotment:
                foreach (int cluster in keptClusters)
                {
                    double leastMembership = clusters[cluster].LeastMembership, largestMembership = clusters[cluster].LargestMembership;
                    fis.Outputs.Add(new FuzzyVariable
                    {
                        Name = $"y{cluster + 1}", Min = 0, Max = 1,
                        Terms =
                        [
                            new FuzzyTerm { Name = "high", Mf = new TrapezoidalMf(alpha, leastMembership, largestMembership, 1) },
                            new FuzzyTerm { Name = "low",  Mf = new TrapezoidalMf(0, 1 - largestMembership, 1 - leastMembership, 1 - alpha) },
                        ],
                    });
                    fis.Classes.Add(clusterClasses.Names[clusterClasses.ClassOfCluster[cluster]]);
                }
                break;
            case RuleBaseKind.Classifier:
                fis.Classes.AddRange(clusterClasses.Names);
                break;
            case RuleBaseKind.Mamdani:
                var mamdaniOutput = new FuzzyVariable { Name = columnNames[targetColumn] };
                foreach (int cluster in keptClusters)
                    mamdaniOutput.Terms.Add(new FuzzyTerm
                    {
                        Name = $"C{cluster + 1}",
                        Mf = FitTerm(data, targetColumn, clusters[cluster], result.Centers[cluster], alpha, options.Shape),
                    });
                SetUniverse(mamdaniOutput, data, targetColumn);
                fis.Outputs.Add(mamdaniOutput);
                break;
            default:
                var sugenoOutput = new FuzzyVariable { Name = columnNames[targetColumn] };
                SetUniverse(sugenoOutput, data, targetColumn);
                fis.Outputs.Add(sugenoOutput);
                break;
        }

        // ── rules ───────────────────────────────────────────────────────────
        for (int ruleIndex = 0; ruleIndex < keptClusters.Length; ruleIndex++)
        {
            int cluster = keptClusters[ruleIndex];
            var rule = new FuzzyRule
            {
                Label      = $"C{cluster + 1}",
                Antecedent = Enumerable.Repeat(ruleIndex, inputColumns.Length).ToArray(),
            };
            switch (options.Kind)
            {
                case RuleBaseKind.Allotment:
                    // HIGH (term 0) for the own output, LOW (term 1) for outputs of clusters that intersect A^l, none otherwise
                    rule.Consequent = Enumerable.Range(0, keptClusters.Length)
                        .Select(output => output == ruleIndex ? 0
                            : Intersect(clusters[cluster].Support, clusters[keptClusters[output]].Support) ? 1 : -1)
                        .ToArray();
                    break;
                case RuleBaseKind.Classifier:
                    rule.Class = clusterClasses.ClassOfCluster[cluster];
                    break;
                case RuleBaseKind.Mamdani:
                    rule.Consequent = [ruleIndex];
                    break;
                default:
                    rule.Coefficients = new double[fis.SugenoOrder == 0 ? 1 : inputColumns.Length + 1];
                    break;
            }
            fis.Rules.Add(rule);
        }

        if (fis.Kind == FisKind.Sugeno)
        {
            var inputs = data.SelectColumns(inputColumns);
            var targets = Enumerable.Range(0, objectCount).Select(row => data[row, targetColumn]).ToArray();
            ConsequentFitter.FitSugeno(fis, inputs, targets);
        }

        fis.Validate();
        return fis;
    }

    /// <summary>
    /// Support A^l_α (objects with μ ≥ α), kernel K(A^l) (typical points, μ = height) and the least /
    /// largest membership over the support of the α-level fuzzy cluster A^l_(α). The largest membership
    /// is the height h(A^l); a cluster with height below 1 is subnormal.
    /// </summary>
    public sealed record ClusterSets(int[] Support, int[] Kernel, double LeastMembership, double LargestMembership)
    {
        public double Height => LargestMembership;
    }

    /// <summary>Support, kernel and membership range of cluster <paramref name="cluster"/> of <paramref name="result"/>.</summary>
    public static ClusterSets Describe(ClusterResult result, int cluster)
    {
        int objectCount = result.Membership.GetLength(0);
        var support = new List<int>();
        double least = double.PositiveInfinity, largest = 0;
        for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
        {
            double membership = result.Membership[objectIndex, cluster];
            if (membership <= 0 || membership < result.Alpha) continue;
            support.Add(objectIndex);
            least   = System.Math.Min(least, membership);
            largest = System.Math.Max(largest, membership);
        }
        // typical points: objects where μ attains the height h(A^l) = sup μ
        const double tolerance = 1e-12;
        var kernel = support.Where(objectIndex => result.Membership[objectIndex, cluster] >= largest - tolerance).ToArray();
        return new ClusterSets(support.ToArray(), kernel, support.Count == 0 ? 0 : least, largest);
    }

    /// <summary>Antecedent term of cluster <paramref name="sets"/> on <paramref name="column"/>, per (4.19)–(4.26).</summary>
    public static MembershipFunction FitTerm(RawDataMatrix data, int column, ClusterSets sets, int typicalPoint, double alpha, MfShape shape)
    {
        double supportMin = sets.Support.Min(objectIndex => data[objectIndex, column]);
        double supportMax = sets.Support.Max(objectIndex => data[objectIndex, column]);
        double coreMin = sets.Kernel.Min(objectIndex => data[objectIndex, column]);
        double coreMax = sets.Kernel.Max(objectIndex => data[objectIndex, column]);

        switch (shape)
        {
            case MfShape.Triangular:
            {
                double apex = sets.Kernel.Contains(typicalPoint) ? data[typicalPoint, column] : (coreMin + coreMax) / 2;
                return new TriangularMf(LeftFoot(apex, supportMin, alpha), apex, RightFoot(apex, supportMax, alpha));
            }
            case MfShape.Gaussian:
            {
                double center = (coreMin + coreMax) / 2;
                double reach = System.Math.Max(center - supportMin, supportMax - center);
                // μ = 1 − α at the farther end of the support; 1 − α is floored so that α = 1 stays finite
                double levelAtReach = System.Math.Max(1 - alpha, 1e-6);
                double sigma = reach / System.Math.Sqrt(-2 * System.Math.Log(levelAtReach));
                return new GaussianMf(center, System.Math.Max(sigma, GaussianMf.MinSigma * (1 + System.Math.Abs(center))));
            }
            default:
                return new TrapezoidalMf(LeftFoot(coreMin, supportMin, alpha), coreMin, coreMax, RightFoot(coreMax, supportMax, alpha));
        }
    }

    // line through (core, 1) and (support bound, 1 − α) reaches 0 at core ∓ |core − bound| / α
    private static double LeftFoot(double core, double supportMin, double alpha)  => core - (core - supportMin) / alpha;
    private static double RightFoot(double core, double supportMax, double alpha) => core + (supportMax - core) / alpha;

    private static void SetUniverse(FuzzyVariable variable, RawDataMatrix data, int column)
    {
        double lower = double.PositiveInfinity, upper = double.NegativeInfinity;
        for (int row = 0; row < data.Rows; row++)
        {
            lower = System.Math.Min(lower, data[row, column]);
            upper = System.Math.Max(upper, data[row, column]);
        }
        foreach (var term in variable.Terms)
        {
            lower = System.Math.Min(lower, term.Mf.Support.Min);
            upper = System.Math.Max(upper, term.Mf.Support.Max);
        }
        if (upper - lower < 1e-12) { lower -= 0.5; upper += 0.5; }
        variable.Min = lower;
        variable.Max = upper;
    }

    private static bool Intersect(int[] first, int[] second) => first.Intersect(second).Any();

    /// <summary>Class index per cluster and the class name list (majority class when labels are known).</summary>
    private static (int[] ClassOfCluster, IReadOnlyList<string> Names) ClusterClasses(
        ClusterResult result, ClusterSets[] clusters, RuleExtractionOptions options)
    {
        int clusterCount = clusters.Length;
        if (options.Labels is not { } labels || options.ClassNames is not { } classNames)
            return (Enumerable.Range(0, clusterCount).ToArray(),
                    Enumerable.Range(1, clusterCount).Select(number => $"C{number}").ToArray());

        var classOfCluster = new int[clusterCount];
        for (int cluster = 0; cluster < clusterCount; cluster++)
        {
            var votes = new int[classNames.Count];
            foreach (int objectIndex in clusters[cluster].Support) votes[labels[objectIndex]]++;
            // ties go to the class of the typical point, then to the lower index
            int majority = 0;
            for (int classIndex = 1; classIndex < votes.Length; classIndex++)
                if (votes[classIndex] > votes[majority]) majority = classIndex;
            int typicalPoint = result.Centers.Length > cluster ? result.Centers[cluster] : -1;
            if (typicalPoint >= 0 && votes[labels[typicalPoint]] == votes[majority]) majority = labels[typicalPoint];
            classOfCluster[cluster] = majority;
        }
        return (classOfCluster, classNames);
    }
}
