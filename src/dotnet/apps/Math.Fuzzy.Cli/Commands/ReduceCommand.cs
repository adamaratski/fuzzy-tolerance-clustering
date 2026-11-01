using System.CommandLine;

namespace MathFuzzy.Cli.Commands;

/// <summary>mfuzzy reduce --method dpafc-tc --input data.csv --output reduced.csv</summary>
public static class ReduceCommand
{
    public static Command Build()
    {
        var method = new Option<string>("--method") { Description = "Method: dpafc | dpafc-tc | dpafc-taga", DefaultValueFactory = _ => "dpafc", Required = true };
        var input  = new Option<FileInfo>("--input") { Required = true };
        var output = new Option<FileInfo?>("--output");

        var cmd = new Command("reduce", "Dimensionality reduction via attribute-space clustering")
            { method, input, output };
        cmd.SetAction(async (parse, ct) =>
            await Console.Out.WriteLineAsync($"[stub] reduce {parse.GetRequiredValue(input).Name} via {parse.GetRequiredValue(method)}"));
        return cmd;
    }
}
