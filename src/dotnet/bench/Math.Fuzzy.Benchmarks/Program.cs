using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using MathFuzzy.Benchmarks;
using MathFuzzy.Clustering.Algorithms.Hierarchical;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

// mfuzzy-bench <closure|search|bound|hafc|stages|all> --data <datasets dir> --out <results.jsonl> [--single-core] [--skip-slow]
//
// Every experiment times variants that differ in one technique and checks that they
// produce the same result. --single-core pins the process to one core, so the
// library's parallel code paths can be timed without parallelism.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
var arguments = args.ToList();
string experiment = arguments.FirstOrDefault(argument => !argument.StartsWith("--")) ?? "all";
string datasetsFolder = OptionValue("--data") ?? Path.Combine("..", "..", "datasets");
string outputPath = OptionValue("--out") ?? "benchmark-results.jsonl";
bool singleCore = arguments.Contains("--single-core");
bool skipSlow = arguments.Contains("--skip-slow");

if (singleCore && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
    Process.GetCurrentProcess().ProcessorAffinity = 1;
int cores = singleCore ? 1 : Environment.ProcessorCount;

var log = new MeasurementLog(outputPath);
Console.WriteLine($"Processor: {ProcessorName()}, logical cores used: {cores}, {RuntimeInformation.FrameworkDescription}, " +
                  $"Vector<double>: {System.Numerics.Vector<double>.Count} lanes, AVX2: {Avx2.IsSupported}, AVX-512F: {Avx512F.IsSupported}");

var iris = RawDataMatrix.FromCsv(Path.Combine(datasetsFolder, "iris.csv"));
var irisMatrix = new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance).Build(iris);

if (experiment is "closure" or "all") RunClosure();
if (experiment is "search" or "all") RunSearch();
if (experiment is "bound" or "all") RunExactCoverBound();
if (experiment is "earlystop") RunEarlyStopOnly();
if (experiment is "hafc" or "all") RunHierarchical();
if (experiment is "stages" or "all") RunStages();
return;

void RunClosure()
{
    var inputs = new List<(string Name, FuzzyToleranceMatrix Matrix)> { ("iris", irisMatrix) };
    foreach (int size in new[] { 500, 1000, 2000 })
        inputs.Add(($"blobs{size}", BuildMatrix(SyntheticBlobs(size))));

    foreach (var (name, matrix) in inputs)
    {
        var reference = new MaxMinTransitiveClosure().Compute(matrix);
        int repeats = matrix.Size switch { <= 500 => 15, <= 1000 => 7, _ => 3 };

        var variants = new List<(string Variant, Func<FuzzyToleranceMatrix> Run)>
        {
            ("Floyd–Warshall, scalar, 1 thread",      () => ClosureVariants.ScalarSequential(matrix)),
            ("Floyd–Warshall, SIMD 128-bit, 1 thread", () => ClosureVariants.VectorSequential(matrix, 128)),
            ("Floyd–Warshall, SIMD 256-bit, 1 thread", () => ClosureVariants.VectorSequential(matrix, 256)),
            ("Floyd–Warshall, scalar, parallel",       () => ClosureVariants.ScalarParallel(matrix)),
            ("Floyd–Warshall, SIMD 256-bit, parallel (former library)", () => ClosureVariants.VectorParallel(matrix)),
            ("Spanning tree, 1 thread",                () => ClosureVariants.SpanningTree(matrix, parallel: false)),
            ("Spanning tree, parallel (library)",      () => new MaxMinTransitiveClosure().Compute(matrix)),
        };
        if (Vector512.IsHardwareAccelerated)
            variants.Insert(3, ("Floyd–Warshall, SIMD 512-bit, 1 thread", () => ClosureVariants.VectorSequential(matrix, 512)));

        foreach (var (variant, run) in variants)
        {
            // single-core runs only need the variants whose parallelism they remove
            if (singleCore && !variant.Contains("parallel") && !variant.Contains("library")) continue;
            double difference = ClosureVariants.MaxDifference(run(), reference);
            if (difference > 0.0)
                throw new InvalidOperationException($"{variant} on {name} differs from the library closure by {difference}");
            var (median, minimum, maximum) = WallClock.Measure(() => run(), repeats);
            log.Add(new Measurement("closure", variant, name, matrix.Size, cores, repeats, median, minimum, maximum));
        }
    }
}

void RunSearch()
{
    const int clusterCount = 3;
    var levels = irisMatrix.GetDistinctValues();

    // library: level pre-filter + bit-set engine + branch and bound + exact-cover bound (+ parallel levels)
    var libraryStatistics = new SearchDiagnostics();
    var library = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0),
                                    AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
    var libraryBest = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0, Diagnostics: libraryStatistics),
                                        AllDistinctAlphaSelector.Instance, F1Criterion.Instance).Run(irisMatrix)[0];
    var (median, minimum, maximum) = WallClock.Measure(() => library.Run(irisMatrix), 5);
    log.Add(new Measurement("search", "Bit-set engine + pre-filter + exact-cover bound (library)", "iris", irisMatrix.Size, cores, 5,
                            median, minimum, maximum,
                            Note: $"{libraryStatistics.LevelsSearched} of {levels.Count} levels searched, {libraryStatistics.NodesVisited:N0} nodes"));

    // the same engine without the exact-cover bound (the library of version 1.0.0)
    var plainStatistics = new SearchDiagnostics();
    var plain = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0, ExactCoverPruning: false),
                                  AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
    var plainBest = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0, ExactCoverPruning: false,
                                                      Diagnostics: plainStatistics),
                                      AllDistinctAlphaSelector.Instance, F1Criterion.Instance).Run(irisMatrix)[0];
    if (plainBest.Alpha != libraryBest.Alpha || plainBest.Score != libraryBest.Score
        || !plainBest.Centers.SequenceEqual(libraryBest.Centers))
        throw new InvalidOperationException("The exact-cover bound changed the best allotment on Iris.");
    (median, minimum, maximum) = WallClock.Measure(() => plain.Run(irisMatrix), 5);
    log.Add(new Measurement("search", "Bit-set engine + pre-filter", "iris", irisMatrix.Size, cores, 5,
                            median, minimum, maximum,
                            Note: $"{plainStatistics.LevelsSearched} of {levels.Count} levels searched, {plainStatistics.NodesVisited:N0} nodes"));
    if (singleCore) return;

    // early stopping: levels scanned in descending order, stop after a block of
    // 8 levels without improvement once a solution exists (a heuristic, off by default)
    var earlyStop = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0, EarlyStop: true),
                                      AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
    var earlyBest = earlyStop.Run(irisMatrix)[0];
    (median, minimum, maximum) = WallClock.Measure(() => earlyStop.Run(irisMatrix), 5);
    log.Add(new Measurement("search", "Bit-set engine + early stopping (heuristic)", "iris", irisMatrix.Size, cores, 5,
                            median, minimum, maximum,
                            Note: $"best F1 = {earlyBest.Score:F6} at α = {earlyBest.Alpha:F5} vs exhaustive F1 = {libraryBest.Score:F6} at α = {libraryBest.Alpha:F5}"));

    // pre-filter + plain enumeration over all levels
    // runs for minutes, so a single run without warm-up (all code is fully optimised from the start)
    SearchOutcome filtered = default!;
    double filteredMilliseconds = WallClock.MeasureOnce(
        () => filtered = SearchVariants.Enumerate(irisMatrix, levels, clusterCount, preFilter: true));
    log.Add(new Measurement("search", "Pre-filter + plain enumeration", "iris", irisMatrix.Size, 1, 1,
                            filteredMilliseconds, filteredMilliseconds, filteredMilliseconds,
                            Note: $"{filtered.LevelsSearched} of {levels.Count} levels searched, {filtered.CombinationsChecked:N0} combinations"));

    bool sameBest = Math.Abs(filtered.Alpha - libraryBest.Alpha) < 1e-12
                    && Math.Abs(filtered.Score - libraryBest.Score) < 1e-9
                    && filtered.Centres.Order().SequenceEqual(libraryBest.Centers.Order());
    Console.WriteLine($"  best allotment: library α={libraryBest.Alpha:F5} F1={libraryBest.Score:F6} [{string.Join(",", libraryBest.Centers)}], " +
                      $"enumeration α={filtered.Alpha:F5} F1={filtered.Score:F6} [{string.Join(",", filtered.Centres)}] → {(sameBest ? "identical" : "DIFFERENT")}");

    // plain enumeration of all C(n, c) combinations: timed on evenly spaced levels and extrapolated
    const int sampleSize = 24;
    var sample = Enumerable.Range(0, sampleSize).Select(index => levels[index * (levels.Count - 1) / (sampleSize - 1)]).ToList();
    double sampleMilliseconds = WallClock.MeasureOnce(() => SearchVariants.Enumerate(irisMatrix, sample, clusterCount, preFilter: false));
    double perLevel = sampleMilliseconds / sampleSize;
    log.Add(new Measurement("search", "Plain enumeration of all combinations", "iris", irisMatrix.Size, 1, 1,
                            perLevel * levels.Count, perLevel * levels.Count, perLevel * levels.Count, Extrapolated: true,
                            Note: $"{perLevel:F1} ms per level on {sampleSize} evenly spaced levels × {levels.Count} levels"));
}

// The exact-cover bound on larger data: D-AFC(3) with the automatic relaxation of w.
void RunExactCoverBound()
{
    if (singleCore) return;
    const int clusterCount = 3;
    foreach (string name in skipSlow ? new[] { "seeds" } : new[] { "seeds", "wine", "penguins" })
    {
        var matrix = BuildMatrix(RawDataMatrix.FromCsv(Path.Combine(datasetsFolder, name + "_labeled.csv")));
        ClusterResult? reference = null;
        foreach (bool bound in new[] { true, false })
        {
            var statistics = new SearchDiagnostics();
            var algorithm = new DAfcAlgorithm(
                // no node budget: both variants must finish every pass, or the comparison is not like for like
                new DAfcOptions(clusterCount, MaxCombinations: long.MaxValue, ExactCoverPruning: bound, Diagnostics: statistics),
                AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
            ClusterResult best = null!;
            double elapsed = WallClock.MeasureOnce(() => best = algorithm.Run(matrix)[0]);
            if (reference is null) reference = best;
            else if (best.Alpha != reference.Alpha || best.Score != reference.Score || !best.Centers.SequenceEqual(reference.Centers))
                throw new InvalidOperationException($"The exact-cover bound changed the best allotment on {name}.");
            log.Add(new Measurement("bound", bound ? "With the exact-cover bound (library)" : "Without the exact-cover bound",
                                    name, matrix.Size, cores, 1, elapsed, elapsed, elapsed,
                                    Note: $"w = {best.IntersectionCount}, {statistics.NodesVisited:N0} nodes" +
                                          (statistics.BudgetExhausted ? ", node budget exhausted" : "")));
        }
    }
}

void RunEarlyStopOnly()
{
    const int clusterCount = 3;
    var exhaustive = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0),
                                       AllDistinctAlphaSelector.Instance, F1Criterion.Instance).Run(irisMatrix)[0];
    var earlyStop = new DAfcAlgorithm(new DAfcOptions(clusterCount, MaxIntersections: 0, EarlyStop: true),
                                      AllDistinctAlphaSelector.Instance, F1Criterion.Instance);
    var earlyBest = earlyStop.Run(irisMatrix)[0];
    var (median, minimum, maximum) = WallClock.Measure(() => earlyStop.Run(irisMatrix), 5);
    log.Add(new Measurement("search", "Bit-set engine + early stopping (heuristic)", "iris", irisMatrix.Size, cores, 5,
                            median, minimum, maximum,
                            Note: $"best F1 = {earlyBest.Score:F6} at α = {earlyBest.Alpha:F5} vs exhaustive F1 = {exhaustive.Score:F6} at α = {exhaustive.Alpha:F5}"));
}

void RunHierarchical()
{
    var builder = new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance);
    var algorithm = new HAfcTcAlgorithm(builder, new MaxMinTransitiveClosure(), F1Criterion.Instance);
    int levelCount = algorithm.Run(iris).Levels.Count;
    var (median, minimum, maximum) = WallClock.Measure(() => algorithm.Run(iris), 5);
    log.Add(new Measurement("hafc", "Best typical point per class (library)", "iris", iris.Rows, cores, 5,
                            median, minimum, maximum, Note: $"{levelCount} levels"));
    if (singleCore || skipSlow) return;

    // the former approach: a D-AFC combination search on every level of the closure,
    // bounded by the default budget of 5·10⁸ search nodes per level
    var closure = new MaxMinTransitiveClosure().Compute(irisMatrix);
    var closureLevels = closure.GetDistinctValues().Order().ToList();
    double elapsed = WallClock.MeasureOnce(() =>
    {
        foreach (double alpha in closureLevels)
        {
            int classCount = ClassCount(closure, alpha);
            if (classCount < 2) continue;
            new DAfcAlgorithm(new DAfcOptions(classCount, ExactCoverPruning: false), new ManualAlphaSelector([alpha]), F1Criterion.Instance)
                .RunForAlpha(closure, alpha);
        }
    });
    log.Add(new Measurement("hafc", "Combination search per level (budget 5·10⁸)", "iris", iris.Rows, cores, 1,
                            elapsed, elapsed, elapsed, Note: $"{closureLevels.Count} levels"));
}

void RunStages()
{
    foreach (var (name, data) in new[] { ("iris", iris), ("blobs2000", SyntheticBlobs(2000)) })
    {
        var builder = new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance);
        FuzzyToleranceMatrix matrix = builder.Build(data);
        var (median, minimum, maximum) = WallClock.Measure(() => builder.Build(data), 5);
        log.Add(new Measurement("stages", "Tolerance matrix T", name, data.Rows, cores, 5, median, minimum, maximum));
        (median, minimum, maximum) = WallClock.Measure(() => new MaxMinTransitiveClosure().Compute(matrix), 5);
        log.Add(new Measurement("stages", "Transitive closure (library)", name, data.Rows, cores, 5, median, minimum, maximum));
        int levelCount = 0;
        (median, minimum, maximum) = WallClock.Measure(() => levelCount = matrix.GetDistinctValues().Count, 5);
        log.Add(new Measurement("stages", "Distinct α-levels of T", name, data.Rows, cores, 5, median, minimum, maximum,
                                Note: $"{levelCount} levels"));
    }
}

static FuzzyToleranceMatrix BuildMatrix(RawDataMatrix data) =>
    new ToleranceMatrixBuilder(EuclideanDistance.Instance, MinMaxNormalization.Instance).Build(data);

// Three Gaussian groups in four dimensions with a fixed seed, as a stand-in for larger data sets.
static RawDataMatrix SyntheticBlobs(int objectCount)
{
    var random = new Random(20261028);
    double[][] groupCentres = [[0, 0, 0, 0], [4, 4, 0, 1], [0, 5, 3, 2]];
    var data = new RawDataMatrix(objectCount, 4);
    for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
    {
        var centre = groupCentres[objectIndex % groupCentres.Length];
        for (int feature = 0; feature < 4; feature++)
        {
            // Box–Muller
            double gaussian = Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble())) * Math.Cos(2.0 * Math.PI * random.NextDouble());
            data[objectIndex, feature] = centre[feature] + gaussian;
        }
    }
    return data;
}

// Number of equivalence classes of the α-cut of a transitive (closure) matrix.
static int ClassCount(FuzzyToleranceMatrix closure, double alpha)
{
    var assigned = new bool[closure.Size];
    int classes = 0;
    for (int seed = 0; seed < closure.Size; seed++)
    {
        if (assigned[seed]) continue;
        classes++;
        var row = closure.GetRow(seed);
        for (int member = 0; member < closure.Size; member++)
            if (row[member] >= alpha) assigned[member] = true;
    }
    return classes;
}

static string ProcessorName()
{
    if (!OperatingSystem.IsWindows()) return RuntimeInformation.ProcessArchitecture.ToString();
    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
    return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "unknown";
}

string? OptionValue(string name)
{
    int position = arguments.IndexOf(name);
    return position >= 0 && position + 1 < arguments.Count ? arguments[position + 1] : null;
}
