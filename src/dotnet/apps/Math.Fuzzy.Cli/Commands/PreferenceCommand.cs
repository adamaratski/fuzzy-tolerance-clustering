using System.CommandLine;
using System.Globalization;
using System.Text;
using MathFuzzy.Clustering.Applications.Preference;
using MathFuzzy.Core.Preference;

namespace MathFuzzy.Cli.Commands;

/// <summary>
/// mfuzzy preference --input relations.csv
/// Selection of the most representative fuzzy preference relations of a group of experts by clustering,
/// and the choice of the best alternatives by Orlovsky's non-domination method.
/// </summary>
public static class PreferenceCommand
{
    public static Command Build()
    {
        var input = new Option<FileInfo>("--input") { Description = "CSV file with the preference relations: header expert,alternative,a_1,…,a_n; n rows per expert", Required = true };
        var weights = new Option<string?>("--weights") { Description = "Weights of the relations, one per relation of the file in its order, for the aggregation when several relations are selected (default: equal; renormalised over the selected relations)" };
        var tieTolerance = new Option<double>("--tie-tolerance") { Description = "Values that differ by no more than this are equal", DefaultValueFactory = _ => 1e-12 };
        var output = Reports.OutputOption();
        var format = Reports.FormatOption();

        var cmd = new Command("preference", "Select the most representative fuzzy preference relations and the best alternatives")
            { input, weights, tieTolerance, output, format };
        cmd.SetAction((parse, ct) => CsvFiles.Guard(async () =>
        {
            string formatName = Reports.CheckFormat(parse.GetValue(format)!);
            double tolerance = parse.GetValue(tieTolerance);
            if (!(tolerance >= 0.0)) throw new ArgumentException("--tie-tolerance must not be negative.");

            var relations = FuzzyPreferenceRelationSet.FromCsv(parse.GetRequiredValue(input).FullName);
            var result = FuzzyPreferenceAnalysis.Analyze(relations, new PreferenceAnalysisOptions(TieTolerance: tolerance), ct);

            double[] nonDominated = result.NonDominated;
            int[] ranking = result.Ranking, best = result.BestAlternatives;
            double acceptance = result.AcceptanceDegree;
            if (parse.GetValue(weights) is { } weightText)
            {
                var all = ParseWeights(weightText, relations.ExpertCount);
                var selectedWeights = result.SelectedRelations.Select(expert => all[expert]).ToArray();
                double total = selectedWeights.Sum();
                if (!(total > 0.0))
                    throw new ArgumentException("The weights of the selected relations sum to zero.");
                nonDominated = FuzzyPreferenceAnalysis.AggregatedNonDominatedSet(
                    [.. result.SelectedRelations.Select(relations.Relation)], [.. selectedWeights.Select(weight => weight / total)]);
                ranking = FuzzyPreferenceAnalysis.Rank(nonDominated, tolerance);
                acceptance = nonDominated.Max();
                best = [.. Enumerable.Range(0, nonDominated.Length).Where(alternative => nonDominated[alternative] >= acceptance - tolerance)];
            }

            var experts = relations.ExpertNames;
            var alternatives = relations.AlternativeNames;
            var text = new StringBuilder();
            text.AppendLine($"Relations: {relations.ExpertCount}, alternatives: {relations.AlternativeCount}");
            text.AppendLine();
            text.AppendLine("Kuzmin distances:");
            AppendMatrix(text, result.Distances, experts, "F3");
            text.AppendLine("Tolerance of the relations:");
            AppendMatrix(text, ToArray(result.Tolerance), experts, "F4");

            var allotment = result.Allotment;
            if (allotment.ClusterCount > 0)
            {
                text.AppendLine($"Allotment of the relations: c = {allotment.ClusterCount}, alpha = {allotment.Alpha:F4}, {allotment.CriterionName} = {allotment.Score:F4}");
                for (int cluster = 0; cluster < allotment.ClusterCount; cluster++)
                    text.AppendLine($"  Cluster {cluster + 1}: {{{string.Join(", ", allotment.MemberSets[cluster].Select(member => experts[member]))}}}, typical relation {experts[allotment.Centers[cluster]]}");
                if (result.AllotmentTie is { } tie) text.AppendLine($"Note: {tie}");
            }
            else
            {
                text.AppendLine("No allotment of the relations; every relation is a candidate.");
            }
            text.AppendLine();
            text.AppendLine("Index of fuzziness of every relation:");
            for (int expert = 0; expert < relations.ExpertCount; expert++)
                text.AppendLine($"  {experts[expert],-10} {result.FuzzinessIndices[expert]:F4}" + (result.TypicalRelations.Contains(expert) ? "  (typical)" : ""));
            text.AppendLine($"Selected relation(s): {string.Join(", ", result.SelectedRelations.Select(expert => experts[expert]))}");
            text.AppendLine();
            text.AppendLine("Non-dominated set:");
            for (int alternative = 0; alternative < alternatives.Length; alternative++)
                text.AppendLine($"  {alternatives[alternative],-10} {nonDominated[alternative].ToString("F4", CultureInfo.InvariantCulture)}");
            text.AppendLine($"Ranking: {string.Join(" > ", ranking.Select(alternative => alternatives[alternative]))}");
            text.AppendLine($"Best alternative(s): {string.Join(", ", best.Select(alternative => alternatives[alternative]))} with degree {acceptance.ToString("F4", CultureInfo.InvariantCulture)}");

            await Reports.Emit(formatName, text.ToString(), () => new
            {
                experts,
                alternatives,
                distances = ToJagged(result.Distances),
                tolerance = ToJagged(ToArray(result.Tolerance)),
                allotment = allotment.ClusterCount == 0 ? null : new
                {
                    clusterCount = allotment.ClusterCount,
                    alpha = allotment.Alpha,
                    clusters = allotment.MemberSets.Select((members, cluster) => new
                    {
                        relations = members.Select(member => experts[member]).ToArray(),
                        typicalRelation = experts[allotment.Centers[cluster]]
                    }),
                    tie = result.AllotmentTie
                },
                typicalRelations = result.TypicalRelations.Select(expert => experts[expert]),
                fuzzinessIndices = experts.Select((name, expert) => new { relation = name, index = result.FuzzinessIndices[expert] }),
                selectedRelations = result.SelectedRelations.Select(expert => experts[expert]),
                nonDominated = alternatives.Select((name, alternative) => new { alternative = name, degree = nonDominated[alternative] }),
                ranking = ranking.Select(alternative => alternatives[alternative]),
                bestAlternatives = best.Select(alternative => alternatives[alternative]),
                acceptanceDegree = acceptance
            }, parse.GetValue(output));
        }));
        return cmd;
    }

    private static double[] ParseWeights(string text, int expected)
    {
        double[] values;
        try
        {
            values = [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => double.Parse(part, CultureInfo.InvariantCulture))];
        }
        catch (FormatException)
        {
            throw new ArgumentException($"--weights expects numbers separated by commas; found '{text}'.");
        }
        if (values.Length != expected)
            throw new ArgumentException($"--weights: {values.Length} weights for {expected} relations.");
        if (values.Any(value => !(value >= 0.0)))
            throw new ArgumentException("--weights: the weights must not be negative.");
        return values;
    }

    private static double[,] ToArray(MathFuzzy.Core.Matrix.FuzzyToleranceMatrix matrix)
    {
        var values = new double[matrix.Size, matrix.Size];
        for (int row = 0; row < matrix.Size; row++)
            for (int column = 0; column < matrix.Size; column++)
                values[row, column] = matrix[row, column];
        return values;
    }

    private static double[][] ToJagged(double[,] values) =>
        [.. Enumerable.Range(0, values.GetLength(0)).Select(row => Enumerable.Range(0, values.GetLength(1)).Select(column => values[row, column]).ToArray())];

    private static void AppendMatrix(StringBuilder text, double[,] values, IReadOnlyList<string> names, string valueFormat)
    {
        text.Append($"{"",-8}");
        foreach (string name in names) text.Append($" {name,-8}");
        text.AppendLine();
        for (int row = 0; row < names.Count; row++)
        {
            text.Append($"{names[row],-8}");
            for (int column = 0; column < names.Count; column++)
                text.Append($" {values[row, column].ToString(valueFormat, CultureInfo.InvariantCulture),-8}");
            text.AppendLine();
        }
        text.AppendLine();
    }
}
