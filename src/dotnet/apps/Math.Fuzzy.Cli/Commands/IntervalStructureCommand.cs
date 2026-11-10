using System.CommandLine;
using System.Globalization;
using System.Text;
using MathFuzzy.Clustering.Algorithms.Interval;
using MathFuzzy.Core.Interval;

namespace MathFuzzy.Cli.Commands;

/// <summary>The options of the stable-structure technique, shared by interval-structure and project-paths.</summary>
internal sealed class StructureOptionSet
{
    public Option<FileInfo> Input { get; } = new("--input") { Description = "CSV file with interval-valued data (columns name_lo, name_hi)", Required = true };
    public Option<string?> Measure { get; } = Reports.IntervalMeasureOption();
    public Option<double> Lambda { get; } = Reports.LambdaOption();
    public Option<string> Scaling { get; } = Reports.ScalingOption();
    public Option<string> SNorm { get; } = Reports.SNormOption();
    public Option<string> BoundNormalization { get; } = Reports.BoundNormalizationOption();
    public Option<string> Leap { get; } = Reports.LeapOption();
    public Option<string> Validity { get; } = new("--validity") { Description = "Validity measure that selects the number of clusters: qmf (default) | lmf | msc", DefaultValueFactory = _ => "qmf" };
    public Option<string> ValidityDirection { get; } = new("--validity-direction") { Description = "Select the largest (max, default) or the smallest (min) value of the validity measure", DefaultValueFactory = _ => "max" };
    public Option<string?> Clusters { get; } = new("--clusters") { Description = "Numbers of clusters to try, for example 2,3,4 (default: the plausible numbers of the stability analysis)" };
    public Option<FileInfo?> Output { get; } = Reports.OutputOption();
    public Option<string> Format { get; } = Reports.FormatOption();

    public IEnumerable<Option> All => [Input, Measure, Lambda, Scaling, SNorm, BoundNormalization, Leap, Validity, ValidityDirection, Clusters];

    public StableStructureOptions Build(ParseResult parse)
    {
        string direction = parse.GetValue(ValidityDirection)!.ToLowerInvariant();
        if (direction is not ("max" or "min"))
            throw new ArgumentException($"Unknown validity direction: '{direction}'. Valid names: max, min");
        string validity = parse.GetValue(Validity)!.ToLowerInvariant();
        if (validity is not ("qmf" or "lmf" or "msc"))
            throw new ArgumentException($"Unknown validity measure: '{validity}'. Valid names: qmf, lmf, msc");
        return new StableStructureOptions(
            Measure: IntervalTolerance.ParseMeasure(parse.GetValue(Measure) ?? "sqeuclid"),
            Lambda: parse.GetValue(Lambda),
            Scaling: SpecialInputs.ParseScaling(parse.GetValue(Scaling)),
            Norm: PlausibleClusterCounts.ParseNorm(parse.GetValue(SNorm)!),
            Stability: Reports.StabilityOptions(parse.GetValue(BoundNormalization)!, parse.GetValue(Scaling)!, parse.GetValue(Leap)!),
            ValidityMeasure: validity,
            MaximizeValidity: direction == "max",
            ClusterCounts: parse.GetValue(Clusters) is { } counts ? Reports.ParseCounts(counts) : null);
    }

    /// <summary>Candidates, one row each, then the selected allotment.</summary>
    public static void AppendStructure(StringBuilder text, StableStructureResult structure, IReadOnlyList<string>? names, string validity)
    {
        if (structure.Stability is { } stability)
        {
            text.AppendLine($"Structure of the matrices of bounds: {Reports.StructureName(stability.Structure)}" +
                            $" (lower: c = {stability.Lower.ClusterCount} at {stability.Lower.Alpha:F6}; upper: c = {stability.Upper.ClusterCount} at {stability.Upper.Alpha:F6})");
            text.AppendLine($"Plausible numbers of clusters: {{{string.Join(", ", structure.Plausible!.Counts)}}}");
        }
        else
        {
            text.AppendLine("Numbers of clusters given with --clusters.");
        }
        text.AppendLine();
        text.AppendLine($"{"c",-4} {"alpha",-10} {"criterion",-10} {"V_" + validity.ToUpperInvariant(),-10} {"unique",-7} admissible");
        text.AppendLine(new string('-', 56));
        foreach (var candidate in structure.Candidates)
        {
            if (candidate.Allotment is not { } allotment)
            {
                text.AppendLine($"{candidate.ClusterCount,-4} no allotment");
                continue;
            }
            text.AppendLine($"{candidate.ClusterCount,-4} {allotment.Alpha,-10:F6} {allotment.Score,-10:F4} " +
                            $"{candidate.Validity!.Value,-10:F4} {(candidate.IsUnique ? "yes" : "no"),-7} {(candidate.IsAdmissible ? "yes" : "no")}");
        }
        text.AppendLine();
        if (structure.Selected is not { } selected)
        {
            text.AppendLine("No admissible number of clusters: the maximiser of the criterion is not unique for any of them.");
            return;
        }
        text.AppendLine($"Selected allotment: c = {selected.ClusterCount}, alpha = {selected.Alpha:F6}, {selected.CriterionName} = {selected.Score:F4}");
        for (int cluster = 0; cluster < selected.ClusterCount; cluster++)
            text.AppendLine($"  Cluster {cluster + 1} (typical point {Reports.ObjectLabel(names, selected.Centers[cluster])}): " +
                            Reports.ObjectList(names, selected.MemberSets[cluster]));
    }

    public static object StructureDto(StableStructureResult structure, IReadOnlyList<string>? names) => new
    {
        structure = structure.Stability is { } stability ? Reports.StructureName(stability.Structure) : null,
        plausibleCounts = structure.Plausible?.Counts,
        candidates = structure.Candidates.Select(candidate => new
        {
            clusterCount = candidate.ClusterCount,
            threshold = candidate.Allotment?.Alpha,
            criterion = candidate.Allotment?.Score,
            validity = candidate.Validity,
            unique = candidate.IsUnique,
            admissible = candidate.IsAdmissible
        }),
        selected = structure.Selected is not { } selected ? null : new
        {
            clusterCount = selected.ClusterCount,
            threshold = selected.Alpha,
            criterion = selected.Score,
            clusters = selected.MemberSets.Select((members, cluster) => new
            {
                typicalPoint = selected.Centers[cluster] + 1,
                members = members.Select(member => member + 1).ToArray(),
                names = names is null ? null : members.Select(member => names[member]).ToArray()
            })
        }
    };
}

/// <summary>
/// mfuzzy interval-structure --input oils.csv
/// The stable-structure technique for interval-valued data: plausible numbers of clusters, D-AFC(c) for each,
/// the number of clusters chosen by a validity measure.
/// </summary>
public static class IntervalStructureCommand
{
    public static Command Build()
    {
        var set = new StructureOptionSet();
        var cmd = new Command("interval-structure", "Stable clustering structure of interval-valued data (stable-structure technique)");
        foreach (var option in set.All) cmd.Options.Add(option);
        cmd.Options.Add(set.Output);
        cmd.Options.Add(set.Format);
        cmd.SetAction((parse, ct) =>
        {
            int exitCode = 0;
            return CsvFiles.Guard(async () =>
            {
                string format = Reports.CheckFormat(parse.GetValue(set.Format)!);
                var options = set.Build(parse);
                var data = IntervalDataMatrix.FromCsv(parse.GetRequiredValue(set.Input).FullName);
                var structure = StableStructureTechnique.Run(data, options, ct);

                var text = new StringBuilder();
                text.AppendLine($"Objects: {data.Objects}, attributes: {data.Attributes} (objects are numbered from 1)");
                StructureOptionSet.AppendStructure(text, structure, data.ObjectNames, options.ValidityMeasure);
                await Reports.Emit(format, text.ToString(), () => StructureOptionSet.StructureDto(structure, data.ObjectNames), parse.GetValue(set.Output));
                if (structure.Selected is null) exitCode = 3;
            }, () => exitCode);
        });
        return cmd;
    }
}
