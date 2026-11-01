using System.CommandLine;

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

        var buildInput    = new Option<FileInfo>("--input") { Required = true };
        var buildDistance = new Option<string>("--distance") { DefaultValueFactory = _ => "euclidean" };
        var buildNorm     = new Option<string>("--normalize") { DefaultValueFactory = _ => "minmax" };
        var buildOutput   = new Option<FileInfo?>("--output");

        var buildCmd = new Command("build", "Build fuzzy tolerance matrix from raw data")
            { buildInput, buildDistance, buildNorm, buildOutput };
        buildCmd.SetAction(async (parse, ct) =>
            await Console.Out.WriteLineAsync($"[stub] build matrix from {parse.GetRequiredValue(buildInput).Name}"));

        var closureInput  = new Option<FileInfo>("--input") { Required = true };
        var closureOutput = new Option<FileInfo?>("--output");
        var closureCmd = new Command("closure", "Compute max-min transitive closure") { closureInput, closureOutput };
        closureCmd.SetAction(async (parse, ct) =>
            await Console.Out.WriteLineAsync($"[stub] transitive closure of {parse.GetRequiredValue(closureInput).Name}"));

        cmd.Subcommands.Add(buildCmd);
        cmd.Subcommands.Add(closureCmd);
        return cmd;
    }
}
