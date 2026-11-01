using System.Globalization;
using System.Text;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Serialization;

/// <summary>
/// Writes a system in the MATLAB Fuzzy Logic Toolbox text format (<c>.fis</c>, version 2.0),
/// readable by <c>readfis</c>. Only operators that the toolbox knows by name are accepted.
/// Rules use the toolbox encoding: 1-based term indices, 0 = variable not used, negative = IS NOT,
/// then weight and connection (1 = AND, 2 = OR).
/// </summary>
public static class FisFileWriter
{
    public static string Write(FuzzyInferenceSystem fis)
    {
        if (fis.Kind == FisKind.Classifier)
            throw new NotSupportedException("The .fis format has no class consequents; export a Mamdani (allotment) system instead.");
        fis.Validate();
        var settings = fis.Settings;
        bool sugeno = fis.Kind == FisKind.Sugeno;
        var enabledRules = fis.Rules.Where(rule => rule.Enabled).ToList();
        var text = new StringBuilder();

        text.AppendLine("[System]")
            .AppendLine($"Name='{Escape(fis.Name)}'")
            .AppendLine(sugeno ? "Type='sugeno'" : "Type='mamdani'")
            .AppendLine("Version=2.0")
            .AppendLine($"NumInputs={fis.Inputs.Count}")
            .AppendLine($"NumOutputs={fis.Outputs.Count}")
            .AppendLine($"NumRules={enabledRules.Count}")
            .AppendLine($"AndMethod='{AndName(settings.And)}'")
            .AppendLine($"OrMethod='{OrName(settings.Or)}'")
            .AppendLine($"ImpMethod='{(sugeno || settings.Implication == Implication.Product ? "prod" : "min")}'")
            .AppendLine($"AggMethod='{(sugeno ? "sum" : OrName(settings.Aggregation))}'")
            .AppendLine($"DefuzzMethod='{(sugeno ? "wtaver" : DefuzzificationName(settings.Defuzzification))}'");

        for (int input = 0; input < fis.Inputs.Count; input++)
            WriteVariable(text, $"Input{input + 1}", fis.Inputs[input]);

        for (int output = 0; output < fis.Outputs.Count; output++)
        {
            var variable = fis.Outputs[output];
            if (!sugeno) { WriteVariable(text, $"Output{output + 1}", variable); continue; }

            // one constant / linear output MF per rule; MATLAB orders linear coefficients as [p1 … pm p0]
            text.AppendLine().AppendLine($"[Output{output + 1}]")
                .AppendLine($"Name='{Escape(variable.Name)}'")
                .AppendLine($"Range=[{Number(variable.Min)} {Number(variable.Max)}]")
                .AppendLine($"NumMFs={enabledRules.Count}");
            for (int ruleIndex = 0; ruleIndex < enabledRules.Count; ruleIndex++)
            {
                var coefficients = enabledRules[ruleIndex].Coefficients;
                string kind = fis.SugenoOrder == 0 ? "constant" : "linear";
                IEnumerable<double> values = fis.SugenoOrder == 0 ? [coefficients[0]] : coefficients.Skip(1).Append(coefficients[0]);
                string name = enabledRules[ruleIndex].Label ?? $"r{ruleIndex + 1}";
                text.AppendLine($"MF{ruleIndex + 1}='{Escape(name)}':'{kind}',[{string.Join(" ", values.Select(Number))}]");
            }
        }

        text.AppendLine().AppendLine("[Rules]");
        for (int ruleIndex = 0; ruleIndex < enabledRules.Count; ruleIndex++)
        {
            var rule = enabledRules[ruleIndex];
            var antecedent = rule.Antecedent.Select((term, input) => term < 0 ? 0 : (term + 1) * (rule.IsNegated(input) ? -1 : 1));
            IEnumerable<int> consequent = sugeno ? [ruleIndex + 1] : rule.Consequent.Select(term => term < 0 ? 0 : term + 1);
            text.Append(string.Join(" ", antecedent)).Append(", ")
                .Append(string.Join(" ", consequent))
                .Append(" (").Append(Number(rule.Weight)).Append(") : ")
                .AppendLine(rule.Connective == Connective.And ? "1" : "2");
        }
        return text.ToString();
    }

    private static void WriteVariable(StringBuilder text, string header, FuzzyVariable variable)
    {
        text.AppendLine().AppendLine($"[{header}]")
            .AppendLine($"Name='{Escape(variable.Name)}'")
            .AppendLine($"Range=[{Number(variable.Min)} {Number(variable.Max)}]")
            .AppendLine($"NumMFs={variable.Terms.Count}");
        for (int term = 0; term < variable.Terms.Count; term++)
            text.AppendLine($"MF{term + 1}='{Escape(variable.Terms[term].Name)}':{MfText(variable.Terms[term].Mf)}");
    }

    private static string MfText(MembershipFunction mf)
    {
        // MATLAB gaussmf takes [sigma c]
        IReadOnlyList<double> parameters = mf is GaussianMf gaussian ? [gaussian.Sigma, gaussian.Center] : mf.Parameters;
        string type = mf.Shape switch
        {
            MfShape.Triangular  => "trimf",
            MfShape.Trapezoidal => "trapmf",
            _                   => "gaussmf",
        };
        return $"'{type}',[{string.Join(" ", parameters.Select(Number))}]";
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string AndName(TNorm norm) => norm switch
    {
        TNorm.Minimum => "min",
        TNorm.Product => "prod",
        _ => throw new NotSupportedException($"T-norm {norm} has no .fis name (min | prod)."),
    };

    private static string OrName(SNorm conorm) => conorm switch
    {
        SNorm.Maximum          => "max",
        SNorm.ProbabilisticSum => "probor",
        _ => throw new NotSupportedException($"S-norm {conorm} has no .fis name (max | probor)."),
    };

    private static string DefuzzificationName(Defuzzification method) => method switch
    {
        Defuzzification.Centroid          => "centroid",
        Defuzzification.Bisector          => "bisector",
        Defuzzification.MeanOfMaximum     => "mom",
        Defuzzification.SmallestOfMaximum => "som",
        _                                 => "lom",
    };

    private static string Escape(string name) => name.Replace("'", "''");
}
