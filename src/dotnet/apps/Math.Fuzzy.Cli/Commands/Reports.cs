using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MathFuzzy.Clustering.Algorithms.Interval;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Interval;

namespace MathFuzzy.Cli.Commands;

/// <summary>Options and output shared by the sub-commands for interval-valued data, large data and preference relations.</summary>
internal static class Reports
{
    public static Option<string> FormatOption() =>
        new("--format") { Description = "Output format: table | json", DefaultValueFactory = _ => "table" };

    public static Option<FileInfo?> OutputOption() => new("--output") { Description = "Output file. Default: stdout" };

    public static Option<string?> IntervalMeasureOption() =>
        new("--interval-measure") { Description = "Interval measure: sqeuclid (default) | euclid | bb | hausdorff | juyuan | sqeuclid-matching | euclid-matching" };

    public static Option<double> LambdaOption() =>
        new("--lambda") { Description = "Exponent λ ≥ 1 of the Ju–Yuan measure", DefaultValueFactory = _ => 2.0 };

    public static Option<string> ScalingOption() =>
        new("--interval-scaling") { Description = "Scaling of the bounds: minmax | max | none", DefaultValueFactory = _ => "minmax" };

    public static Option<string> SNormOption() =>
        new("--snorm") { Description = "S-norm that combines the fuzzy numbers of clusters: max | probsum | boundedsum", DefaultValueFactory = _ => "max" };

    public static Option<string> BoundNormalizationOption() =>
        new("--bound-normalization") { Description = "Normalisation of the matrices of lower and upper bounds: separate (default) | joint", DefaultValueFactory = _ => "separate" };

    public static Option<string> LeapOption() =>
        new("--leap") { Description = "Form of the leap heuristic of D-AFC-TC: ratio (default) | difference", DefaultValueFactory = _ => "ratio" };

    public static string CheckFormat(string format)
    {
        string name = format.ToLowerInvariant();
        if (name is not ("table" or "json"))
            throw new ArgumentException($"Unknown format: '{format}'. Valid names: table, json");
        return name;
    }

    public static BoundNormalization ParseBoundNormalization(string name) => name.ToLowerInvariant().Trim() switch
    {
        "separate" => BoundNormalization.Separate,
        "joint" => BoundNormalization.Joint,
        _ => throw new ArgumentException($"Unknown bound normalisation: '{name}'. Valid names: separate, joint")
    };

    public static LeapForm ParseLeap(string name) => name.ToLowerInvariant().Trim() switch
    {
        "ratio" => LeapForm.Ratio,
        "difference" => LeapForm.Difference,
        _ => throw new ArgumentException($"Unknown leap form: '{name}'. Valid names: ratio, difference")
    };

    public static IntervalStabilityOptions StabilityOptions(string boundNormalization, string scaling, string leap) =>
        new(Normalization: ParseBoundNormalization(boundNormalization),
            Scaling: SpecialInputs.ParseScaling(scaling),
            LeapForm: ParseLeap(leap));

    public static string StructureName(ClusteringStructure structure) => structure switch
    {
        ClusteringStructure.Stable => "stable",
        ClusteringStructure.QuasiStable => "quasi-stable",
        _ => "unstable"
    };

    /// <summary>Numbers of clusters from "2,3,4".</summary>
    public static int[] ParseCounts(string text)
    {
        try
        {
            return [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.Parse(part, CultureInfo.InvariantCulture))];
        }
        catch (FormatException)
        {
            throw new ArgumentException($"--clusters expects whole numbers separated by commas, for example 2,3,4; found '{text}'.");
        }
    }

    /// <summary>Label of an object: its name when the file has names, otherwise x followed by its number from 1.</summary>
    public static string ObjectLabel(IReadOnlyList<string>? names, int objectIndex) =>
        names is null ? $"x{objectIndex + 1}" : $"{objectIndex + 1} ({names[objectIndex]})";

    public static string ObjectList(IReadOnlyList<string>? names, IEnumerable<int> objects) =>
        string.Join(", ", objects.Select(objectIndex => ObjectLabel(names, objectIndex)));

    public static string Numbers(IEnumerable<double> values) =>
        "(" + string.Join(", ", values.Select(value => value.ToString("F4", CultureInfo.InvariantCulture))) + ")";

    public static string ToJson(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Writes the table or the JSON text to the output file or to standard output.</summary>
    public static Task Emit(string format, string table, Func<object> json, FileInfo? output) =>
        CsvFiles.WriteAsync(format == "json" ? ToJson(json()) : table, output, "the report");

    /// <summary>Shared description of the bound clusterings for the table and JSON forms.</summary>
    public static void AppendBound(StringBuilder text, string title, BoundClustering bound, IReadOnlyList<string>? names)
    {
        text.AppendLine($"{title}: threshold {bound.Alpha:F6}, {bound.ClusterCount} clusters");
        for (int cluster = 0; cluster < bound.ClusterCount; cluster++)
            text.AppendLine($"  class {cluster + 1}: {ObjectList(names, bound.Allotment.MemberSets[cluster])}; prototype {Numbers(bound.Prototypes[cluster])}");
    }

    public static object BoundDto(BoundClustering bound, IReadOnlyList<string>? names) => new
    {
        threshold = bound.Alpha,
        clusterCount = bound.ClusterCount,
        classes = bound.Allotment.MemberSets.Select((members, cluster) => new
        {
            members = members.Select(member => member + 1).ToArray(),
            names = names is null ? null : members.Select(member => names[member]).ToArray(),
            prototype = bound.Prototypes[cluster]
        })
    };
}
