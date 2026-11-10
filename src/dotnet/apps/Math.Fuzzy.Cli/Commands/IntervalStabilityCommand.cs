using System.CommandLine;
using System.Globalization;
using System.Text;
using MathFuzzy.Clustering.Algorithms.Interval;
using MathFuzzy.Core.Interval;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy interval-stability --input oils.csv
/// Type of the clustering structure of interval-valued data (stable, quasi-stable, unstable) from D-AFC-TC on
/// the matrices of lower and upper bounds, and the set of most possible numbers of clusters.
/// </summary>
public static class IntervalStabilityCommand
{
    public static Command Build()
    {
        var input = new Option<FileInfo>("--input") { Description = "CSV file with interval-valued data (columns name_lo, name_hi)", Required = true };
        var boundNormalization = Reports.BoundNormalizationOption();
        var scaling = Reports.ScalingOption();
        var leap = Reports.LeapOption();
        var snorm = Reports.SNormOption();
        var output = Reports.OutputOption();
        var format = Reports.FormatOption();

        var cmd = new Command("interval-stability", "Type of the clustering structure of interval-valued data and the plausible numbers of clusters")
            { input, boundNormalization, scaling, leap, snorm, output, format };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            string formatName = Reports.CheckFormat(parse.GetValue(format)!);
            var options = Reports.StabilityOptions(parse.GetValue(boundNormalization)!, parse.GetValue(scaling)!, parse.GetValue(leap)!);
            var norm = PlausibleClusterCounts.ParseNorm(parse.GetValue(snorm)!);

            var data = IntervalDataMatrix.FromCsv(parse.GetRequiredValue(input).FullName);
            var stability = IntervalStability.Analyze(data, options, ct);
            var plausible = PlausibleClusterCounts.Compute(stability, data.Objects, norm);

            var text = new StringBuilder();
            text.AppendLine($"Objects: {data.Objects}, attributes: {data.Attributes} (objects are numbered from 1)");
            text.AppendLine($"Structure: {Reports.StructureName(stability.Structure)}");
            Reports.AppendBound(text, "Lower bounds", stability.Lower, data.ObjectNames);
            Reports.AppendBound(text, "Upper bounds", stability.Upper, data.ObjectNames);
            text.AppendLine();
            text.AppendLine($"Possibility of the number of clusters ({parse.GetValue(snorm)} S-norm):");
            foreach (var entry in plausible.Possibility)
                text.AppendLine($"  c = {entry.Key,-4} {entry.Value.ToString("F4", CultureInfo.InvariantCulture)}");
            text.AppendLine($"Cut level: {plausible.Level:F6}");
            text.AppendLine($"Plausible numbers of clusters: {{{string.Join(", ", plausible.Counts)}}}");

            await Reports.Emit(formatName, text.ToString(), () => new
            {
                objects = data.Objects,
                attributes = data.Attributes,
                structure = Reports.StructureName(stability.Structure),
                lower = Reports.BoundDto(stability.Lower, data.ObjectNames),
                upper = Reports.BoundDto(stability.Upper, data.ObjectNames),
                possibility = plausible.Possibility.Select(entry => new { clusterCount = entry.Key, possibility = entry.Value }),
                level = plausible.Level,
                plausibleCounts = plausible.Counts
            }, parse.GetValue(output));
        }));
        return cmd;
    }
}
