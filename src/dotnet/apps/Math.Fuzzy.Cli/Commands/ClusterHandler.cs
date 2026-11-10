using System.Globalization;
using System.Text;
using System.Text.Json;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Algorithms.Intuitionistic;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.Intuitionistic;
using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Inference.Generation;
using MathFuzzy.Inference.Serialization;

namespace MathFuzzy.Cli.Commands;

internal static class ClusterHandler
{
    /// <summary>Exit codes: 0 = allotment found, 2 = invalid input or options, 3 = no allotment exists.</summary>
    internal static Task<int> Run(ClusterRequest request, CancellationToken ct)
    {
        int exitCode = 0;
        return CsvFiles.Guard(() => Execute(request, ct, code => exitCode = code), () => exitCode);
    }

    private static async Task Execute(ClusterRequest request, CancellationToken ct, Action<int> setExitCode)
    {
        string format = request.Format.ToLowerInvariant();
        if (format is not ("table" or "json" or "csv"))
            throw new ArgumentException($"Unknown format: '{request.Format}'. Valid names: table, json, csv");
        if (request.InputIsMatrix && request.MatrixBuildOptionsGiven.Count > 0)
            throw new ArgumentException(
                $"{string.Join(", ", request.MatrixBuildOptionsGiven)}: such options say how a tolerance matrix is built from " +
                "attribute data and cannot be combined with --matrix.");
        if (request.InputIsMatrix && request.Rules is not null)
            throw new ArgumentException("--rules needs attribute data: fuzzy rules are built on the attributes, not on a relation.");
        if (request.Top < 1)
            throw new ArgumentException("--top must be at least 1.");

        bool intuitionistic = AlgorithmFactory.IsIntuitionistic(request.Algorithm);
        if (intuitionistic)
        {
            if (request.InputIsMatrix)
                throw new ArgumentException(
                    "An intuitionistic fuzzy tolerance is given as two matrices: --input <membership> --nonmembership <non-membership> (without --matrix).");
            if (request.MatrixBuildOptionsGiven.Count > 0)
                throw new ArgumentException(
                    $"{string.Join(", ", request.MatrixBuildOptionsGiven)}: such options do not apply to intuitionistic fuzzy data; " +
                    "the tolerance of intuitionistic attribute data is built with --ifs-measure.");
            if (request.Rules is not null)
                throw new ArgumentException("--rules is not available for the intuitionistic algorithms.");
            if (request.NonMembership is not null && request.IntuitionisticMeasure is not null)
                throw new ArgumentException("--ifs-measure builds a tolerance from attribute data and cannot be combined with --nonmembership.");
        }
        else if (request.NonMembership is not null)
        {
            throw new ArgumentException("--nonmembership applies to the intuitionistic algorithms only (dpaifc, daifc, dpaifc-tc).");
        }

        var diagnostics = new SearchDiagnostics();
        var options = new AlgorithmOptions(
            Distance:         request.Distance,
            Normalization:    request.Normalize,
            ClusterCount:     request.Clusters,
            TagaOperator:     request.Taga,
            UserAlpha:        request.Alpha,
            MinClusterPower:  request.MinSize,
            MaxClusterPower:  request.MaxSize,
            MinkowskiP:       request.MinkowskiP,
            Criterion:        request.Criterion,
            Accuracy:         request.Accuracy,
            MaxIntersections: request.Intersections,
            // the intuitionistic search has its own, smaller default budget
            MaxSearchNodes:   intuitionistic && !request.MaxNodesGiven ? new IntuitionisticOptions().MaxSearchNodes : request.MaxNodes,
            MaxResults:       request.Top,
            Diagnostics:      diagnostics,
            ThresholdScanOrder:    request.ScanOrder,
            IntuitionisticMeasure: request.IntuitionisticMeasure);
        AlgorithmFactory.EnsureOptionsApply(request.Algorithm, options);

        DataFile? file = null;
        IReadOnlyList<ClusterResult> results;
        if (intuitionistic)
        {
            results = request.NonMembership is not null
                ? AlgorithmFactory.RunIntuitionistic(
                      request.Algorithm,
                      IntuitionisticToleranceMatrix.FromCsv(request.Input.FullName, request.NonMembership.FullName), options, ct)
                : AlgorithmFactory.RunIntuitionistic(
                      request.Algorithm, IntuitionisticDataMatrix.FromCsv(request.Input.FullName), options, ct);
        }
        else if (request.InputIsMatrix)
        {
            var tolerance = FuzzyToleranceMatrix.FromCsv(request.Input.FullName);
            results = AlgorithmFactory.Run(request.Algorithm, tolerance, options, ct);
        }
        else
        {
            file = DataFile.Load(request.Input.FullName);
            if (file.HasLabels)
                await Console.Error.WriteLineAsync(
                    $"Labels detected: {file.ClassNames!.Length} classes [{string.Join(", ", file.ClassNames)}]");
            results = AlgorithmFactory.Run(request.Algorithm, file.Data, options, ct);
        }

        if (diagnostics.BudgetExhausted)
            await Console.Error.WriteLineAsync(
                $"Warning: the combination search stopped after {diagnostics.NodesVisited:N0} nodes (the node budget). " +
                "The allotments below are the best found so far, not necessarily the best that exist; " +
                "raise --max-nodes or restrict the search (--alpha, --accuracy, --min-size).");

        if (results.Count == 0)
        {
            await Console.Error.WriteLineAsync(diagnostics.BudgetExhausted
                ? "No allotment was found before the node budget was spent."
                : "No allotment exists for the given parameters.");
            setExitCode(3);
            return;
        }

        bool hierarchy = request.Algorithm.Trim().StartsWith("hafc-", StringComparison.OrdinalIgnoreCase);

        // External validation of the best result when labels are available (flat algorithms only).
        ExternalValidationResult? validation = null;
        if (file is { HasLabels: true } && !hierarchy)
            validation = ExternalValidator.Compute(results[0], file.TrueLabels!, file.ClassNames!);

        // For a hierarchy every level is validated against the labels.
        IReadOnlyList<ExternalValidationResult>? levelValidation = null;
        if (file is { HasLabels: true } && hierarchy)
            levelValidation = [.. results.Select(level => ExternalValidator.Compute(level, file.TrueLabels!, file.ClassNames!))];

        string rendered = format switch
        {
            "json" => RenderJson(results, validation, levelValidation, diagnostics),
            "csv"  => RenderCsv(results),
            _      => hierarchy ? RenderHierarchy(results) : RenderTable(results, validation)
        };

        if (request.Output is not null)
        {
            await File.WriteAllTextAsync(request.Output.FullName, rendered, Encoding.UTF8, ct);
            await Console.Error.WriteLineAsync($"Wrote {results.Count} result(s) to {request.Output.FullName}");
        }
        else
        {
            await Console.Out.WriteAsync(rendered);
        }

        if (request.Rules is not null && file is not null)
        {
            // one rule per cluster of the best allotment, as in the workbench's Inference page
            var system = ClusterRuleExtractor.Extract(file.Data, results[0], new RuleExtractionOptions());
            await File.WriteAllTextAsync(request.Rules.FullName, FisJson.Serialize(system), ct);
            await Console.Error.WriteLineAsync($"Wrote the inference system ({system.Rules.Count} rules) to {request.Rules.FullName}");
        }
    }

    private static string RenderTable(IReadOnlyList<ClusterResult> results, ExternalValidationResult? ext)
    {
        var sb = new StringBuilder();
        bool hasTb = results.Any(r => r.TiebreakerScore.HasValue);
        string criterionName = results[0].CriterionName;

        bool hasBeta = results[0].Beta.HasValue;
        string betaHeader = hasBeta ? $"{"Beta",-8} " : "";
        if (hasTb)
            sb.AppendLine($"{"#",-4} {"c",-4} {"Alpha",-8} {betaHeader}{criterionName,-10} {"Dist↓",-8} Centers                  Sizes");
        else
            sb.AppendLine($"{"#",-4} {"c",-4} {"Alpha",-8} {betaHeader}{criterionName,-10} Centers                  Sizes");
        sb.AppendLine(new string('-', (hasTb ? 88 : 80) + (hasBeta ? 9 : 0)));

        int rank = 1;
        foreach (var r in results)
        {
            string centers = string.Join(",", r.Centers);
            string sizes   = string.Join(",", r.MemberSets.Select(s => s.Length));
            string tbCol   = hasTb
                ? $"{(r.TiebreakerScore.HasValue ? r.TiebreakerScore.Value.ToString("F4") : "—"),-8} "
                : "";
            string betaCol = hasBeta ? $"{r.Beta!.Value,-8:F4} " : "";
            sb.AppendLine($"{rank,-4} {r.ClusterCount,-4} {r.Alpha,-8:F4} {betaCol}{r.Score,-10:F4} {tbCol}{centers,-25} [{sizes}]");
            rank++;
            if (rank > 20 && results.Count > 20) { sb.AppendLine($"  ... ({results.Count - 20} more; use --format json or csv for all)"); break; }
        }

        var best = results[0];
        sb.AppendLine();
        sb.AppendLine($"Best: c={best.ClusterCount}, α={best.Alpha:F4}, " +
                      (hasBeta ? $"β={best.Beta!.Value:F4}, " : "") +
                      (best.IntersectionBound is int bound ? $"w={bound}, " : "") +
                      $"{criterionName}={best.Score:F4}" +
                      (best.TiebreakerScore.HasValue ? $", Dist↓={best.TiebreakerScore.Value:F4}" : "") +
                      (best.IntersectionCount > 0 ? $", objects in intersections: {best.IntersectionCount}" : ""));
        for (int i = 0; i < best.MemberSets.Length; i++)
        {
            string majority = ext is not null ? $"  [{ext.ClusterMajority[i]}]" : "";
            sb.AppendLine($"  Cluster {i + 1} (center={best.Centers[i]}){majority}: [{string.Join(",", best.MemberSets[i])}]");
        }

        if (hasBeta)
        {
            // the original algorithms require a unique maximiser of the criterion
            if (ClusterResult.DescribeTie(results) is { } tie)
            {
                sb.AppendLine();
                sb.AppendLine($"Note: {tie}");
            }
        }
        else
        {
            var measures = AllotmentMeasures.Compute(best);
            sb.AppendLine();
            sb.AppendLine("Validity measures of the best allotment:");
            sb.AppendLine($"  Linear fuzziness V_LMF          {measures.LinearFuzziness:F6}");
            sb.AppendLine($"  Quadratic fuzziness V_QMF       {measures.QuadraticFuzziness:F6}");
            sb.AppendLine($"  Separation and compactness V_MSC {measures.SeparationCompactness:F6}");
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

    /// <summary>The levels of a hierarchy, coarse to fine: threshold, number of clusters, sizes, typical points.</summary>
    private static string RenderHierarchy(IReadOnlyList<ClusterResult> levels)
    {
        var sb = new StringBuilder();
        string criterionName = levels[0].CriterionName;
        sb.AppendLine($"Hierarchy: {levels.Count} levels, from {levels[0].ClusterCount} to {levels[^1].ClusterCount} clusters");
        sb.AppendLine($"{"Level",-6} {"Alpha",-10} {"c",-5} {criterionName,-10} Sizes (typical points)");
        sb.AppendLine(new string('-', 80));
        for (int index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            string clusters = level.ClusterCount <= 12
                ? string.Join(" ", level.MemberSets.Select((members, cluster) => $"{members.Length}({level.Centers[cluster]})"))
                : string.Join(" ", level.MemberSets.Select(members => members.Length).OrderByDescending(size => size).Take(12)) + " …";
            sb.AppendLine($"{index + 1,-6} {level.Alpha,-10:F6} {level.ClusterCount,-5} {level.Score,-10:F4} {clusters}");
        }
        return sb.ToString();
    }

    private static string RenderJson(
        IReadOnlyList<ClusterResult> results, ExternalValidationResult? ext,
        IReadOnlyList<ExternalValidationResult>? levelValidation, SearchDiagnostics diagnostics)
    {
        static object ValidationDto(ExternalValidationResult validation) => new
        {
            purity              = validation.Purity,
            randIndex           = validation.RandIndex,
            adjustedRandIndex   = validation.AdjustedRandIndex,
            nmi                 = validation.NormalizedMutualInfo,
            fowlkesMallows      = validation.FowlkesMallows,
            clusterMajority     = validation.ClusterMajority
        };

        object ResultDto(ClusterResult r, int index)
        {
            var result = new Dictionary<string, object?>
            {
                ["clusterCount"]   = r.ClusterCount,
                ["alpha"]          = r.Alpha,
                ["score"]          = r.Score,
                ["distTiebreaker"] = r.TiebreakerScore,
                ["criterion"]      = r.CriterionName,
                ["centers"]        = r.Centers,
                ["clusters"]       = r.MemberSets
            };
            if (levelValidation is not null) result["validation"] = ValidationDto(levelValidation[index]);
            if (r.Beta.HasValue)
            {
                result["beta"] = r.Beta;
                result["intersectionBound"] = r.IntersectionBound;
                return result;
            }
            var measures = AllotmentMeasures.Compute(r);
            result["measures"] = new
            {
                linearFuzziness       = measures.LinearFuzziness,
                quadraticFuzziness    = measures.QuadraticFuzziness,
                separationCompactness = measures.SeparationCompactness
            };
            return result;
        }

        var dto = new Dictionary<string, object?>
        {
            ["results"] = results.Select(ResultDto),
            ["search"]  = new
            {
                nodesVisited    = diagnostics.NodesVisited,
                budgetExhausted = diagnostics.BudgetExhausted
            }
        };
        if (ext is not null)
        {
            dto["validation"] = ValidationDto(ext);
        }

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RenderCsv(IReadOnlyList<ClusterResult> results)
    {
        var sb = new StringBuilder();
        bool hasTb = results.Any(r => r.TiebreakerScore.HasValue);
        bool hasBeta = results.Any(r => r.Beta.HasValue);
        string betaHeader = hasBeta ? "beta," : "";
        sb.AppendLine(hasTb ? $"rank,c,alpha,{betaHeader}score,dist_tiebreaker,centers,sizes" : $"rank,c,alpha,{betaHeader}score,centers,sizes");
        int rank = 1;
        foreach (var r in results)
        {
            string tbField = hasTb ? $",{(r.TiebreakerScore.HasValue ? r.TiebreakerScore.Value.ToString("F6", CultureInfo.InvariantCulture) : "")}" : "";
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{rank},{r.ClusterCount},{r.Alpha:F6},{(hasBeta ? $"{r.Beta:F6}," : "")}{r.Score:F6}{tbField},\"{string.Join(";", r.Centers)}\",\"{string.Join(";", r.MemberSets.Select(s => s.Length))}\""));
            rank++;
        }
        return sb.ToString();
    }
}
