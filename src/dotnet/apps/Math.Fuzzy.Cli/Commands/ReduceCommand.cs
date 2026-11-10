using System.CommandLine;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy reduce --method dpafc-tc --input data.csv --output reduced.csv     (attribute selection)
/// mfuzzy reduce --algorithm dafc --clusters 3 --input data.csv               (reduction of large data through interval-valued representatives)
/// </summary>
public static class ReduceCommand
{
    public static Command Build()
    {
        var method    = new Option<string>("--method") { Description = "Attribute selection: method dpafc | dpafc-tc | dpafc-taga", DefaultValueFactory = _ => "dpafc" };
        var input     = new Option<FileInfo>("--input") { Description = "CSV file with attribute data (objects in rows)", Required = true };
        var distance  = new Option<string>("--distance") { Description = "Distance between attributes: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var norm      = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var criterion = new Option<string>("--criterion") { Description = "Quality criterion: f1 | f2", DefaultValueFactory = _ => "f1" };
        var taga      = new Option<string?>("--taga") { Description = "Aggregation operator (dpafc-taga only, or a TAGA key with --algorithm): auto | max | min | mean | median | umedian | dmedian (default: auto)" };
        var output    = new Option<FileInfo?>("--output") { Description = "Attribute selection: output CSV with the selected attributes (default: only the selection is printed). With --algorithm: the report file (default: stdout)" };

        var algorithm  = new Option<string?>("--algorithm") { Description = "Reduction of large data: cluster the objects through interval-valued representatives with this algorithm key (as in mfuzzy cluster); without it the command selects attributes" };
        var clusters   = new Option<int?>("--clusters") { Description = "--algorithm: number of clusters c" };
        var alpha      = new Option<double?>("--alpha") { Description = "--algorithm: threshold α" };
        var opening    = new Option<string?>("--opening") { Description = "--algorithm: transitive opening of the TO keys, complete-linkage (default) | single-linkage-order" };
        var inters     = new Option<int?>("--intersections") { Description = "--algorithm: largest number w of objects in intersection areas" };
        var minSize    = new Option<int>("--min-size") { Description = "--algorithm: least number of representatives in a cluster", DefaultValueFactory = _ => 1 };
        var maxSize    = new Option<int>("--max-size") { Description = "--algorithm: largest number of representatives in a cluster (0 = no bound)", DefaultValueFactory = _ => 0 };
        var maxNodes   = new Option<long>("--max-nodes") { Description = "--algorithm: node budget of the combination search", DefaultValueFactory = _ => 500_000_000L };
        var top        = new Option<int>("--top") { Description = "--algorithm: largest number of allotments reported", DefaultValueFactory = _ => 200 };
        var accuracy   = new Option<double?>("--accuracy") { Description = "--algorithm: accuracy threshold ε of the tolerance of the representatives" };
        var representatives = new Option<int?>("--representatives") { Description = "--algorithm: number of representatives (default: the integer part of the square root of the number of objects)" };
        var partition  = new Option<FileInfo?>("--partition") { Description = "--algorithm: file with a class label for every object (one per line) instead of k-means" };
        var noReduce   = new Option<bool>("--no-reduce") { Description = "--algorithm: switch the reduction off; every object is its own class" };
        var restarts   = new Option<int>("--kmeans-restarts") { Description = "--algorithm: number of k-means++ seedings", DefaultValueFactory = _ => 10 };
        var seed       = new Option<int>("--kmeans-seed") { Description = "--algorithm: seed of the k-means++ seeding", DefaultValueFactory = _ => 0 };
        var kmeansNorm = new Option<string?>("--kmeans-normalization") { Description = "--algorithm: normalisation of the data before k-means, none (default) | minmax | max" };
        var format     = new Option<string>("--format") { Description = "--algorithm: output format table | json | csv", DefaultValueFactory = _ => "table" };

        var cmd = new Command("reduce", "Attribute selection by clustering the attributes, or reduction of large data (--algorithm)")
        {
            method, input, distance, norm, criterion, taga, output, algorithm, clusters, alpha, opening, inters, minSize, maxSize,
            maxNodes, top, accuracy, representatives, partition, noReduce, restarts, seed, kmeansNorm, format
        };
        cmd.SetAction((parse, ct) =>
        {
            bool Given(Option option) => parse.GetResult(option) is { Implicit: false };
            if (parse.GetValue(algorithm) is { } key)
            {
                if (Given(method))
                    return Task.FromResult(Fail("--method selects attributes and cannot be combined with --algorithm."));
                var rejected = new List<string>();
                if (Given(distance)) rejected.Add("--distance");
                if (Given(norm)) rejected.Add("--normalize");
                return ReduceLargeDataHandler.Run(new ReduceLargeDataRequest(
                    Algorithm: key, Input: parse.GetRequiredValue(input),
                    Representatives: parse.GetValue(representatives), Partition: parse.GetValue(partition),
                    NoReduce: parse.GetValue(noReduce), KMeansRestarts: parse.GetValue(restarts), KMeansSeed: parse.GetValue(seed),
                    KMeansNormalization: parse.GetValue(kmeansNorm),
                    Options: new AlgorithmOptions(
                        ClusterCount: parse.GetValue(clusters), UserAlpha: parse.GetValue(alpha), TagaOperator: parse.GetValue(taga),
                        Criterion: parse.GetValue(criterion)!, Accuracy: parse.GetValue(accuracy),
                        MaxIntersections: parse.GetValue(inters), MinClusterPower: parse.GetValue(minSize),
                        MaxClusterPower: parse.GetValue(maxSize), MaxSearchNodes: parse.GetValue(maxNodes),
                        MaxResults: parse.GetValue(top), OpeningMethod: parse.GetValue(opening), Diagnostics: new SearchDiagnostics()),
                    Output: parse.GetValue(output), Format: parse.GetValue(format)!, RejectedOptions: rejected), ct);
            }

            var stray = new Option[] { clusters, alpha, opening, inters, accuracy, representatives, partition, noReduce, kmeansNorm, restarts, seed, minSize, maxSize, maxNodes, top, format }
                .Where(Given).Select(option => option.Name).ToList();
            if (stray.Count > 0)
                return Task.FromResult(Fail($"{string.Join(", ", stray)}: these options need --algorithm (reduction of large data)."));
            return SelectAttributes(parse, method, input, distance, norm, criterion, taga, output, ct);
        });
        return cmd;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return 2;
    }

    private static Task<int> SelectAttributes(
        ParseResult parse, Option<string> method, Option<FileInfo> input, Option<string> distance, Option<string> norm,
        Option<string> criterion, Option<string?> taga, Option<FileInfo?> output, CancellationToken ct) => CsvFiles.Guard(async () =>
    {
        var file    = CsvFiles.Read(parse.GetRequiredValue(input));
        var data    = file.Data;
        var names   = file.FeatureNames;
        var options = new AlgorithmOptions(
            Distance:      parse.GetValue(distance)!,
            Normalization: parse.GetValue(norm)!,
            Criterion:     parse.GetValue(criterion)!,
            TagaOperator:  parse.GetValue(taga));

        string methodName = parse.GetValue(method)!;
        if (options.TagaOperator is not null && !methodName.Contains("taga", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"--taga does not apply to the method '{methodName}'.");

        var reducer  = AlgorithmFactory.CreateReducer(methodName, options);
        var reduced  = reducer.Reduce(data, ct);
        var selected = reducer.SelectedAttributeIndices;

        await Console.Out.WriteLineAsync(
            $"Selected {selected.Count} of {data.Cols} attributes: " +
            string.Join(", ", selected.Select(index => $"{names[index]} (column {index + 1})")));

        if (parse.GetValue(output) is { } target)
            await CsvFiles.WriteAsync(CsvFiles.Format(reduced, selected.Select(index => names[index]).ToArray()),
                                      target, $"the reduced data ({reduced.Rows} × {reduced.Cols})");
    });
}
