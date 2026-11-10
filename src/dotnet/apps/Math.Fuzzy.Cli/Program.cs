using System.CommandLine;
using System.Globalization;
using System.Text;
using MathFuzzy.Cli.Commands;

// Numbers are read and written with a decimal point whatever the system locale is,
// so that CSV and JSON output means the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var root = new RootCommand("mfuzzy — Math.Fuzzy clustering toolkit");

root.Subcommands.Add(ClusterCommand.Build());
root.Subcommands.Add(MatrixCommand.Build());
root.Subcommands.Add(ReduceCommand.Build());
root.Subcommands.Add(InferCommand.Build());
root.Subcommands.Add(SizeCommand.BuildSize());
root.Subcommands.Add(SizeCommand.BuildBounds());
root.Subcommands.Add(IntervalStabilityCommand.Build());
root.Subcommands.Add(IntervalStructureCommand.Build());
root.Subcommands.Add(ProjectPathsCommand.Build());
root.Subcommands.Add(PreferenceCommand.Build());

return await root.Parse(args).InvokeAsync();
