using System.Text;
using System.Text.Json;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.Serialization;

namespace MathFuzzy.Cli.Commands;

internal static class ClusterHandler
{
    internal static async Task Run(
        string algorithm, FileInfo input, int? clusters, double? alpha,
        string distance, string normalize, string taga, double minkowskiP,
        string criterion, FileInfo? output, string format, double? accuracy = null,
        int intersections = 0, FileInfo? rules = null)
    {
        if (format.ToLowerInvariant() is not ("table" or "json" or "csv"))
        {
            await Console.Error.WriteLineAsync($"Unknown format: '{format}'. Valid names: table, json, csv");
            Environment.Exit(2);
            return;
        }

        // Try to load as labeled dataset first; fall back to plain data
        var labeled = LabeledDataset.TryFromCsv(input.FullName);
        var data    = labeled?.Data ?? RawDataMatrix.FromCsv(input.FullName);

        if (labeled is not null)
            await Console.Error.WriteLineAsync(
                $"Labels detected: {labeled.ClassCount} classes [{string.Join(", ", labeled.ClassNames)}]");

        var options = new AlgorithmOptions(
            Distance:      distance,
            Normalization: normalize,
            ClusterCount:  clusters,
            TagaOperator:  taga,
            UserAlpha:     alpha,
            MinkowskiP:    minkowskiP,
            Criterion:     criterion,
            Accuracy:      accuracy,
            MaxIntersections: intersections);

        IReadOnlyList<ClusterResult> results;
        try
        {
            results = AlgorithmFactory.Run(algorithm, data, options);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Algorithm error: {ex.Message}");
            Environment.Exit(2);
            return;
        }

        if (results.Count == 0)
        {
            await Console.Error.WriteLineAsync("No valid clustering found for the given parameters.");
            Environment.Exit(3);
            return;
        }

        // Compute external validation for the best result if labels are available
        ExternalValidationResult? validation = null;
        if (labeled is not null)
            validation = ExternalValidator.Compute(results[0], labeled.TrueLabels, labeled.ClassNames);

        string rendered = format.ToLowerInvariant() switch
        {
            "json"  => RenderJson(results, validation),
            "csv"   => RenderCsv(results),
            "table" => RenderTable(results, validation),
            _ => throw new ArgumentException($"Unknown format: '{format}'. Valid names: table, json, csv")
        };

        if (output is not null)
        {
            await File.WriteAllTextAsync(output.FullName, rendered, Encoding.UTF8);
            await Console.Out.WriteLineAsync($"Wrote {results.Count} result(s) to {output.FullName}");
        }
        else
        {
            await Console.Out.WriteAsync(rendered);
        }

        if (rules is not null)
        {
            // one rule per cluster of the best allotment, as in the workbench's Inference page
            var system = ClusterRuleExtractor.Extract(data, results[0], new RuleExtractionOptions());
            await File.WriteAllTextAsync(rules.FullName, FisJson.Serialize(system));
            await Console.Error.WriteLineAsync($"Wrote the inference system ({system.Rules.Count} rules) to {rules.FullName}");
        }
    }

    private static string RenderTable(IReadOnlyList<ClusterResult> results, ExternalValidationResult? ext)
    {
        var sb = new StringBuilder();
        bool hasTb = results.Any(r => r.TiebreakerScore.HasValue);

        if (hasTb)
            sb.AppendLine($"{"#",-4} {"c",-4} {"Alpha",-8} {"F1",-10} {"Dist↓",-8} Centers                  Sizes");
        else
            sb.AppendLine($"{"#",-4} {"c",-4} {"Alpha",-8} {"F1",-10} Centers                  Sizes");
        sb.AppendLine(new string('-', hasTb ? 88 : 80));

        int rank = 1;
        foreach (var r in results)
        {
            string centers = string.Join(",", r.Centers);
            string sizes   = string.Join(",", r.MemberSets.Select(s => s.Length));
            string tbCol   = hasTb
                ? $"{(r.TiebreakerScore.HasValue ? r.TiebreakerScore.Value.ToString("F4") : "—"),-8} "
                : "";
            sb.AppendLine($"{rank,-4} {r.ClusterCount,-4} {r.Alpha,-8:F4} {r.Score,-10:F4} {tbCol}{centers,-25} [{sizes}]");
            rank++;
            if (rank > 20) { sb.AppendLine("  ... (truncated to top 20)"); break; }
        }

        var best = results[0];
        sb.AppendLine();
        sb.AppendLine($"Best: c={best.ClusterCount}, α={best.Alpha:F4}, F1={best.Score:F4}" +
                      (best.TiebreakerScore.HasValue ? $", Dist↓={best.TiebreakerScore.Value:F4}" : ""));
        for (int i = 0; i < best.MemberSets.Length; i++)
        {
            string majority = ext is not null ? $"  [{ext.ClusterMajority[i]}]" : "";
            sb.AppendLine($"  Cluster {i + 1} (center={best.Centers[i]}){majority}: [{string.Join(",", best.MemberSets[i])}]");
        }

        if (ext is not null)
        {
            sb.AppendLine();
            sb.AppendLine("External validation (best result vs ground truth):");
            sb.AppendLine($"  Purity              {ext.Purity:F4}");
            sb.AppendLine($"  Rand Index          {ext.RandIndex:F4}");
            sb.AppendLine($"  Adjusted Rand Index {ext.AdjustedRandIndex:F4}");
            sb.AppendLine($"  NMI                 {ext.NormalizedMutualInfo:F4}");
            sb.AppendLine($"  Fowlkes-Mallows     {ext.FowlkesMallows:F4}");
        }

        return sb.ToString();
    }

    private static string RenderJson(IReadOnlyList<ClusterResult> results, ExternalValidationResult? ext)
    {
        static object ResultDto(ClusterResult r) => new
        {
            clusterCount     = r.ClusterCount,
            alpha            = r.Alpha,
            score            = r.Score,
            distTiebreaker   = r.TiebreakerScore,
            criterion        = r.CriterionName,
            centers          = r.Centers,
            clusters         = r.MemberSets
        };

        object dto = ext is null
            ? (object)results.Select(ResultDto)
            : new
            {
                results = results.Select(ResultDto),
                validation = new
                {
                    purity              = ext.Purity,
                    randIndex           = ext.RandIndex,
                    adjustedRandIndex   = ext.AdjustedRandIndex,
                    nmi                 = ext.NormalizedMutualInfo,
                    fowlkesMallows      = ext.FowlkesMallows,
                    clusterMajority     = ext.ClusterMajority
                }
            };

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RenderCsv(IReadOnlyList<ClusterResult> results)
    {
        var sb = new StringBuilder();
        bool hasTb = results.Any(r => r.TiebreakerScore.HasValue);
        sb.AppendLine(hasTb ? "rank,c,alpha,score,dist_tiebreaker,centers,sizes" : "rank,c,alpha,score,centers,sizes");
        int rank = 1;
        foreach (var r in results)
        {
            string tbField = hasTb ? $",{(r.TiebreakerScore.HasValue ? r.TiebreakerScore.Value.ToString("F6") : "")}" : "";
            sb.AppendLine($"{rank},{r.ClusterCount},{r.Alpha:F6},{r.Score:F6}{tbField}," +
                          $"\"{string.Join(";", r.Centers)}\"," +
                          $"\"{string.Join(";", r.MemberSets.Select(s => s.Length))}\"");
            rank++;
        }
        return sb.ToString();
    }
}
