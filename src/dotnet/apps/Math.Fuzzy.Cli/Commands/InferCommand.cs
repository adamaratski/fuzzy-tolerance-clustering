using System.CommandLine;

namespace MathFuzzy.Cli.Commands;

/// <summary>mfuzzy infer --system rules.json --input 1.2,3.4</summary>
public static class InferCommand
{
    public static Command Build()
    {
        var system = new Option<FileInfo>("--system") { Description = "JSON fuzzy rule system", Required = true };
        var input  = new Option<string>("--input") { Description = "Comma-separated crisp input values", Required = true };

        var cmd = new Command("infer", "Run Mamdani or Sugeno fuzzy inference") { system, input };
        cmd.SetAction(async (parse, ct) =>
            await Console.Out.WriteLineAsync($"[stub] infer with {parse.GetRequiredValue(system).Name} on [{parse.GetRequiredValue(input)}]"));
        return cmd;
    }
}
