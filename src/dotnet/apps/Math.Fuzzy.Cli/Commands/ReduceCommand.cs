using System.CommandLine;
using MathFuzzy.Clustering;

namespace MathFuzzy.Cli.Commands;

/// <summary>mfuzzy reduce --method dpafc-tc --input data.csv --output reduced.csv</summary>
public static class ReduceCommand
{
    public static Command Build()
    {
        var method    = new Option<string>("--method") { Description = "Method: dpafc | dpafc-tc | dpafc-taga", DefaultValueFactory = _ => "dpafc" };
        var input     = new Option<FileInfo>("--input") { Description = "CSV file with attribute data", Required = true };
        var distance  = new Option<string>("--distance") { Description = "Distance between attributes: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var norm      = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var criterion = new Option<string>("--criterion") { Description = "Quality criterion: f1 | f2", DefaultValueFactory = _ => "f1" };
        var taga      = new Option<string>("--taga") { Description = "TAGA operator (dpafc-taga): max | min | mean | median | umedian | dmedian", DefaultValueFactory = _ => "mean" };
        var output    = new Option<FileInfo?>("--output") { Description = "Output CSV with the selected attributes. Default: only the selection is printed" };

        var cmd = new Command("reduce", "Attribute selection by clustering the attributes")
            { method, input, distance, norm, criterion, taga, output };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var file    = parse.GetRequiredValue(input);
            var data    = CsvFiles.ReadData(file);
            var names   = CsvFiles.FeatureNames(file, data);
            var options = new AlgorithmOptions(
                Distance:      parse.GetValue(distance)!,
                Normalization: parse.GetValue(norm)!,
                Criterion:     parse.GetValue(criterion)!,
                TagaOperator:  parse.GetValue(taga));

            var reducer  = AlgorithmFactory.CreateReducer(parse.GetValue(method)!, options);
            var reduced  = reducer.Reduce(data, ct);
            var selected = reducer.SelectedAttributeIndices;

            await Console.Out.WriteLineAsync(
                $"Selected {selected.Count} of {data.Cols} attributes: " +
                string.Join(", ", selected.Select(index => $"{names[index]} (column {index + 1})")));

            if (parse.GetValue(output) is { } target)
                await CsvFiles.WriteAsync(CsvFiles.Format(reduced, selected.Select(index => names[index]).ToArray()),
                                          target, $"the reduced data ({reduced.Rows} × {reduced.Cols})");
        }));
        return cmd;
    }
}
