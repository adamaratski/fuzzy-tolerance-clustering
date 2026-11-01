using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;

namespace MathFuzzy.Inference.Systems;

/// <summary>Kind of rule consequent and, with it, of the inference scheme.</summary>
public enum FisKind
{
    /// <summary>Consequents are fuzzy terms of one or more output variables; outputs are defuzzified.</summary>
    Mamdani,
    /// <summary>Takagi–Sugeno: consequent is a constant (order 0) or a linear function (order 1).</summary>
    Sugeno,
    /// <summary>Consequent is a class label; the class with the largest aggregated firing wins.</summary>
    Classifier,
}

public enum Connective { And, Or }

/// <summary>Linguistic term: a name and its membership function.</summary>
public sealed class FuzzyTerm
{
    public required string Name { get; set; }
    public required MembershipFunction Mf { get; set; }
}

/// <summary>Linguistic variable over the universe [Min, Max].</summary>
public sealed class FuzzyVariable
{
    public required string Name { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public List<FuzzyTerm> Terms { get; set; } = [];
}

/// <summary>
/// IF x₁ is A₁ AND … AND xₘ is Aₘ THEN consequent. <see cref="Antecedent"/> holds one
/// term index per input variable (−1 = the input does not take part in the rule).
/// </summary>
public sealed class FuzzyRule
{
    /// <summary>Optional label, e.g. the cluster the rule was extracted from.</summary>
    public string? Label { get; set; }
    public int[] Antecedent { get; set; } = [];
    /// <summary>Per input: true for "xⱼ is NOT A" (μ replaced by 1 − μ). Empty = no negations.</summary>
    public bool[] Negated { get; set; } = [];
    public Connective Connective { get; set; } = Connective.And;
    /// <summary>Rule weight in [0, 1]; multiplies the firing strength.</summary>
    public double Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    /// <summary>Mamdani: one term index per output variable (−1 = the output is not mentioned).</summary>
    public int[] Consequent { get; set; } = [];
    /// <summary>Classifier: class index.</summary>
    public int Class { get; set; }
    /// <summary>Sugeno: y = p₀ (order 0) or y = p₀ + Σ pⱼ xⱼ (order 1).</summary>
    public double[] Coefficients { get; set; } = [];

    public bool IsNegated(int inputIndex) => inputIndex < Negated.Length && Negated[inputIndex];
}

public sealed class InferenceSettings
{
    public TNorm And { get; set; } = TNorm.Minimum;
    public SNorm Or { get; set; } = SNorm.Maximum;
    public Implication Implication { get; set; } = Implication.Minimum;
    /// <summary>Combines rule outputs: implied Mamdani sets, or classifier rule votes per class.</summary>
    public SNorm Aggregation { get; set; } = SNorm.Maximum;
    public Defuzzification Defuzzification { get; set; } = Defuzzification.Centroid;
    /// <summary>Number of grid points on each Mamdani output universe.</summary>
    public int Resolution { get; set; } = 1001;
    /// <summary>Two class scores closer than this are a tie (the object is ambiguous).</summary>
    public double TieTolerance { get; set; } = 1e-9;
}

/// <summary>Result of evaluating the system for one input vector.</summary>
public sealed record InferenceResult(
    /// <summary>Firing strength of every rule (0 for disabled rules).</summary>
    double[] Firing,
    /// <summary>Crisp value per output (Mamdani, Sugeno); NaN where no fired rule mentions the output.</summary>
    double[] Outputs,
    /// <summary>Per-class score: aggregated firing (classifier) or crisp output (Mamdani with one output per class).</summary>
    double[] ClassScores,
    /// <summary>Winning class index; −1 when no class has evidence or the best score is tied.</summary>
    int PredictedClass,
    /// <summary>True when two or more classes share the best score.</summary>
    bool Ambiguous,
    /// <summary>Sugeno: per-rule consequent value yₖ(x).</summary>
    double[] RuleOutputs,
    /// <summary>Mamdani: output grid and aggregated membership per output (null unless requested).</summary>
    double[][]? OutputGrids = null,
    double[][]? OutputMemberships = null)
{
    public double Output => Outputs.Length > 0 ? Outputs[0] : double.NaN;
    public bool Fired => Firing.Any(strength => strength > 0);
    public bool Rejected => PredictedClass < 0 && !Ambiguous;
}

/// <summary>
/// Fuzzy inference system: Mamdani (MIMO), Takagi–Sugeno (one output) or classifier.
/// Mutable so that an interactive editor can change rules and terms in place.
/// </summary>
public sealed class FuzzyInferenceSystem
{
    public string Name { get; set; } = "fis";
    public FisKind Kind { get; set; }
    /// <summary>Sugeno consequent order: 0 (constant) or 1 (linear).</summary>
    public int SugenoOrder { get; set; }
    public List<FuzzyVariable> Inputs { get; set; } = [];
    /// <summary>Output variables: Mamdani terms; for Sugeno a single variable naming the output. Empty for classifiers.</summary>
    public List<FuzzyVariable> Outputs { get; set; } = [];
    /// <summary>
    /// Class names. Required for <see cref="FisKind.Classifier"/>; for a Mamdani system with
    /// one output per class (output k = degree of belonging to class k) they enable a class decision.
    /// </summary>
    public List<string> Classes { get; set; } = [];
    public List<FuzzyRule> Rules { get; set; } = [];
    public InferenceSettings Settings { get; set; } = new();

    /// <summary>True when the system yields a class decision (classifier, or Mamdani with one output per class).</summary>
    public bool IsClassifying =>
        Kind == FisKind.Classifier || (Kind == FisKind.Mamdani && Classes.Count > 0 && Classes.Count == Outputs.Count);

    /// <summary>Weighted firing strength of one rule; 0 for disabled rules.</summary>
    public double FiringStrength(FuzzyRule rule, ReadOnlySpan<double> inputValues)
    {
        if (!rule.Enabled) return 0;
        bool isAnd = rule.Connective == Connective.And;
        double strength = isAnd ? 1 : 0;
        bool hasClause = false;
        for (int input = 0; input < Inputs.Count; input++)
        {
            int term = rule.Antecedent[input];
            if (term < 0) continue;
            double membership = Inputs[input].Terms[term].Mf.Evaluate(inputValues[input]);
            if (rule.IsNegated(input)) membership = 1 - membership;
            strength = isAnd ? Settings.And.Apply(strength, membership) : Settings.Or.Apply(strength, membership);
            hasClause = true;
        }
        // a rule with no antecedent clauses always fires fully
        return (hasClause ? strength : 1) * rule.Weight;
    }

    public InferenceResult Evaluate(ReadOnlySpan<double> inputValues, bool includeOutputSets = false)
    {
        if (inputValues.Length != Inputs.Count)
            throw new ArgumentException($"Expected {Inputs.Count} inputs, got {inputValues.Length}.", nameof(inputValues));
        Validate();

        var firing = new double[Rules.Count];
        for (int rule = 0; rule < Rules.Count; rule++) firing[rule] = FiringStrength(Rules[rule], inputValues);

        return Kind switch
        {
            FisKind.Classifier => EvaluateClassifier(firing),
            FisKind.Sugeno     => EvaluateSugeno(firing, inputValues),
            _                  => EvaluateMamdani(firing, includeOutputSets),
        };
    }

    private InferenceResult EvaluateClassifier(double[] firing)
    {
        var scores = new double[Classes.Count];
        for (int rule = 0; rule < Rules.Count; rule++)
        {
            if (firing[rule] <= 0) continue;
            int classIndex = Rules[rule].Class;
            scores[classIndex] = Settings.Aggregation.Apply(scores[classIndex], firing[rule]);
        }
        var (winner, tie) = Decide(scores, score => score > 0);
        return new InferenceResult(firing, [], scores, winner, tie, []);
    }

    private InferenceResult EvaluateSugeno(double[] firing, ReadOnlySpan<double> inputValues)
    {
        var ruleOutputs = new double[Rules.Count];
        double weightedSum = 0, firingSum = 0;
        for (int rule = 0; rule < Rules.Count; rule++)
        {
            var coefficients = Rules[rule].Coefficients;
            double ruleOutput = coefficients.Length > 0 ? coefficients[0] : 0;
            for (int term = 1; term < coefficients.Length && term <= inputValues.Length; term++)
                ruleOutput += coefficients[term] * inputValues[term - 1];
            ruleOutputs[rule] = ruleOutput;
            weightedSum += firing[rule] * ruleOutput;
            firingSum   += firing[rule];
        }
        return new InferenceResult(firing, [firingSum > 0 ? weightedSum / firingSum : double.NaN], [], -1, false, ruleOutputs);
    }

    private InferenceResult EvaluateMamdani(double[] firing, bool includeOutputSets)
    {
        int gridSize = System.Math.Max(Settings.Resolution, 2);
        var crisp = new double[Outputs.Count];
        var grids = includeOutputSets ? new double[Outputs.Count][] : null;
        var aggregatedSets = includeOutputSets ? new double[Outputs.Count][] : null;

        for (int outputIndex = 0; outputIndex < Outputs.Count; outputIndex++)
        {
            var output = Outputs[outputIndex];
            var grid = new double[gridSize];
            var aggregated = new double[gridSize];
            double step = (output.Max - output.Min) / (gridSize - 1);
            for (int point = 0; point < gridSize; point++) grid[point] = output.Min + point * step;

            for (int rule = 0; rule < Rules.Count; rule++)
            {
                int term = Rules[rule].Consequent[outputIndex];
                if (firing[rule] <= 0 || term < 0) continue;
                var consequentMf = output.Terms[term].Mf;
                for (int point = 0; point < gridSize; point++)
                    aggregated[point] = Settings.Aggregation.Apply(aggregated[point],
                        Settings.Implication.Apply(firing[rule], consequentMf.Evaluate(grid[point])));
            }

            crisp[outputIndex] = Settings.Defuzzification.Defuzzify(grid, aggregated);
            if (grids is not null) { grids[outputIndex] = grid; aggregatedSets![outputIndex] = aggregated; }
        }

        if (!IsClassifying)
            return new InferenceResult(firing, crisp, [], -1, false, [], grids, aggregatedSets);

        // output k is the degree of belonging to class k; an output no fired rule speaks about carries no evidence
        var (winner, tie) = Decide(crisp, value => !double.IsNaN(value));
        return new InferenceResult(firing, crisp, crisp, winner, tie, [], grids, aggregatedSets);
    }

    private (int Winner, bool Tie) Decide(double[] scores, Func<double, bool> hasEvidence)
    {
        int winner = -1;
        for (int classIndex = 0; classIndex < scores.Length; classIndex++)
            if (hasEvidence(scores[classIndex]) && (winner < 0 || scores[classIndex] > scores[winner])) winner = classIndex;
        if (winner < 0) return (-1, false);
        for (int classIndex = 0; classIndex < scores.Length; classIndex++)
            if (classIndex != winner && hasEvidence(scores[classIndex])
                && scores[winner] - scores[classIndex] <= Settings.TieTolerance)
                return (-1, true);
        return (winner, false);
    }

    /// <summary>Checks that every rule refers to existing terms / classes; throws otherwise.</summary>
    public void Validate()
    {
        if (Kind == FisKind.Sugeno && Outputs.Count > 1)
            throw new InvalidOperationException("A Sugeno system has a single output.");
        for (int ruleIndex = 0; ruleIndex < Rules.Count; ruleIndex++)
        {
            var rule = Rules[ruleIndex];
            int number = ruleIndex + 1;
            if (rule.Antecedent.Length != Inputs.Count)
                throw new InvalidOperationException($"Rule {number}: antecedent has {rule.Antecedent.Length} clauses, system has {Inputs.Count} inputs.");
            for (int input = 0; input < Inputs.Count; input++)
                if (rule.Antecedent[input] >= Inputs[input].Terms.Count)
                    throw new InvalidOperationException($"Rule {number}: input '{Inputs[input].Name}' has no term #{rule.Antecedent[input] + 1}.");
            switch (Kind)
            {
                case FisKind.Mamdani:
                    if (rule.Consequent.Length != Outputs.Count)
                        throw new InvalidOperationException($"Rule {number}: consequent has {rule.Consequent.Length} clauses, system has {Outputs.Count} outputs.");
                    for (int output = 0; output < Outputs.Count; output++)
                        if (rule.Consequent[output] >= Outputs[output].Terms.Count)
                            throw new InvalidOperationException($"Rule {number}: output '{Outputs[output].Name}' has no term #{rule.Consequent[output] + 1}.");
                    break;
                case FisKind.Classifier when rule.Class < 0 || rule.Class >= Classes.Count:
                    throw new InvalidOperationException($"Rule {number}: class #{rule.Class + 1} does not exist.");
                case FisKind.Sugeno when rule.Coefficients.Length != (SugenoOrder == 0 ? 1 : Inputs.Count + 1):
                    throw new InvalidOperationException($"Rule {number}: order-{SugenoOrder} consequent needs {(SugenoOrder == 0 ? 1 : Inputs.Count + 1)} coefficients.");
            }
        }
    }

    /// <summary>Human-readable rule text, e.g. "IF x1 is C1 AND x2 is C1 THEN class is setosa".</summary>
    public string Describe(FuzzyRule rule) => $"IF {DescribeAntecedent(rule)} THEN {DescribeConsequent(rule)}";

    public string DescribeAntecedent(FuzzyRule rule)
    {
        var clauses = new List<string>();
        for (int input = 0; input < Inputs.Count && input < rule.Antecedent.Length; input++)
        {
            int term = rule.Antecedent[input];
            if (term >= 0 && term < Inputs[input].Terms.Count)
                clauses.Add($"{Inputs[input].Name} is {(rule.IsNegated(input) ? "NOT " : "")}{Inputs[input].Terms[term].Name}");
        }
        return clauses.Count == 0 ? "always" : string.Join(rule.Connective == Connective.And ? " AND " : " OR ", clauses);
    }

    public string DescribeConsequent(FuzzyRule rule)
    {
        switch (Kind)
        {
            case FisKind.Classifier:
                return $"class is {(rule.Class >= 0 && rule.Class < Classes.Count ? Classes[rule.Class] : "?")}";
            case FisKind.Mamdani:
                var clauses = new List<string>();
                for (int output = 0; output < Outputs.Count && output < rule.Consequent.Length; output++)
                {
                    int term = rule.Consequent[output];
                    if (term >= 0 && term < Outputs[output].Terms.Count)
                        clauses.Add($"{Outputs[output].Name} is {Outputs[output].Terms[term].Name}");
                }
                return clauses.Count == 0 ? "nothing" : string.Join(" AND ", clauses);
            default:
                return $"{(Outputs.Count > 0 ? Outputs[0].Name : "y")} = {LinearText(rule.Coefficients)}";
        }
    }

    private string LinearText(double[] coefficients)
    {
        if (coefficients.Length == 0) return "0";
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var text = new System.Text.StringBuilder(coefficients[0].ToString("0.###", culture));
        for (int term = 1; term < coefficients.Length && term <= Inputs.Count; term++)
            text.Append(coefficients[term] < 0 ? " − " : " + ")
                .Append(System.Math.Abs(coefficients[term]).ToString("0.###", culture))
                .Append('·').Append(Inputs[term - 1].Name);
        return text.ToString();
    }
}
