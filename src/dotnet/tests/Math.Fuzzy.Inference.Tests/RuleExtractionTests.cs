using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Evaluation;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.MembershipFunctions;
using MathFuzzy.Inference.Serialization;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Tests;

/// <summary>
/// Rule-base generation from D-AFC(c) clusters against the worked example of the monograph
/// (Viattchenin 2013, sec. 4.5.3): Sneath–Sokal data, c = 2, the clustering of sec. 2.2.6
/// (α* = 0.81944, object 5 in both clusters with μ = 0.92969 and 0.875).
/// </summary>
public class RuleExtractionTests
{
    private const int ObjectCount = 16;

    private static RawDataMatrix SneathSokal()
    {
        int[,] coordinates = { {0,4},{0,3},{1,5},{2,4},{3,3},{2,2},{2,1},{1,0},
                               {5,5},{6,5},{7,6},{5,3},{7,3},{6,2},{6,1},{8,1} };
        var data = new RawDataMatrix(ObjectCount, 2);
        for (int row = 0; row < ObjectCount; row++) { data[row, 0] = coordinates[row, 0]; data[row, 1] = coordinates[row, 1]; }
        return data;
    }

    private static ClusterResult SneathSokalAllotment()
    {
        var results = AlgorithmFactory.Run("dafc", SneathSokal(),
            new AlgorithmOptions(Distance: "squared-euclidean", Normalization: "max", ClusterCount: 2),
            TestContext.Current.CancellationToken);
        return results[0];
    }

    private static int[] SupportOf(ClusterResult result, int cluster) =>
        Enumerable.Range(0, ObjectCount).Where(objectIndex => result.Membership[objectIndex, cluster] >= result.Alpha).ToArray();

    /// <summary>Table 4.10: defuzzified (class 1, class 2) per object.</summary>
    private static readonly double[,] Table410 =
    {
        { .919, .0798 }, { .924, .0746 }, { .915, .0853 }, { .918, .0815 },
        { .500, .5000 }, { .918, .0815 }, { .918, .0815 }, { .915, .0853 },
        { .0824, .919 }, { .0824, .919 }, { .0853, .915 }, { .0808, .920 },
        { .0764, .925 }, { .0808, .920 }, { .0853, .915 }, { .0853, .915 },
    };

    [Fact]
    public void Clustering_IsTheOneOfSection226()
    {
        var result = SneathSokalAllotment();
        Assert.Equal(0.81944, result.Alpha, 5);
        Assert.Equal(0.92969, result.Membership[4, 0], 5);
        Assert.Equal(0.875, result.Membership[4, 1], 5);
    }

    [Fact]
    public void Antecedents_ReproduceInputRangesOfFig412()
    {
        var fis = ClusterRuleExtractor.Extract(SneathSokal(), SneathSokalAllotment(), new RuleExtractionOptions());

        // Fig. 4.12: x1 ∈ [0, 8.2203], x2 ∈ [−0.661, 6.661]
        Assert.Equal(0, fis.Inputs[0].Min, 4);
        Assert.Equal(8.2203, fis.Inputs[0].Max, 4);
        Assert.Equal(-0.6610, fis.Inputs[1].Min, 4);
        Assert.Equal(6.6610, fis.Inputs[1].Max, 4);
    }

    [Fact]
    public void Antecedent_PassesThroughOneMinusAlphaAtSupportBounds()
    {
        var data = SneathSokal();
        var result = SneathSokalAllotment();
        var fis = ClusterRuleExtractor.Extract(data, result, new RuleExtractionOptions());

        for (int cluster = 0; cluster < 2; cluster++)
        {
            var support = SupportOf(result, cluster);
            for (int feature = 0; feature < 2; feature++)
            {
                var mf = (TrapezoidalMf)fis.Inputs[feature].Terms[cluster].Mf;
                double supportMin = support.Min(objectIndex => data[objectIndex, feature]);
                double supportMax = support.Max(objectIndex => data[objectIndex, feature]);
                // (4.21)/(4.22) where the support bound differs from the core, (4.25)/(4.26) at the typical point
                if (supportMin < mf.LeftShoulder)  Assert.Equal(1 - result.Alpha, mf.Evaluate(supportMin), 9);
                if (supportMax > mf.RightShoulder) Assert.Equal(1 - result.Alpha, mf.Evaluate(supportMax), 9);
                Assert.Equal(data[result.Centers[cluster], feature], mf.LeftShoulder, 12);   // one typical point: triangle apex
            }
        }
    }

    [Fact]
    public void Consequents_AreHighAndLowTrapezoidsOfTheMonograph()
    {
        var result = SneathSokalAllotment();
        var fis = ClusterRuleExtractor.Extract(SneathSokal(), result, new RuleExtractionOptions());

        Assert.Equal(2, fis.Outputs.Count);
        // the clusters share object 5, so every rule speaks about both outputs: HIGH own, LOW other
        Assert.Equal([0, 1], fis.Rules[0].Consequent);
        Assert.Equal([1, 0], fis.Rules[1].Consequent);

        // C²₂ = (α, μ̲₂, μ̄₂, 1) with μ̲₂ = 0.875 (object 5), μ̄₂ = 1; C²₁ = (0, 1 − μ̄₂, 1 − μ̲₂, 1 − α)
        var high = (TrapezoidalMf)fis.Outputs[1].Terms[0].Mf;
        var low  = (TrapezoidalMf)fis.Outputs[1].Terms[1].Mf;
        Assert.Equal(new TrapezoidalMf(result.Alpha, 0.875, 1, 1), high with { LeftShoulder = System.Math.Round(high.LeftShoulder, 5) });
        Assert.Equal(new TrapezoidalMf(0, 0, 0.125, 1 - result.Alpha), low with { RightShoulder = System.Math.Round(low.RightShoulder, 5) });
    }

    [Fact]
    public void Outputs_AgreeWithTable410()
    {
        var data = SneathSokal();
        var fis = ClusterRuleExtractor.Extract(data, SneathSokalAllotment(), new RuleExtractionOptions());

        for (int row = 0; row < ObjectCount; row++)
        {
            var outputs = fis.Evaluate(data.GetRow(row)).Outputs;
            // the table is reproduced to 3.2e-3: the published values fit a consequent level of ≈ 0.83
            // rather than α* = 0.81944 (antecedents and decisions agree exactly, see the tests above)
            Assert.Equal(Table410[row, 0], outputs[0], 0.0035);
            Assert.Equal(Table410[row, 1], outputs[1], 0.0035);
        }
        Assert.Equal(0.5, fis.Evaluate(data.GetRow(4)).Outputs[0], 9);
        Assert.Equal(0.5, fis.Evaluate(data.GetRow(4)).Outputs[1], 9);
    }

    [Fact]
    public void Allotment_ClassifiesEveryObjectAsTheClustering_FifthIsDoubleClassified()
    {
        var data = SneathSokal();
        var fis = ClusterRuleExtractor.Extract(data, SneathSokalAllotment(), new RuleExtractionOptions());
        var truth = Enumerable.Range(0, ObjectCount).Select(row => row < 8 ? "C1" : "C2").ToArray();

        var report = FisEvaluator.Classify(fis, data, truth);

        Assert.Equal(15, report.Correct);
        Assert.Equal(1, report.Ambiguous);           // object 5 belongs to both classes
        Assert.Equal(1, report.Confusion[0, 2]);
    }

    [Fact]
    public void Classifier_UsesMajorityClassOfCluster()
    {
        var data = SneathSokal();
        var labels = Enumerable.Range(0, ObjectCount).Select(row => row < 8 ? 1 : 0).ToArray();   // "left" = 1, "right" = 0
        var fis = ClusterRuleExtractor.Extract(data, SneathSokalAllotment(), new RuleExtractionOptions
        {
            Kind = RuleBaseKind.Classifier, Labels = labels, ClassNames = ["right", "left"],
        });

        Assert.Equal(FisKind.Classifier, fis.Kind);
        Assert.Equal([1, 0], fis.Rules.Select(rule => rule.Class));
        var report = FisEvaluator.Classify(fis, data, labels.Select(label => label == 1 ? "left" : "right").ToArray());
        Assert.Equal(15, report.Correct);
        Assert.Equal(1, report.Ambiguous);
        Assert.Equal("IF x1 is C1 AND x2 is C1 THEN class is left", fis.Describe(fis.Rules[0]));
    }

    [Theory]
    [InlineData(RuleBaseKind.Mamdani)]
    [InlineData(RuleBaseKind.SugenoZero)]
    [InlineData(RuleBaseKind.SugenoFirst)]
    public void Regression_PredictsTargetColumn(RuleBaseKind kind)
    {
        var data = SneathSokal();
        var fis = ClusterRuleExtractor.Extract(data, SneathSokalAllotment(), new RuleExtractionOptions
        {
            Kind = kind, TargetColumn = 1, FeatureNames = ["u", "v"],
        });

        Assert.Single(fis.Inputs);
        Assert.Equal("u", fis.Inputs[0].Name);
        Assert.Equal("v", fis.Outputs[0].Name);
        var inputs = data.SelectColumns([0]);
        var targets = Enumerable.Range(0, ObjectCount).Select(row => data[row, 1]).ToArray();
        var report = FisEvaluator.Regress(fis, inputs, targets);

        Assert.Equal(ObjectCount, report.Evaluated);
        // sanity bound only: v has std ≈ 1.70 and barely depends on u, so no model does much better
        Assert.InRange(report.Rmse, 0, 2.5);
    }

    [Theory]
    [InlineData(MfShape.Triangular)]
    [InlineData(MfShape.Gaussian)]
    public void OtherShapes_KeepOneMinusAlphaAtTheWiderSupportBound(MfShape shape)
    {
        var data = SneathSokal();
        var result = SneathSokalAllotment();
        var fis = ClusterRuleExtractor.Extract(data, result, new RuleExtractionOptions { Shape = shape });
        var support = SupportOf(result, 0);
        var mf = fis.Inputs[0].Terms[0].Mf;
        double supportMin = support.Min(objectIndex => data[objectIndex, 0]);
        double supportMax = support.Max(objectIndex => data[objectIndex, 0]);

        Assert.Equal(shape, mf.Shape);
        Assert.Equal(1, mf.Evaluate(data[result.Centers[0], 0]), 9);
        Assert.Equal(1 - result.Alpha, System.Math.Min(mf.Evaluate(supportMin), mf.Evaluate(supportMax)), 9);
    }

    [Fact]
    public void Extract_RejectsResultsNotOverObjects()
    {
        var data = SneathSokal();
        var result = SneathSokalAllotment() with { Membership = new double[2, 2] };
        Assert.Throws<ArgumentException>(() => ClusterRuleExtractor.Extract(data, result, new RuleExtractionOptions()));
    }

    /// <summary>
    /// Sec. 4.5.3, second example: Anderson's Iris data, D-AFC(c = 3), fully separated clusters.
    /// The monograph reports the input ranges of Fig. 4.14 and "five objects incorrectly and
    /// two objects were double-classified ... rejected objects are absent".
    /// </summary>
    [Fact]
    public void Iris_ReproducesFig414RangesAndClassificationOfSection453()
    {
        var iris = LabeledDataset.TryFromCsv(DatasetPath("iris_labeled.csv"))!;
        var allotment = AlgorithmFactory.Run("dafc", iris.Data,
            new AlgorithmOptions(Distance: "squared-euclidean", Normalization: "max", ClusterCount: 3),
            TestContext.Current.CancellationToken)[0];

        var fis = ClusterRuleExtractor.Extract(iris.Data, allotment, new RuleExtractionOptions
        {
            Labels = iris.TrueLabels, ClassNames = iris.ClassNames,
        });

        double[,] fig414Ranges = { { 4.274, 7.907 }, { 1.9851, 4.4371 }, { 0.9814, 6.9297 }, { 0.0963, 2.5074 } };
        for (int input = 0; input < 4; input++)
        {
            Assert.Equal(fig414Ranges[input, 0], fis.Inputs[input].Min, 0.0005);
            Assert.Equal(fig414Ranges[input, 1], fis.Inputs[input].Max, 0.0005);
        }
        var report = FisEvaluator.Classify(fis, iris.Data, iris.TrueLabels.Select(label => iris.ClassNames[label]).ToArray());
        Assert.Equal((143, 5, 2, 0), (report.Correct, report.Misclassified, report.Ambiguous, report.Rejected));
    }

    private static string DatasetPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }

    [Fact]
    public void GeneratedSystem_ExportsToFisAndJson()
    {
        var fis = ClusterRuleExtractor.Extract(SneathSokal(), SneathSokalAllotment(), new RuleExtractionOptions { Name = "sneath" });
        var text = FisFileWriter.Write(fis);
        Assert.Contains("NumOutputs=2", text);
        Assert.Contains("MF2='low':'trapmf'", text);
        Assert.Contains("1 1, 1 2 (1) : 1", text);
        Assert.Equal(fis.Rules.Count, FisJson.Deserialize(FisJson.Serialize(fis)).Rules.Count);
    }
}
