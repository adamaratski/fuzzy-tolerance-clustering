using System.CommandLine;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy cluster --algorithm dafc --input data.csv --clusters 3 --distance euclidean --output results.json
/// </summary>
public static class ClusterCommand
{
    public static Command Build()
    {
        var algorithm = new Option<string>("--algorithm") { Description = "Algorithm: dafc | dpafc | dafc-tc | dafc-tc-alpha | dpafc-tc | dafc-taga | dpafc-taga | hafc-tc", Required = true };
        var input     = new Option<FileInfo>("--input") { Description = "CSV file with raw data or tolerance matrix", Required = true };
        var clusters  = new Option<int?>("--clusters") { Description = "Number of clusters (omit for auto)" };
        var alpha     = new Option<double?>("--alpha") { Description = "User alpha threshold for dafc-tc-alpha (0..1)" };
        var distance  = new Option<string>("--distance") { Description = "Distance: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var accuracy  = new Option<double?>("--accuracy") { Description = "Accuracy threshold ε: round the tolerance matrix to multiples of ε before building levels (e.g. 0.00001)" };
        var norm      = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var taga      = new Option<string>("--taga") { Description = "TAGA operator: max | min | mean | median | umedian | dmedian", DefaultValueFactory = _ => "mean" };
        var minkP     = new Option<double>("--minkowski-p") { Description = "Minkowski order p (used when --distance minkowski)", DefaultValueFactory = _ => 3.0 };
        var criterion = new Option<string>("--criterion") { Description = "Quality criterion: f1 | f2", DefaultValueFactory = _ => "f1" };
        var inters    = new Option<int>("--intersections") { Description = "D-PAFC: max objects in intersection areas (0 = fully separated clusters)", DefaultValueFactory = _ => 0 };
        var rules     = new Option<FileInfo?>("--rules") { Description = "Write the fuzzy inference system generated from the best allotment (JSON, readable by `mfuzzy infer`)" };
        var output    = new Option<FileInfo?>("--output") { Description = "Output file (JSON). Default: stdout" };
        var format    = new Option<string>("--format") { Description = "Output format: table | json | csv", DefaultValueFactory = _ => "table" };

        var cmd = new Command("cluster", "Run a fuzzy clustering algorithm") { algorithm, input, clusters, alpha, distance, norm, taga, minkP, criterion, output, format, accuracy, inters, rules };
        cmd.SetAction((parse, ct) => ClusterHandler.Run(
            parse.GetValue(algorithm)!,
            parse.GetValue(input)!,
            parse.GetValue(clusters),
            parse.GetValue(alpha),
            parse.GetValue(distance)!,
            parse.GetValue(norm)!,
            parse.GetValue(taga)!,
            parse.GetValue(minkP),
            parse.GetValue(criterion)!,
            parse.GetValue(output),
            parse.GetValue(format)!,
            parse.GetValue(accuracy),
            parse.GetValue(inters),
            parse.GetValue(rules)));
        return cmd;
    }
}
