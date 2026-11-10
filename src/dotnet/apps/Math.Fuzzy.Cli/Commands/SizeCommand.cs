using System.CommandLine;
using System.Text;
using MathFuzzy.Clustering;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy size --input data.csv --distance squared-euclidean --normalize max
/// mfuzzy bounds --objects 150 --clusters 2 --alpha 0.80384 --shape gaussian
/// </summary>
public static class SizeCommand
{
    /// <summary>
    /// The technique for the maximal number of objects in a fuzzy cluster: D-AFC(u) for a range
    /// of u, with the validity measures of every result and the optimal u*.
    /// </summary>
    public static Command BuildSize()
    {
        var input     = new Option<FileInfo>("--input") { Description = "CSV file with attribute data, or with a fuzzy tolerance matrix when --matrix is given", Required = true };
        var matrix    = new Option<bool>("--matrix") { Description = "The input file is a fuzzy tolerance matrix" };
        var distance  = new Option<string>("--distance") { Description = "Distance: euclidean | squared-euclidean | manhattan | chebyshev | minkowski | cosine | hamming", DefaultValueFactory = _ => "euclidean" };
        var norm      = new Option<string>("--normalize") { Description = "Normalization: minmax | max | none", DefaultValueFactory = _ => "minmax" };
        var accuracy  = new Option<double?>("--accuracy") { Description = "Accuracy threshold ε of the tolerance matrix" };
        var first     = new Option<int?>("--from") { Description = "First value of u (default: the size of the smallest cluster of the principal allotment)" };
        var last      = new Option<int?>("--to") { Description = "Last value of u (default: first + 5, at most n − 1)" };
        var measure   = new Option<string>("--measure") { Description = "Validity measure that decides u*: lmf | qmf | msc", DefaultValueFactory = _ => "lmf" };
        var criterion = new Option<string>("--criterion") { Description = "Quality criterion: f1 | f2", DefaultValueFactory = _ => "f1" };
        var maxNodes  = new Option<long>("--max-nodes") { Description = "Node budget of each combination search", DefaultValueFactory = _ => 500_000_000L };

        var cmd = new Command("size", "Choose the maximal number u of objects in a fuzzy cluster with D-AFC(u) and a validity measure")
            { input, matrix, distance, norm, accuracy, first, last, measure, criterion, maxNodes };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var options = new AlgorithmOptions(
                Distance:      parse.GetValue(distance)!,
                Normalization: parse.GetValue(norm)!,
                Accuracy:      parse.GetValue(accuracy),
                Criterion:     parse.GetValue(criterion)!);
            var quality = AlgorithmFactory.CreateCriterion(options.Criterion);
            var builder = AlgorithmFactory.CreateToleranceBuilder(options);

            FuzzyToleranceMatrix tolerance;
            int? least;
            if (parse.GetValue(matrix))
            {
                tolerance = FuzzyToleranceMatrix.FromCsv(parse.GetRequiredValue(input).FullName);
                least = ClusterSizeSelection.LeastMaxClusterPower(tolerance, quality, ct);
            }
            else
            {
                var data = CsvFiles.Read(parse.GetRequiredValue(input)).Data;
                tolerance = builder.Build(data);
                least = ClusterSizeSelection.LeastMaxClusterPower(data, builder, quality, ct);
            }

            int firstPower = parse.GetValue(first) ?? least
                ?? throw new ArgumentException("The data have no principal allotment, so --from must be given.");
            int lastPower = parse.GetValue(last) ?? Math.Min(firstPower + 5, tolerance.Size - 1);
            string measureName = parse.GetValue(measure)!;

            var diagnostics = new SearchDiagnostics();
            var selection = ClusterSizeSelection.Scan(tolerance, firstPower, lastPower, measureName, quality, diagnostics, ct);

            var text = new StringBuilder();
            if (least is int smallest)
                text.AppendLine($"Smallest cluster of the principal allotment: {smallest} objects (u_min)");
            text.AppendLine($"{"u",-5} {"alpha",-10} {"w",-4} {"c",-4} {"V_LMF",-10} {"V_QMF",-10} {"V_MSC",-10} sizes");
            text.AppendLine(new string('-', 78));
            foreach (var step in selection.Steps)
            {
                if (step.Allotment is not { } allotment || step.Measures is not { } measures)
                {
                    text.AppendLine($"{step.MaxClusterPower,-5} no allotment");
                    continue;
                }
                string sizes = string.Join(",", allotment.MemberSets.Select(members => members.Length));
                text.AppendLine($"{step.MaxClusterPower,-5} {allotment.Alpha,-10:F6} {allotment.IntersectionCount,-4} {allotment.ClusterCount,-4} " +
                                $"{measures.LinearFuzziness,-10:F6} {measures.QuadraticFuzziness,-10:F6} {measures.SeparationCompactness,-10:F6} " +
                                $"[{sizes}]{(step.Tied ? "  (tied typical points)" : "")}");
            }
            text.AppendLine();
            text.AppendLine(selection.OptimalMaxClusterPower is int optimal
                ? $"Optimal maximal number of objects: u* = {optimal} (the {measureName} measure is the same at u = {optimal + 1})"
                : $"No plateau of the {measureName} measure in u = {firstPower}..{lastPower}; widen the range with --to.");
            await Console.Out.WriteAsync(text.ToString());

            if (diagnostics.BudgetExhausted)
                await Console.Error.WriteLineAsync(
                    "Warning: a combination search stopped at its node budget; rows of the table may be incomplete.");
        }));
        return cmd;
    }

    /// <summary>The set of potential numbers of clusters from one result (c clusters at the threshold α).</summary>
    public static Command BuildBounds()
    {
        var objects  = new Option<int>("--objects") { Description = "Number of objects n", Required = true };
        var clusters = new Option<int>("--clusters") { Description = "Number c of fully separated clusters found", Required = true };
        var alpha    = new Option<double>("--alpha") { Description = "Threshold α at which they were found", Required = true };
        var shape    = new Option<string>("--shape") { Description = "Fuzzy number: triangular | gaussian", DefaultValueFactory = _ => "triangular" };

        var cmd = new Command("bounds", "Bounds of the set of potential numbers of fuzzy clusters")
            { objects, clusters, alpha, shape };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            var kind = parse.GetValue(shape)!.ToLowerInvariant() switch
            {
                "triangular" => ClusterCountShape.Triangular,
                "gaussian"   => ClusterCountShape.Gaussian,
                var other    => throw new ArgumentException($"Unknown shape: '{other}'. Valid names: triangular, gaussian")
            };
            var bounds = ClusterCountBounds.Compute(parse.GetValue(objects), parse.GetValue(clusters), parse.GetValue(alpha), kind);

            await Console.Out.WriteLineAsync($"Potential numbers of clusters: {bounds.Least} … {bounds.Largest}");
            for (int index = 0; index < bounds.Possibility.Count; index++)
                await Console.Out.WriteLineAsync($"  c = {bounds.Least + index,-4} possibility {bounds.Possibility[index]:F4}");
        }));
        return cmd;
    }
}
