using System.CommandLine;
using System.Text;
using MathFuzzy.Clustering.Algorithms.Interval;
using MathFuzzy.Core.Interval;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy project-paths --input paths.csv --select density
/// Selection of a minimal subset of paths of a project network with interval-valued durations:
/// the stable structure of the paths, then the clusters of maximal density or minimal index of fuzziness.
/// </summary>
public static class ProjectPathsCommand
{
    public static Command Build()
    {
        var set = new StructureOptionSet();
        var select = new Option<string>("--select") { Description = "Rule that selects the clusters: density (default) | lmf | qmf", DefaultValueFactory = _ => "density" };
        var cmd = new Command("project-paths", "Select the paths of a project network with interval-valued durations");
        foreach (var option in set.All) cmd.Options.Add(option);
        cmd.Options.Add(select);
        cmd.Options.Add(set.Output);
        cmd.Options.Add(set.Format);
        cmd.SetAction((parse, ct) =>
        {
            int exitCode = 0;
            return CsvFiles.Guard(async () =>
            {
                string format = Reports.CheckFormat(parse.GetValue(set.Format)!);
                var options = set.Build(parse);
                var rule = ProjectPathSelection.ParseRule(parse.GetValue(select)!);
                var data = IntervalDataMatrix.FromCsv(parse.GetRequiredValue(set.Input).FullName);
                var structure = StableStructureTechnique.Run(data, options, ct);
                if (structure.Selected is null)
                {
                    await Console.Error.WriteLineAsync("No admissible allotment of the paths was found.");
                    exitCode = 3;
                    return;
                }
                var selection = ProjectPathSelection.SelectFrom(structure, rule);
                var allotment = selection.Allotment;
                var names = data.ObjectNames;

                var text = new StringBuilder();
                text.AppendLine($"Paths: {data.Objects}, activities: {data.Attributes} (paths are numbered from 1)");
                StructureOptionSet.AppendStructure(text, structure, names, options.ValidityMeasure);
                text.AppendLine();
                text.AppendLine($"{"Cluster",-8} {"I_L",-10} {"I_Q",-10} {"D",-10} Paths");
                for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
                {
                    var indices = selection.Indices[cluster];
                    text.AppendLine($"{cluster + 1,-8} {indices.LinearFuzziness,-10:F4} {indices.QuadraticFuzziness,-10:F4} {indices.Density,-10:F4} " +
                                    Reports.ObjectList(names, allotment.MemberSets[cluster]));
                }
                text.AppendLine();
                text.AppendLine($"Rule: {parse.GetValue(select)}. Selected clusters: {string.Join(", ", selection.SelectedClusters.Select(cluster => cluster + 1))}");
                text.AppendLine($"Selected paths: {Reports.ObjectList(names, selection.SelectedPaths)}");

                await Reports.Emit(format, text.ToString(), () => new
                {
                    structure = StructureOptionSet.StructureDto(structure, names),
                    rule = parse.GetValue(select),
                    indices = selection.Indices.Select((indices, cluster) => new
                    {
                        cluster = cluster + 1,
                        linearFuzziness = indices.LinearFuzziness,
                        quadraticFuzziness = indices.QuadraticFuzziness,
                        density = indices.Density
                    }),
                    selectedClusters = selection.SelectedClusters.Select(cluster => cluster + 1),
                    selectedPaths = selection.SelectedPaths.Select(path => path + 1)
                }, parse.GetValue(set.Output));
            }, () => exitCode);
        });
        return cmd;
    }
}
