using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Evaluation;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Operators;
using MathFuzzy.Inference.Serialization;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Tests;

public class InferenceSystemTests
{
    /// <summary>One input "x" on [0, 10] with terms low / high; one output "y" on [0, 10].</summary>
    private static FuzzyInferenceSystem TwoRuleMamdani() => new()
    {
        Kind = FisKind.Mamdani,
        Inputs =
        [
            new FuzzyVariable
            {
                Name = "x", Min = 0, Max = 10,
                Terms =
                [
                    new FuzzyTerm { Name = "low",  Mf = new TriangularMf(0, 0, 10) },
                    new FuzzyTerm { Name = "high", Mf = new TriangularMf(0, 10, 10) },
                ],
            },
        ],
        Outputs =
        [
            new FuzzyVariable
            {
                Name = "y", Min = 0, Max = 10,
                Terms =
                [
                    new FuzzyTerm { Name = "small", Mf = new TrapezoidalMf(0, 0, 2, 4) },
                    new FuzzyTerm { Name = "large", Mf = new TrapezoidalMf(6, 8, 10, 10) },
                ],
            },
        ],
        Rules =
        [
            new FuzzyRule { Antecedent = [0], Consequent = [0] },
            new FuzzyRule { Antecedent = [1], Consequent = [1] },
        ],
    };

    [Fact]
    public void Mamdani_IsSymmetricAndMonotone()
    {
        var fis = TwoRuleMamdani();
        Assert.Equal(5, fis.Evaluate([5]).Output, 3);           // equal firing of mirror-image rules
        double outputAtTwo = fis.Evaluate([2]).Output, outputAtEight = fis.Evaluate([8]).Output;
        Assert.Equal(10 - outputAtTwo, outputAtEight, 3);
        Assert.True(fis.Evaluate([0]).Output < outputAtTwo);
        Assert.Equal([0.8, 0.2], fis.Evaluate([2]).Firing.Select(strength => System.Math.Round(strength, 12)));
    }

    [Fact]
    public void Mamdani_SingleFiredRule_DefuzzifiesItsClippedSet()
    {
        var fis = TwoRuleMamdani();
        fis.Rules[1].Enabled = false;
        var result = fis.Evaluate([0], includeOutputSets: true);
        // only "small" (0, 0, 2, 4) fires with w = 1: centroid = (16 + 8 + 4) / (3·6) = 14/9
        Assert.Equal(14.0 / 9, result.Output, 3);
        Assert.Equal(0, result.Firing[1]);
        Assert.Equal(fis.Settings.Resolution, result.OutputGrids![0].Length);
    }

    [Fact]
    public void Mamdani_NoRuleFired_GivesNaN()
    {
        var fis = TwoRuleMamdani();
        fis.Inputs[0].Terms[0].Mf = new TriangularMf(0, 1, 2);
        fis.Inputs[0].Terms[1].Mf = new TriangularMf(8, 9, 10);
        Assert.True(double.IsNaN(fis.Evaluate([5]).Output));
        Assert.False(fis.Evaluate([5]).Fired);
    }

    [Fact]
    public void RuleWeight_And_Negation_ShapeFiring()
    {
        var fis = TwoRuleMamdani();
        fis.Rules[0].Weight = 0.5;
        fis.Rules[1].Negated = [true];
        var firing = fis.Evaluate([2]).Firing;
        Assert.Equal(0.4, firing[0], 12);   // 0.8 · 0.5
        Assert.Equal(0.8, firing[1], 12);   // 1 − 0.2
        Assert.Contains("NOT high", fis.Describe(fis.Rules[1]));
    }

    [Fact]
    public void OrConnective_UsesSNorm()
    {
        var fis = TwoRuleMamdani();
        fis.Inputs.Add(new FuzzyVariable { Name = "z", Min = 0, Max = 1, Terms = [new FuzzyTerm { Name = "any", Mf = new TriangularMf(0, 1, 1) }] });
        foreach (var rule in fis.Rules) rule.Antecedent = [rule.Antecedent[0], 0];
        fis.Rules[0].Connective = Connective.Or;
        fis.Settings.Or = SNorm.ProbabilisticSum;
        var firing = fis.Evaluate([2, 0.5]).Firing;
        Assert.Equal(0.8 + 0.5 - 0.4, firing[0], 12);
        Assert.Equal(0.2, firing[1], 12);   // AND with min
    }

    [Fact]
    public void Sugeno_IsFiringWeightedAverage()
    {
        var fis = TwoRuleMamdani();
        fis.Kind = FisKind.Sugeno;
        fis.SugenoOrder = 1;
        fis.Outputs[0].Terms.Clear();
        fis.Rules[0].Coefficients = [1, 0];    // y = 1
        fis.Rules[1].Coefficients = [0, 2];    // y = 2x
        var result = fis.Evaluate([2]);
        Assert.Equal((0.8 * 1 + 0.2 * 4) / 1.0, result.Output, 12);
        Assert.Equal([1.0, 4.0], result.RuleOutputs);
    }

    [Fact]
    public void FirstOrderFit_RecoversLinearTargetExactly()
    {
        var fis = TwoRuleMamdani();
        fis.Kind = FisKind.Sugeno;
        fis.SugenoOrder = 1;
        foreach (var rule in fis.Rules) rule.Coefficients = [0, 0];
        var inputs = RawDataMatrix.FromArray(Enumerable.Range(0, 11).Select(value => (double)value).ToArray().ToColumn());
        var targets = Enumerable.Range(0, 11).Select(value => 3 - 0.5 * value).ToArray();

        ConsequentFitter.FitSugeno(fis, inputs, targets);

        Assert.All(fis.Rules, rule =>
        {
            Assert.Equal(3, rule.Coefficients[0], 6);
            Assert.Equal(-0.5, rule.Coefficients[1], 6);
        });
        Assert.Equal(0, FisEvaluator.Regress(fis, inputs, targets).Rmse, 6);
    }

    [Fact]
    public void Classifier_PicksStrongestClass_ReportsTiesAndRejects()
    {
        var fis = TwoRuleMamdani();
        fis.Kind = FisKind.Classifier;
        fis.Outputs.Clear();
        fis.Classes = ["a", "b"];
        fis.Rules[0].Class = 0;
        fis.Rules[1].Class = 1;

        Assert.Equal(0, fis.Evaluate([2]).PredictedClass);
        Assert.Equal(1, fis.Evaluate([9]).PredictedClass);
        var tie = fis.Evaluate([5]);
        Assert.True(tie.Ambiguous);
        Assert.Equal(-1, tie.PredictedClass);

        fis.Inputs[0].Terms[1].Mf = new TriangularMf(8, 9, 10);
        fis.Inputs[0].Terms[0].Mf = new TriangularMf(0, 1, 2);
        Assert.True(fis.Evaluate([5]).Rejected);

        var inputs = RawDataMatrix.FromArray(new double[,] { { 1 }, { 9 }, { 5 }, { 1.5 } });
        var report = FisEvaluator.Classify(fis, inputs, ["a", "b", "a", "b"]);
        Assert.Equal((2, 1, 0, 1), (report.Correct, report.Misclassified, report.Ambiguous, report.Rejected));
        Assert.Equal(1, report.Confusion[1, 0]);   // true b → predicted a
        Assert.Equal(1, report.Confusion[0, 3]);   // true a → rejected
        Assert.Equal(0.5, report.Accuracy);
    }

    [Fact]
    public void Validate_RejectsDanglingTermIndex()
    {
        var fis = TwoRuleMamdani();
        fis.Rules[0].Antecedent = [5];
        Assert.Throws<InvalidOperationException>(() => fis.Evaluate([1]));
    }

    [Fact]
    public void Json_RoundTripPreservesBehaviour()
    {
        var fis = TwoRuleMamdani();
        fis.Rules[1].Negated = [true];
        fis.Settings.Defuzzification = Defuzzification.Bisector;
        fis.Inputs[0].Terms[1].Mf = new GaussianMf(10, 3);

        var json = FisJson.Serialize(fis);
        var restored = FisJson.Deserialize(json);

        Assert.Contains("\"type\": \"gaussian\"", json);
        Assert.Contains("\"defuzzification\": \"bisector\"", json);
        Assert.Contains("\"leftShoulder\": 0", json);
        // derived members (parameters, support, …) are not part of the document
        Assert.DoesNotContain("\"parameters\"", json);
        Assert.DoesNotContain("\"support\"", json);
        Assert.DoesNotContain("\"coreCenter\"", json);
        foreach (var input in new[] { 0.0, 2.5, 7.0 })
            Assert.Equal(fis.Evaluate([input]).Output, restored.Evaluate([input]).Output, 12);
    }

    [Fact]
    public void FisFile_WritesMatlabSections()
    {
        var fis = TwoRuleMamdani();
        fis.Rules[1].Negated = [true];
        var text = FisFileWriter.Write(fis);

        Assert.Contains("Type='mamdani'", text);
        Assert.Contains("DefuzzMethod='centroid'", text);
        Assert.Contains("MF1='low':'trimf',[0 0 10]", text);
        Assert.Contains("MF2='large':'trapmf',[6 8 10 10]", text);
        Assert.Contains("1, 1 (1) : 1", text);
        Assert.Contains("-2, 2 (1) : 1", text);

        fis.Settings.And = TNorm.Lukasiewicz;
        Assert.Throws<NotSupportedException>(() => FisFileWriter.Write(fis));
    }
}

internal static class ArrayExtensions
{
    public static double[,] ToColumn(this double[] values)
    {
        var column = new double[values.Length, 1];
        for (int row = 0; row < values.Length; row++) column[row, 0] = values[row];
        return column;
    }
}
