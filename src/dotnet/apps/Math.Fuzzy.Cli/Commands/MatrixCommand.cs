using System.CommandLine;
using MathFuzzy.Clustering;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy matrix build --input data.csv --distance euclidean --output matrix.csv
/// mfuzzy matrix closure --input matrix.csv --output closure.csv
/// </summary>
public static class MatrixCommand
{
    public static Command Build()
    {
        var cmd = new Command("matrix", "Tolerance matrix operations");

        var buildInput    = new Option<FileInfo>("--input") { Description = "CSV file with attribute data", Required = true };
        var buildDistance = new Option<string>("--distance") { Description = "Distance: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var buildNorm     = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var buildMinkP    = new Option<double>("--minkowski-p") { Description = "Minkowski order p (used when --distance minkowski)", DefaultValueFactory = _ => 3.0 };
        var buildAccuracy = new Option<double?>("--accuracy") { Description = "Accuracy threshold ε: round the matrix to multiples of ε (e.g. 0.00001)" };
        var buildOutput   = new Option<FileInfo?>("--output") { Description = "Output CSV file. Default: stdout" };

        var buildCmd = new Command("build", "Build the fuzzy tolerance matrix T of a data set")
            { buildInput, buildDistance, buildNorm, buildMinkP, buildAccuracy, buildOutput };
        buildCmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var options = new AlgorithmOptions(
                Distance:      parse.GetValue(buildDistance)!,
                Normalization: parse.GetValue(buildNorm)!,
                MinkowskiP:    parse.GetValue(buildMinkP),
                Accuracy:      parse.GetValue(buildAccuracy));
            var data   = CsvFiles.Read(parse.GetRequiredValue(buildInput)).Data;
            var matrix = AlgorithmFactory.CreateToleranceBuilder(options).Build(data);
            await CsvFiles.WriteAsync(CsvFiles.Format(matrix), parse.GetValue(buildOutput),
                                      $"the {matrix.Size} × {matrix.Size} tolerance matrix");
        }));

        var closureInput  = new Option<FileInfo>("--input") { Description = "CSV file with an n × n tolerance matrix", Required = true };
        var closureOutput = new Option<FileInfo?>("--output") { Description = "Output CSV file. Default: stdout" };
        var closureCmd = new Command("closure", "Compute the max-min transitive closure of a tolerance matrix")
            { closureInput, closureOutput };
        closureCmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var matrix  = FuzzyToleranceMatrix.FromCsv(parse.GetRequiredValue(closureInput).FullName);
            var closure = new MaxMinTransitiveClosure().Compute(matrix);
            await CsvFiles.WriteAsync(CsvFiles.Format(closure), parse.GetValue(closureOutput),
                                      $"the transitive closure ({closure.GetDistinctValues().Count} levels)");
        }));

        cmd.Subcommands.Add(buildCmd);
        cmd.Subcommands.Add(closureCmd);
        return cmd;
    }
}
