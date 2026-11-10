using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Core.ThreeWay;

/// <summary>
/// Generalised distance between two objects of three-way data (Viattchenin 2013, eqs. (3.53)–(3.55)).
/// </summary>
public enum ThreeWayDistance
{
    /// <summary>
    /// Generalised normalised Hamming distance, eq. (3.53):
    /// l_G(x_i, x_k) = (1/m)·Σ_t (1/S²)·Σ_s Σ_s' |μ_i(t, s) − μ_k(t, s')|.
    /// </summary>
    Hamming,

    /// <summary>Generalised normalised Euclidean distance, eq. (3.54): e_G = √ε_G.</summary>
    Euclidean,

    /// <summary>
    /// Generalised squared normalised Euclidean distance, eq. (3.55):
    /// ε_G(x_i, x_k) = (1/m)·Σ_t (1/S²)·Σ_s Σ_s' (μ_i(t, s) − μ_k(t, s'))².
    /// </summary>
    SquaredEuclidean,
}

/// <summary>
/// Three-way data x_its: n objects, m attributes and S situations (for example time points),
/// Viattchenin 2013, sec. 3.3 (poly-matrix (3.50)). After normalisation every object is an
/// m × S matrix of membership values, a type-two fuzzy set; the generalised distances compare every
/// pair of situations, and the fuzzy tolerance is their complement.
/// </summary>
public sealed class ThreeWayDataMatrix
{
    private readonly double[] _values;   // [object, attribute, situation], row-major

    public int Objects { get; }
    public int Attributes { get; }
    public int Situations { get; }

    public IReadOnlyList<string> ObjectNames { get; }
    public IReadOnlyList<string> AttributeNames { get; }
    public IReadOnlyList<string> SituationNames { get; }

    /// <summary>Creates the data from a table indexed [object, attribute, situation].</summary>
    public ThreeWayDataMatrix(
        double[,,] values,
        IReadOnlyList<string>? objectNames = null,
        IReadOnlyList<string>? attributeNames = null,
        IReadOnlyList<string>? situationNames = null)
    {
        Objects = values.GetLength(0);
        Attributes = values.GetLength(1);
        Situations = values.GetLength(2);
        if (Objects == 0 || Attributes == 0 || Situations == 0)
            throw new ArgumentException("Three-way data need at least one object, one attribute and one situation.");

        _values = new double[Objects * Attributes * Situations];
        for (int objectIndex = 0; objectIndex < Objects; objectIndex++)
            for (int attribute = 0; attribute < Attributes; attribute++)
                for (int situation = 0; situation < Situations; situation++)
                {
                    double value = values[objectIndex, attribute, situation];
                    if (!double.IsFinite(value))
                        throw new ArgumentException(
                            $"Object {objectIndex + 1}, attribute {attribute + 1}, situation {situation + 1}: the value must be finite.");
                    _values[Index(objectIndex, attribute, situation)] = value;
                }

        ObjectNames = CheckNames(objectNames, Objects, "object", "x");
        AttributeNames = CheckNames(attributeNames, Attributes, "attribute", "a");
        SituationNames = CheckNames(situationNames, Situations, "situation", "s");
    }

    private static IReadOnlyList<string> CheckNames(IReadOnlyList<string>? names, int count, string what, string prefix)
    {
        if (names is null) return [.. Enumerable.Range(1, count).Select(index => $"{prefix}{index}")];
        if (names.Count != count) throw new ArgumentException($"One name per {what} is expected.");
        return [.. names];
    }

    private int Index(int objectIndex, int attribute, int situation)
        => (objectIndex * Attributes + attribute) * Situations + situation;

    /// <summary>The value of attribute <paramref name="attribute"/> of an object in a situation.</summary>
    public double this[int objectIndex, int attribute, int situation] => _values[Index(objectIndex, attribute, situation)];

    /// <summary>
    /// The data normalised attribute by attribute, with the extreme values taken over all objects
    /// and all situations: <see cref="MinMaxNormalization"/> gives eq. (3.52),
    /// (x − min)/(max − min), and <see cref="MaxNormalization"/> gives eq. (3.51), x / max
    /// (Viattchenin 2013, sec. 3.3.2). The normalisation is applied to the (n·S) × m table in
    /// which every object appears once per situation.
    /// </summary>
    public ThreeWayDataMatrix Normalize(INormalization normalization)
    {
        var stacked = new RawDataMatrix(Objects * Situations, Attributes);
        for (int objectIndex = 0; objectIndex < Objects; objectIndex++)
            for (int situation = 0; situation < Situations; situation++)
                for (int attribute = 0; attribute < Attributes; attribute++)
                    stacked[objectIndex * Situations + situation, attribute] = this[objectIndex, attribute, situation];

        var normalized = normalization.Apply(stacked);
        var values = new double[Objects, Attributes, Situations];
        for (int objectIndex = 0; objectIndex < Objects; objectIndex++)
            for (int situation = 0; situation < Situations; situation++)
                for (int attribute = 0; attribute < Attributes; attribute++)
                    values[objectIndex, attribute, situation] = normalized[objectIndex * Situations + situation, attribute];
        return new ThreeWayDataMatrix(values, ObjectNames, AttributeNames, SituationNames);
    }

    /// <summary>
    /// Generalised distance between two objects (eqs. (3.53)–(3.55)) on the values as they are; the
    /// data should be normalised first. The distance of an object to itself is not zero when its
    /// values differ between situations.
    /// </summary>
    public double Distance(int firstObject, int secondObject, ThreeWayDistance distance)
    {
        double sum = 0.0;
        for (int attribute = 0; attribute < Attributes; attribute++)
        {
            int first = Index(firstObject, attribute, 0), second = Index(secondObject, attribute, 0);
            double attributeSum = 0.0;
            for (int situation = 0; situation < Situations; situation++)
                for (int otherSituation = 0; otherSituation < Situations; otherSituation++)
                {
                    double difference = _values[first + situation] - _values[second + otherSituation];
                    attributeSum += distance == ThreeWayDistance.Hamming ? Math.Abs(difference) : difference * difference;
                }
            sum += attributeSum / ((double)Situations * Situations);
        }
        double mean = sum / Attributes;
        return distance == ThreeWayDistance.Euclidean ? Math.Sqrt(mean) : mean;
    }

    /// <summary>
    /// The generalised similarity 1 − d(x_i, x_k), clamped to [0, 1], for every pair including
    /// i = k. Its diagonal holds 1 − d(x_i, x_i) &lt; 1 for an object whose values change between
    /// situations.
    /// </summary>
    public double[,] Similarities(ThreeWayDistance distance)
    {
        var similarities = new double[Objects, Objects];
        for (int row = 0; row < Objects; row++)
            for (int column = row; column < Objects; column++)
            {
                double value = Math.Clamp(1.0 - Distance(row, column, distance), 0.0, 1.0);
                similarities[row, column] = value;
                similarities[column, row] = value;
            }
        return similarities;
    }

    /// <summary>
    /// The fuzzy tolerance built as the monograph states it (Viattchenin 2013, sec. 3.3.3):
    /// normalisation (eq. (3.52) with <see cref="MinMaxNormalization"/>, eq. (3.51) with
    /// <see cref="MaxNormalization"/>, none with <see cref="NoNormalization"/>), a generalised
    /// distance (3.53)–(3.55) and the complement (1.26): T_ik = 1 − d(x_i, x_k).
    /// <para>
    /// By default the diagonal is set to 1, so that T is a fuzzy tolerance. With
    /// <paramref name="keepSelfSimilarity"/> it holds 1 − d(x_i, x_i) instead, the relation on which
    /// the published memberships of the Sato–Sato example were computed: the membership of an
    /// object in its own α-cluster is then below 1, and the object with the largest membership in a
    /// cluster need not be the object that generates it.
    /// </para>
    /// With <paramref name="accuracy"/> the values are rounded to multiples of ε; otherwise values
    /// that differ by rounding noise only are merged.
    /// </summary>
    public FuzzyToleranceMatrix BuildTolerance(
        INormalization normalization, ThreeWayDistance distance,
        bool keepSelfSimilarity = false, double? accuracy = null)
    {
        var similarities = Normalize(normalization).Similarities(distance);
        if (!keepSelfSimilarity)
            for (int objectIndex = 0; objectIndex < Objects; objectIndex++)
                similarities[objectIndex, objectIndex] = 1.0;

        var tolerance = FuzzyToleranceMatrix.FromArray(similarities);
        if (accuracy is double epsilon) return tolerance.Quantize(epsilon);   // the diagonal is kept as it is
        tolerance.SnapLevels();
        return tolerance;
    }

    /// <summary>
    /// Reads three-way data from a delimited text file with a header row and one row per object and
    /// situation: a column with the object identifier, a column with the situation, and one numeric
    /// column per attribute. By default the first column identifies the object and the second the
    /// situation; other columns can be named. Objects and situations keep the order of their first
    /// appearance. Every object must have exactly one row for every situation.
    /// </summary>
    public static ThreeWayDataMatrix FromCsv(string path, string? objectColumn = null, string? situationColumn = null)
        => FromTable(CsvTable.Load(path), objectColumn, situationColumn);

    /// <summary>Reads three-way data from delimited text; see <see cref="FromCsv"/>.</summary>
    public static ThreeWayDataMatrix Parse(
        string text, string? objectColumn = null, string? situationColumn = null, string sourceName = "input")
        => FromTable(CsvTable.Parse(text, sourceName), objectColumn, situationColumn);

    private static ThreeWayDataMatrix FromTable(CsvTable table, string? objectColumn, string? situationColumn)
    {
        if (table.Header is null)
            throw new FormatException($"{table.SourceName}: three-way data need a header row (object, situation, attributes).");
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");
        if (table.ColumnCount < 3)
            throw new FormatException($"{table.SourceName}: an object column, a situation column and at least one attribute column are expected.");

        int objectIndexColumn = FindColumn(table, objectColumn, 0);
        int situationIndexColumn = FindColumn(table, situationColumn, objectIndexColumn == 1 ? 0 : 1);
        if (objectIndexColumn == situationIndexColumn)
            throw new FormatException($"{table.SourceName}: the object and the situation column must differ.");

        var attributeColumns = Enumerable.Range(0, table.ColumnCount)
            .Where(column => column != objectIndexColumn && column != situationIndexColumn)
            .ToArray();
        foreach (int column in attributeColumns)
            if (!table.IsNumericColumn(column))
                throw new FormatException($"{table.SourceName}: attribute column {table.ColumnName(column)} is not numeric.");

        var objectNames = new List<string>();
        var situationNames = new List<string>();
        var objectPosition = new Dictionary<string, int>(StringComparer.Ordinal);
        var situationPosition = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            if (objectPosition.TryAdd(row[objectIndexColumn], objectNames.Count)) objectNames.Add(row[objectIndexColumn]);
            if (situationPosition.TryAdd(row[situationIndexColumn], situationNames.Count)) situationNames.Add(row[situationIndexColumn]);
        }

        int objects = objectNames.Count, situations = situationNames.Count, attributes = attributeColumns.Length;
        var values = new double[objects, attributes, situations];
        var seen = new bool[objects, situations];
        for (int rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            int objectIndex = objectPosition[row[objectIndexColumn]];
            int situation = situationPosition[row[situationIndexColumn]];
            if (seen[objectIndex, situation])
                throw new FormatException(
                    $"{table.SourceName}: object {row[objectIndexColumn]} has two rows for situation {row[situationIndexColumn]}.");
            seen[objectIndex, situation] = true;
            for (int attribute = 0; attribute < attributes; attribute++)
                values[objectIndex, attribute, situation] = table.Number(rowIndex, attributeColumns[attribute]);
        }

        for (int objectIndex = 0; objectIndex < objects; objectIndex++)
            for (int situation = 0; situation < situations; situation++)
                if (!seen[objectIndex, situation])
                    throw new FormatException(
                        $"{table.SourceName}: object {objectNames[objectIndex]} has no row for situation {situationNames[situation]}; " +
                        "every object must be observed in every situation.");

        return new ThreeWayDataMatrix(
            values, objectNames,
            [.. attributeColumns.Select(column => table.Header[column])],
            situationNames);
    }

    private static int FindColumn(CsvTable table, string? name, int fallback)
    {
        if (name is null) return fallback;
        for (int column = 0; column < table.ColumnCount; column++)
            if (string.Equals(table.Header![column], name, StringComparison.OrdinalIgnoreCase)) return column;
        throw new FormatException($"{table.SourceName}: there is no column named {name}.");
    }
}
