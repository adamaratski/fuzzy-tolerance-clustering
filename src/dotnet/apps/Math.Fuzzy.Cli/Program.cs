using System.CommandLine;
using MathFuzzy.Cli.Commands;

var root = new RootCommand("mfuzzy — Math.Fuzzy clustering toolkit");

root.Subcommands.Add(ClusterCommand.Build());
root.Subcommands.Add(MatrixCommand.Build());
root.Subcommands.Add(ReduceCommand.Build());
root.Subcommands.Add(InferCommand.Build());

return await root.Parse(args).InvokeAsync();
