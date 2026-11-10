using System.Globalization;
using System.Text;
using MathFuzzy.Core.IO;

namespace MathFuzzy.Core.Preference;

/// <summary>
/// A set ℛ = {R_1, …, R_g} of fuzzy preference relations of several experts on the same finite set
/// of alternatives {a_1, …, a_n}; μ_{R_f}(a_i, a_j) ∈ [0, 1] is the degree to which expert f
/// considers a_i at least as good as a_j (a weak fuzzy preference relation is reflexive,
/// μ(a_i, a_i) = 1; Viattchenin 2013, sec. 4.4).
///
/// <para><b>File format (one file).</b> A header row <c>expert,alternative,a_1,…,a_n</c> names the
/// alternatives; every expert contributes n rows <c>expert name, alternative name, μ(a_i, a_1), …,
/// μ(a_i, a_n)</c>, in which the second cell names the first argument a_i. The rows of an expert
/// may come in any order but each alternative occurs exactly once; the experts are kept in the
/// order of their first row. Comma, semicolon or tab separate the cells (see <see cref="CsvTable"/>).</para>
///
/// <para><b>File format (one file per expert).</b> <see cref="FromMatrixFiles"/> reads square
/// matrices with a header row of alternative names and an optional first column of names; the
/// expert is named after the file.</para>
/// </summary>
public sealed class FuzzyPreferenceRelationSet
{
    private readonly double[][,] _relations;

    /// <summary>Names of the alternatives, in the order of the rows and columns of every relation.</summary>
    public string[] AlternativeNames { get; }

    /// <summary>Names of the experts (relations).</summary>
    public string[] ExpertNames { get; }

    public int AlternativeCount => AlternativeNames.Length;
    public int ExpertCount => ExpertNames.Length;

    /// <summary>
    /// Creates the set from square matrices of equal size with values in [0, 1]. Missing names are
    /// generated: R1, R2, … for the experts and a1, a2, … for the alternatives.
    /// </summary>
    public FuzzyPreferenceRelationSet(IReadOnlyList<double[,]> relations,
                                      IReadOnlyList<string>? expertNames = null,
                                      IReadOnlyList<string>? alternativeNames = null)
    {
        if (relations.Count == 0) throw new ArgumentException("At least one preference relation is needed.", nameof(relations));
        int size = relations[0].GetLength(0);
        for (int expert = 0; expert < relations.Count; expert++)
        {
            var relation = relations[expert];
            if (relation.GetLength(0) != size || relation.GetLength(1) != size)
                throw new ArgumentException($"Relation {expert + 1} is not a {size} × {size} matrix.", nameof(relations));
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                {
                    double value = relation[row, column];
                    if (!(value >= 0.0 && value <= 1.0))
                        throw new ArgumentException(
                            $"Relation {expert + 1}: value {value.ToString(CultureInfo.InvariantCulture)} at ({row + 1}, {column + 1}) lies outside [0, 1].",
                            nameof(relations));
                }
        }
        if (expertNames is not null && expertNames.Count != relations.Count)
            throw new ArgumentException($"{expertNames.Count} expert names for {relations.Count} relations.", nameof(expertNames));
        if (alternativeNames is not null && alternativeNames.Count != size)
            throw new ArgumentException($"{alternativeNames.Count} alternative names for {size} alternatives.", nameof(alternativeNames));

        _relations = [.. relations.Select(relation => (double[,])relation.Clone())];
        ExpertNames = expertNames is null ? [.. Enumerable.Range(1, relations.Count).Select(index => $"R{index}")] : [.. expertNames];
        AlternativeNames = alternativeNames is null ? [.. Enumerable.Range(1, size).Select(index => $"a{index}")] : [.. alternativeNames];
    }

    /// <summary>μ_{R_expert}(a_row, a_column).</summary>
    public double this[int expert, int row, int column] => _relations[expert][row, column];

    /// <summary>A copy of the matrix of one relation, [first argument, second argument].</summary>
    public double[,] Relation(int expert) => (double[,])_relations[expert].Clone();

    /// <summary>Copies of all relations, in the order of the experts.</summary>
    public IReadOnlyList<double[,]> Relations => [.. _relations.Select(relation => (double[,])relation.Clone())];

    /// <summary>True when μ(a_i, a_i) = 1 for every alternative (a weak preference relation).</summary>
    public bool IsReflexive(int expert)
    {
        for (int alternative = 0; alternative < AlternativeCount; alternative++)
            if (_relations[expert][alternative, alternative] != 1.0) return false;
        return true;
    }

    /// <summary>The relations of the given experts, as a new set.</summary>
    public FuzzyPreferenceRelationSet Select(IReadOnlyList<int> experts) =>
        new([.. experts.Select(expert => _relations[expert])], [.. experts.Select(expert => ExpertNames[expert])], AlternativeNames);

    public static FuzzyPreferenceRelationSet FromCsv(string path) => Parse(File.ReadAllText(path), Path.GetFileName(path));

    /// <summary>Reads the one-file format described in the class summary.</summary>
    public static FuzzyPreferenceRelationSet Parse(string text, string sourceName = "input")
    {
        var table = CsvTable.Parse(text, sourceName);
        if (table.Header is null)
            throw new FormatException(
                $"{sourceName}: a header row 'expert,alternative,<names of the alternatives>' is expected; " +
                "the names of the alternatives must not be numbers.");
        int alternativeCount = table.ColumnCount - 2;
        if (alternativeCount < 1)
            throw new FormatException($"{sourceName}: an expert column, an alternative column and one column per alternative are expected.");

        var alternatives = table.Header[2..];
        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int alternative = 0; alternative < alternativeCount; alternative++)
            if (!position.TryAdd(alternatives[alternative], alternative))
                throw new FormatException($"{sourceName}: the alternative '{alternatives[alternative]}' is named twice in the header.");

        var experts = new List<string>();
        var matrices = new Dictionary<string, (double[,] Matrix, bool[] Seen)>(StringComparer.Ordinal);
        for (int row = 0; row < table.Rows.Length; row++)
        {
            int line = row + 2;
            string expert = table.Rows[row][0];
            string alternativeName = table.Rows[row][1];
            if (expert.Length == 0) throw new FormatException($"{sourceName}: line {line} has no expert name.");
            if (!position.TryGetValue(alternativeName, out int alternativeIndex))
                throw new FormatException($"{sourceName}: line {line}: '{alternativeName}' is not one of the alternatives of the header.");
            if (!matrices.TryGetValue(expert, out var entry))
            {
                entry = (new double[alternativeCount, alternativeCount], new bool[alternativeCount]);
                matrices[expert] = entry;
                experts.Add(expert);
            }
            if (entry.Seen[alternativeIndex])
                throw new FormatException($"{sourceName}: line {line}: expert '{expert}' has a second row for '{alternativeName}'.");
            entry.Seen[alternativeIndex] = true;
            for (int column = 0; column < alternativeCount; column++)
            {
                double value = table.Number(row, column + 2);
                if (value < 0.0 || value > 1.0)
                    throw new FormatException(
                        $"{sourceName}: line {line}, {table.ColumnName(column + 2)}: value {value.ToString(CultureInfo.InvariantCulture)} lies outside [0, 1].");
                entry.Matrix[alternativeIndex, column] = value;
            }
        }
        if (experts.Count == 0) throw new FormatException($"{sourceName}: the file contains no data rows.");
        foreach (string expert in experts)
        {
            int missing = Array.IndexOf(matrices[expert].Seen, false);
            if (missing >= 0)
                throw new FormatException($"{sourceName}: expert '{expert}' has no row for '{alternatives[missing]}'.");
        }
        return new FuzzyPreferenceRelationSet([.. experts.Select(expert => matrices[expert].Matrix)], experts, alternatives);
    }

    /// <summary>
    /// Reads one square matrix per file (header row of alternative names, optional first column of
    /// names); the expert is named after the file without its extension. All files must name the
    /// same alternatives in the same order.
    /// </summary>
    public static FuzzyPreferenceRelationSet FromMatrixFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) throw new ArgumentException("At least one file is needed.", nameof(paths));
        var matrices = new List<double[,]>();
        string[]? alternatives = null;
        foreach (string path in paths)
        {
            var table = CsvTable.Load(path);
            int firstColumn = table.IsNumericColumn(0) ? 0 : 1;
            int size = table.Rows.Length;
            if (size == 0 || table.ColumnCount - firstColumn != size)
                throw new FormatException(
                    $"{table.SourceName}: a square matrix is expected, found {size} rows and {table.ColumnCount - firstColumn} columns.");
            string[] names = table.Header is null
                ? [.. Enumerable.Range(1, size).Select(index => $"a{index}")]
                : table.Header[firstColumn..];
            if (alternatives is null) alternatives = names;
            else if (!alternatives.SequenceEqual(names, StringComparer.Ordinal))
                throw new FormatException($"{table.SourceName}: the alternatives differ from those of the first file.");

            var matrix = new double[size, size];
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                {
                    double value = table.Number(row, column + firstColumn);
                    if (value < 0.0 || value > 1.0)
                        throw new FormatException(
                            $"{table.SourceName}: value {value.ToString(CultureInfo.InvariantCulture)} at row {row + 1}, column {column + 1} lies outside [0, 1].");
                    matrix[row, column] = value;
                }
            matrices.Add(matrix);
        }
        return new FuzzyPreferenceRelationSet(matrices, [.. paths.Select(Path.GetFileNameWithoutExtension).Select(name => name!)], alternatives);
    }

    /// <summary>Writes the set in the one-file format.</summary>
    public string ToCsv()
    {
        var builder = new StringBuilder();
        builder.Append("expert,alternative,").AppendJoin(',', AlternativeNames.Select(Quote)).Append('\n');
        for (int expert = 0; expert < ExpertCount; expert++)
            for (int row = 0; row < AlternativeCount; row++)
            {
                builder.Append(Quote(ExpertNames[expert])).Append(',').Append(Quote(AlternativeNames[row]));
                for (int column = 0; column < AlternativeCount; column++)
                    builder.Append(',').Append(_relations[expert][row, column].ToString("R", CultureInfo.InvariantCulture));
                builder.Append('\n');
            }
        return builder.ToString();
    }

    private static string Quote(string name) =>
        name.IndexOfAny([',', ';', '\t', '"', '\n']) >= 0 ? $"\"{name.Replace("\"", "\"\"")}\"" : name;
}
