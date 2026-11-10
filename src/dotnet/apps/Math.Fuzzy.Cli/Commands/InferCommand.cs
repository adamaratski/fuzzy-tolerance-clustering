using System.CommandLine;
using System.Globalization;
using MathFuzzy.Inference.Serialization;

namespace MathFuzzy.Cli.Commands;

/// <summary>mfuzzy infer --system rules.json --input 5.1,3.5,1.4,0.2</summary>
public static class InferCommand
{
    public static Command Build()
    {
        var system = new Option<FileInfo>("--system") { Description = "Fuzzy inference system in JSON (as exported by the workbench)", Required = true };
        var input  = new Option<string>("--input") { Description = "Comma-separated crisp input values, one per input variable", Required = true };

        var cmd = new Command("infer", "Evaluate a Mamdani, Takagi–Sugeno or classifier system for one input vector")
            { system, input };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var fis = FisJson.Deserialize(await File.ReadAllTextAsync(parse.GetRequiredValue(system).FullName, ct));
            double[] values = parse.GetRequiredValue(input)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(cell => double.Parse(cell, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();

            var result = fis.Evaluate(values);

            await Console.Out.WriteLineAsync($"System: {fis.Kind}, {fis.Inputs.Count} inputs, {fis.Rules.Count} rules");
            for (int rule = 0; rule < result.Firing.Length; rule++)
                await Console.Out.WriteLineAsync($"  rule {rule + 1,-3} firing strength {result.Firing[rule]:F4}");
            for (int index = 0; index < result.Outputs.Length; index++)
            {
                string name = index < fis.Outputs.Count ? fis.Outputs[index].Name : $"y{index + 1}";
                await Console.Out.WriteLineAsync($"  output {name} = {Number(result.Outputs[index])}");
            }
            if (result.ClassScores.Length > 0)
            {
                await Console.Out.WriteLineAsync(
                    "  class scores: " + string.Join(", ", result.ClassScores.Select(Number)));
                await Console.Out.WriteLineAsync(result switch
                {
                    { Ambiguous: true }       => "  decision: ambiguous (two or more classes share the best score)",
                    { PredictedClass: < 0 }   => "  decision: none (no rule fired)",
                    _                         => $"  decision: class {result.PredictedClass + 1}"
                });
            }
        }));
        return cmd;
    }

    /// <summary>A value with four decimals; "—" where no fired rule mentions the output.</summary>
    private static string Number(double value) => double.IsNaN(value) ? "—" : value.ToString("F4", CultureInfo.InvariantCulture);
}
