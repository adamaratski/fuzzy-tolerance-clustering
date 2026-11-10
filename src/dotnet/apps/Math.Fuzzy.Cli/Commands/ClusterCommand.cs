using System.CommandLine;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy cluster --algorithm dafc --input data.csv --clusters 3 --distance euclidean --output results.json
/// mfuzzy cluster --algorithm dpafc --input relation.csv --matrix
/// </summary>
public static class ClusterCommand
{
    public static Command Build()
    {
        var algorithm = new Option<string>("--algorithm") { Description = "Algorithm: dafc | dafc-u | dpafc | dafc-tc | dafc-tc-alpha | dpafc-tc | dafc-taga | dafc-taga-alpha | dpafc-taga | hafc-tc | hafc-taga | dpafc-dr | dpafc-tc-dr | dpafc-taga-dr | dpaifc | daifc | dpaifc-tc (intuitionistic fuzzy data)", Required = true };
        var input     = new Option<FileInfo>("--input") { Description = "CSV file with attribute data (objects in rows), or with an n × n fuzzy tolerance matrix when --matrix is given", Required = true };
        var matrix    = new Option<bool>("--matrix") { Description = "The input file is a fuzzy tolerance matrix (values in [0, 1], symmetric), not attribute data" };
        var nonMember = new Option<FileInfo?>("--nonmembership") { Description = "Intuitionistic algorithms: CSV file with the non-membership matrix; --input is then the membership matrix. Without it --input is intuitionistic attribute data (columns in pairs: membership, non-membership)" };
        var scanOrder = new Option<string?>("--scan-order") { Description = "Intuitionistic algorithms: order of the scan over the threshold pairs, beta-inner (default) | beta-outer" };
        var ifsMeasure = new Option<string?>("--ifs-measure") { Description = "Intuitionistic algorithms on attribute data: similarity measure, hamming (default) | euclidean" };
        var clusters  = new Option<int?>("--clusters") { Description = "Number of clusters c (dafc, daifc: required; dafc-tc: optional)" };
        var alpha     = new Option<double?>("--alpha") { Description = "Threshold α in [0, 1] (dafc: search this level only; dafc-tc-alpha, dafc-taga-alpha: least admissible threshold)" };
        var distance  = new Option<string>("--distance") { Description = "Distance: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var accuracy  = new Option<double?>("--accuracy") { Description = "Accuracy threshold ε: round the tolerance matrix to multiples of ε before building levels (e.g. 0.00001)" };
        var norm      = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var taga      = new Option<string?>("--taga") { Description = "Aggregation operator of the TAGA algorithms: auto | max | min | mean | median | umedian | dmedian (default: auto — the operator whose approximation is nearest to T)" };
        var minkP     = new Option<double>("--minkowski-p") { Description = "Minkowski order p (used when --distance minkowski)", DefaultValueFactory = _ => 3.0 };
        var criterion = new Option<string>("--criterion") { Description = "Quality criterion: f1 | f2", DefaultValueFactory = _ => "f1" };
        var inters    = new Option<int?>("--intersections") { Description = "Largest number w of objects in intersection areas (dpafc: default 0; dafc, dafc-u: default is the automatic rule w = 0, 1, …)" };
        var minSize   = new Option<int>("--min-size") { Description = "Least number of objects in a cluster", DefaultValueFactory = _ => 1 };
        var maxSize   = new Option<int>("--max-size") { Description = "Largest number of objects in a cluster (0 = no bound); the parameter u of dafc-u", DefaultValueFactory = _ => 0 };
        var maxNodes  = new Option<long>("--max-nodes") { Description = "Node budget of the combination search; a warning is printed when it is spent", DefaultValueFactory = _ => 500_000_000L };
        var top       = new Option<int>("--top") { Description = "Largest number of allotments reported, best first", DefaultValueFactory = _ => 200 };
        var rules     = new Option<FileInfo?>("--rules") { Description = "Write the fuzzy inference system generated from the best allotment (JSON, readable by `mfuzzy infer`)" };
        var output    = new Option<FileInfo?>("--output") { Description = "Output file. Default: stdout" };
        var format    = new Option<string>("--format") { Description = "Output format: table | json | csv", DefaultValueFactory = _ => "table" };

        var cmd = new Command("cluster", "Run a fuzzy clustering algorithm")
        {
            algorithm, input, matrix, clusters, alpha, distance, norm, minkP, accuracy, taga, criterion,
            inters, minSize, maxSize, maxNodes, top, rules, output, format, nonMember, scanOrder, ifsMeasure
        };
        cmd.SetAction((parse, ct) =>
        {
            // Options that describe how the tolerance matrix is built make no sense with --matrix.
            bool Given(Option option) => parse.GetResult(option) is { Implicit: false };
            var matrixBuildOptions = new List<string>();
            if (Given(distance)) matrixBuildOptions.Add("--distance");
            if (Given(norm))     matrixBuildOptions.Add("--normalize");
            if (Given(minkP))    matrixBuildOptions.Add("--minkowski-p");
            if (Given(accuracy)) matrixBuildOptions.Add("--accuracy");

            return ClusterHandler.Run(new ClusterRequest(
                Algorithm:      parse.GetValue(algorithm)!,
                Input:          parse.GetValue(input)!,
                InputIsMatrix:  parse.GetValue(matrix),
                Clusters:       parse.GetValue(clusters),
                Alpha:          parse.GetValue(alpha),
                Distance:       parse.GetValue(distance)!,
                Normalize:      parse.GetValue(norm)!,
                MinkowskiP:     parse.GetValue(minkP),
                Accuracy:       parse.GetValue(accuracy),
                Taga:           parse.GetValue(taga),
                Criterion:      parse.GetValue(criterion)!,
                Intersections:  parse.GetValue(inters),
                MinSize:        parse.GetValue(minSize),
                MaxSize:        parse.GetValue(maxSize),
                MaxNodes:       parse.GetValue(maxNodes),
                Top:            parse.GetValue(top),
                Rules:          parse.GetValue(rules),
                Output:         parse.GetValue(output),
                Format:         parse.GetValue(format)!,
                MatrixBuildOptionsGiven: matrixBuildOptions,
                NonMembership:  parse.GetValue(nonMember),
                ScanOrder:      parse.GetValue(scanOrder),
                IntuitionisticMeasure: parse.GetValue(ifsMeasure),
                MaxNodesGiven:  Given(maxNodes)), ct);
        });
        return cmd;
    }
}

/// <summary>The parsed arguments of <c>mfuzzy cluster</c>.</summary>
internal sealed record ClusterRequest(
    string Algorithm, FileInfo Input, bool InputIsMatrix, int? Clusters, double? Alpha,
    string Distance, string Normalize, double MinkowskiP, double? Accuracy, string? Taga,
    string Criterion, int? Intersections, int MinSize, int MaxSize, long MaxNodes, int Top,
    FileInfo? Rules, FileInfo? Output, string Format, IReadOnlyList<string> MatrixBuildOptionsGiven,
    FileInfo? NonMembership = null, string? ScanOrder = null, string? IntuitionisticMeasure = null, bool MaxNodesGiven = false);
