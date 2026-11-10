using System.Globalization;
using System.IO;
using System.Text.Json;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Figures;
using MathFuzzy.Inference.Generation;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

// mfuzzy-figures --data <datasets dir> --performance <measurements.jsonl> --out <figures dir>
//
// Draws every figure of the book from the data sets and the measurements of the repository,
// with the same library calls the reported results come from. Each figure is written as
// <name>.svg (vector, for HTML) and <name>.png (300 dpi, for print).

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var arguments = args.ToList();
string datasetsFolder = OptionValue("--data") ?? Path.Combine("..", "..", "datasets");
string performanceFile = OptionValue("--performance") ?? Path.Combine("..", "..", "results", "performance", "measurements.jsonl");
string outputFolder = OptionValue("--out") ?? "figures";
Directory.CreateDirectory(outputFolder);

// WPF rendering (the PNG exporter) needs a single-threaded apartment.
Exception? failure = null;
var worker = new Thread(() =>
{
    try { DrawAll(); }
    catch (Exception error) { failure = error; }
});
worker.SetApartmentState(ApartmentState.STA);
worker.Start();
worker.Join();
if (failure is not null)
{
    Console.Error.WriteLine($"Error: {failure.Message}");
    return 1;
}
return 0;

void DrawAll()
{
    var monograph = new AlgorithmOptions(Distance: "squared-euclidean", Normalization: "max");
    var unitarised = new AlgorithmOptions(Distance: "euclidean", Normalization: "minmax");

    // ── chapter 1: the five-object example ─────────────────────────────────────────────
    var example = new FuzzyToleranceMatrix(5);
    example[0, 1] = 0.8; example[0, 2] = 0.0; example[0, 3] = 0.1; example[0, 4] = 0.2;
    example[1, 2] = 0.4; example[1, 3] = 0.0; example[1, 4] = 0.9;
    example[2, 3] = 0.0; example[2, 4] = 0.0;
    example[3, 4] = 0.5;
    Save(Drawing.CutGraphs(example, [0.2, 0.5, 0.8], typicalPoints: [[], [0, 2, 3], [1, 2, 3]]), "example-cut-graphs", 900, 360);

    // ── chapters 2–3: Sneath–Sokal and Yang–Wu ──────────────────────────────────────────
    var sneath = DataFile.Load(Path.Combine(datasetsFolder, "sneath_sokal.csv")).Data;
    var sneathPrincipal = AlgorithmFactory.Run("dpafc", sneath, monograph)[0];
    var sneathClosure = AlgorithmFactory.Run("dafc-tc", sneath, monograph)[0];
    Save(Drawing.Scatter(sneath, 0, 1, sneathPrincipal, "x¹", "x²", labelObjects: true), "sneath-dpafc", 620, 520);
    Save(Drawing.Scatter(sneath, 0, 1, sneathClosure, "x¹", "x²", labelObjects: true), "sneath-dafc-tc", 620, 520);

    var sneathTolerance = AlgorithmFactory.CreateToleranceBuilder(monograph).Build(sneath);
    var sneathClosureMatrix = new MaxMinTransitiveClosure().Compute(sneathTolerance);
    double sneathLeap = LeapHeuristicAlphaSelector.SelectFrom(sneathClosureMatrix.GetDistinctValues())!.Value;
    Save(Drawing.Dendrogram(sneathClosureMatrix, sneathLeap), "sneath-dendrogram", 900, 520);

    var yangWu = DataFile.Load(Path.Combine(datasetsFolder, "yang_wu.csv")).Data;
    Save(Drawing.Scatter(yangWu, 0, 1, AlgorithmFactory.Run("dafc-tc", yangWu, monograph)[0], "x¹", "x²", labelObjects: true),
         "yangwu-dafc-tc", 620, 520);

    // ── Iris: memberships of D-AFC(3), levels of the closure, number of classes ─────────
    var irisFile = DataFile.Load(Path.Combine(datasetsFolder, "iris_labeled.csv"));
    var iris = irisFile.Data;
    var irisBest = AlgorithmFactory.Run("dafc", iris, monograph with { ClusterCount = 3, Accuracy = 0.00001 })[0];
    Save(Drawing.MembershipProfiles(irisBest), "iris-memberships", 900, 420);
    Save(Drawing.Scatter(iris, 2, 3, irisBest, "petal length, cm", "petal width, cm", labelObjects: false), "iris-dafc3", 620, 520);

    var irisClosure = new MaxMinTransitiveClosure().Compute(AlgorithmFactory.CreateToleranceBuilder(unitarised).Build(iris));
    Save(Drawing.LevelSequence(irisClosure), "iris-closure-levels", 900, 420);
    Save(Drawing.ClassCountProfile(irisClosure), "iris-class-count", 900, 420);

    // ── shapes that centroid-based methods miss ────────────────────────────────────────
    foreach (var (file, name) in new[]
             {
                 ("sipu_spiral_labeled.csv", "spiral"), ("graves_zigzag_labeled.csv", "zigzag"),
                 ("sipu_pathbased_labeled.csv", "pathbased"), ("graves_zigzag_outliers_labeled.csv", "zigzag-outliers"),
             })
    {
        var shape = DataFile.Load(Path.Combine(datasetsFolder, file)).Data;
        string algorithm = name == "zigzag-outliers" ? "dpafc-tc" : "dafc-tc";
        Save(Drawing.Scatter(shape, 0, 1, AlgorithmFactory.Run(algorithm, shape, unitarised)[0], "x¹", "x²", labelObjects: false),
             $"shape-{name}", 620, 520);
    }

    // ── chapter 8: cluster size and bounds ─────────────────────────────────────────────
    var tamura = FuzzyToleranceMatrix.FromCsv(Path.Combine(datasetsFolder, "tamura_16.csv"));
    Save(Drawing.SizeMeasures(ClusterSizeSelection.Scan(tamura, 3, 8, "lmf", F1Criterion.Instance)), "size-measures-tamura", 760, 420);
    Save(Drawing.SizeMeasures(ClusterSizeSelection.Scan(sneathTolerance, 3, 8, "lmf", F1Criterion.Instance)), "size-measures-sneath", 760, 420);
    Save(Drawing.Bounds(150, 2, 0.80384), "cluster-count-bounds", 760, 420);

    // ── chapter 7: antecedents of the rules for Iris ───────────────────────────────────
    var rules = ClusterRuleExtractor.Extract(iris, irisBest, new RuleExtractionOptions { FeatureNames = irisFile.FeatureNames });
    Save(Drawing.Antecedents(rules, inputIndex: 2, "petal length, cm"), "iris-antecedents", 760, 380);

    // ── chapter 9: measurements ─────────────────────────────────────────────────────────
    if (File.Exists(performanceFile))
    {
        var measurements = File.ReadLines(performanceFile).Where(line => line.Length > 0)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToList();
        Save(Drawing.ClosureTimes(measurements), "closure-times", 760, 460);
        Save(Drawing.SearchTimes(measurements), "search-times", 860, 380);
    }
    else
    {
        Console.Error.WriteLine($"No measurements at {performanceFile}: the timing figures are skipped.");
    }
}

void Save(PlotModel model, string name, int width, int height)
{
    using (var svg = File.Create(Path.Combine(outputFolder, name + ".svg")))
        new SvgExporter { Width = width, Height = height }.Export(model, svg);
    using (var png = File.Create(Path.Combine(outputFolder, name + ".png")))
        new OxyPlot.Wpf.PngExporter { Width = width * 3, Height = height * 3, Resolution = 288 }.Export(model, png);   // 3 × 96 dpi
    Console.WriteLine($"  {name}");
}

string? OptionValue(string name)
{
    int position = arguments.IndexOf(name);
    return position >= 0 && position + 1 < arguments.Count ? arguments[position + 1] : null;
}
