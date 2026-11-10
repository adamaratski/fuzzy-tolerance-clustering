using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Applications.LargeData;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Cli.Commands;

/// <summary>The parsed arguments of <c>mfuzzy reduce --algorithm</c>.</summary>
internal sealed record ReduceLargeDataRequest(
    string Algorithm, FileInfo Input, int? Representatives, FileInfo? Partition, bool NoReduce,
    int KMeansRestarts, int KMeansSeed, string? KMeansNormalization,
    AlgorithmOptions Options, FileInfo? Output, string Format, IReadOnlyList<string> RejectedOptions);

/// <summary>
/// Clustering of a large data set through interval-valued representatives (Viattchenin 2013, CDAM'2013): the
/// objects are partitioned into classes, every class becomes an interval vector, the interval vectors are clustered
/// by the chosen algorithm, and every object takes the memberships of its class.
/// </summary>
internal static class ReduceLargeDataHandler
{
    public static Task<int> Run(ReduceLargeDataRequest request, CancellationToken ct)
    {
        int exitCode = 0;
        return CsvFiles.Guard(() => Execute(request, ct, code => exitCode = code), () => exitCode);
    }

    private static async Task Execute(ReduceLargeDataRequest request, CancellationToken ct, Action<int> setExitCode)
    {
        string format = request.Format.ToLowerInvariant();
        if (format is not ("table" or "json" or "csv"))
            throw new ArgumentException($"Unknown format: '{request.Format}'. Valid names: table, json, csv");
        if (request.RejectedOptions.Count > 0)
            throw new ArgumentException(
                $"{string.Join(", ", request.RejectedOptions)}: these options do not apply to the reduction of large data " +
                "(the tolerance of the representatives is built from their bounds; use --kmeans-normalization for the partition).");
        if (AlgorithmFactory.IsIntuitionistic(request.Algorithm))
            throw new ArgumentException($"{request.Algorithm}: the intuitionistic algorithms need intuitionistic fuzzy data; the reduction produces interval-valued objects.");
        if (request.NoReduce && (request.Representatives.HasValue || request.Partition is not null))
            throw new ArgumentException("--no-reduce cannot be combined with --representatives or --partition.");
        if (request.Representatives.HasValue && request.Partition is not null)
            throw new ArgumentException("--representatives cannot be combined with --partition (the partition fixes the classes).");
        if (request.Representatives is < 2)
            throw new ArgumentException("--representatives must be at least 2.");
        if (request.KMeansRestarts < 1)
            throw new ArgumentException("--kmeans-restarts must be at least 1.");
        AlgorithmFactory.EnsureOptionsApply(request.Algorithm, request.Options);

        var file = CsvFiles.Read(request.Input);
        if (file.HasLabels)
            await Console.Error.WriteLineAsync($"Labels detected: {file.ClassNames!.Length} classes [{string.Join(", ", file.ClassNames)}]");

        INormalization? partitionNormalization = request.KMeansNormalization?.ToLowerInvariant().Trim() switch
        {
            null or "none" => null,
            "minmax" => MinMaxNormalization.Instance,
            "max" => MaxNormalization.Instance,
            var other => throw new ArgumentException($"Unknown normalisation: '{other}'. Valid names: none, minmax, max")
        };
        int[]? partition = request.Partition is null ? null : ReadPartition(request.Partition, file.Data.Rows);
        if (request.Representatives > file.Data.Rows)
            throw new ArgumentException($"--representatives {request.Representatives} exceeds the number of objects ({file.Data.Rows}).");

        var largeOptions = new LargeDataOptions(
            Reduce: !request.NoReduce,
            RepresentativeCount: request.Representatives,
            Partition: partition,
            PartitionNormalization: partitionNormalization,
            KMeansRestarts: request.KMeansRestarts,
            KMeansSeed: request.KMeansSeed,
            RepresentativeTolerance: request.Options.Accuracy is double accuracy
                ? (lower, upper) => LargeDataReduction.BurilloBustinceTolerance(lower, upper).Quantize(accuracy)
                : null);

        var result = LargeDataReduction.Run(
            file.Data, largeOptions,
            (tolerance, token) => AlgorithmFactory.Run(request.Algorithm, tolerance, request.Options, token), ct);

        if (request.Options.Diagnostics is { BudgetExhausted: true } diagnostics)
            await Console.Error.WriteLineAsync(
                $"Warning: the combination search stopped after {diagnostics.NodesVisited:N0} nodes (the node budget). " +
                "The allotments below are the best found so far; raise --max-nodes or restrict the search.");

        int representativeCount = result.Representatives.Count;
        string description = request.NoReduce
            ? $"Representatives: {representativeCount} (no reduction: every object is its own class)"
            : $"Representatives: {representativeCount} interval-valued objects for {file.Data.Rows} objects" +
              (partition is not null ? " (partition from file)" : "");

        if (result.ObjectResults.Count == 0)
        {
            await Console.Error.WriteLineAsync(description);
            await Console.Error.WriteLineAsync(representativeCount < 2
                ? "Fewer than two representatives: nothing to cluster."
                : "No allotment exists for the given parameters.");
            setExitCode(3);
            return;
        }

        string rendered = ClusterHandler.Render(format, request.Algorithm, result.ObjectResults, file, request.Options.Diagnostics ?? new SearchDiagnostics());
        rendered = format switch
        {
            "table" => description + Environment.NewLine + Environment.NewLine + rendered,
            "json" => AddReduction(rendered, representativeCount, file.Data.Rows, request.NoReduce),
            _ => rendered
        };
        if (format == "csv") await Console.Error.WriteLineAsync(description);

        if (request.Output is not null)
        {
            await File.WriteAllTextAsync(request.Output.FullName, rendered, Encoding.UTF8, ct);
            await Console.Error.WriteLineAsync($"Wrote {result.ObjectResults.Count} result(s) to {request.Output.FullName}");
        }
        else
        {
            await Console.Out.WriteAsync(rendered);
        }
    }

    private static string AddReduction(string json, int representatives, int objects, bool noReduce)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node["reduction"] = new JsonObject
        {
            ["representatives"] = representatives,
            ["objects"] = objects,
            ["reduced"] = !noReduce
        };
        return node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// One class label per object, one per line (the first cell of the line); a first line that is not a number is a
    /// header. Labels may be numbers or names; the classes are numbered in the order of their first object.
    /// </summary>
    private static int[] ReadPartition(FileInfo file, int objectCount)
    {
        var labels = new List<string>();
        var lines = File.ReadAllLines(file.FullName).Where(line => line.Trim().Length > 0).ToList();
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string cell = lines[lineIndex].Split(',', ';', '\t')[0].Trim().Trim('"');
            if (lineIndex == 0 && !double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out _) && lines.Count == objectCount + 1)
                continue;
            labels.Add(cell);
        }
        if (labels.Count != objectCount)
            throw new FormatException($"{file.Name}: {labels.Count} labels for {objectCount} objects.");
        var number = new Dictionary<string, int>(StringComparer.Ordinal);
        return [.. labels.Select(label => number.TryGetValue(label, out int known) ? known : number[label] = number.Count)];
    }
}
