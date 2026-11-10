using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.FuzzyData;

/// <summary>
/// Fuzzy data: every object is a vector of fuzzy numbers V_i = (V_i¹, …, V_iᵐ) whose components
/// are of the same kind (Viattchenin 2013, sec. 3.2, def. 3.15). The parameters of a component are
/// <list type="bullet">
/// <item>(m, a, b) for an LR-type or triangular fuzzy number: modal value, left and right spread;</item>
/// <item>(m̲, m̄, a, b) for an LR-type or trapezoidal fuzzy interval: the two ends of the core and the spreads;</item>
/// <item>(m, σ) for a Gaussian fuzzy number.</item>
/// </list>
/// The fuzzy tolerance is built as the monograph states it (sec. 3.2.2): the dissimilarity of two
/// objects is the average of the squared component distances (eqs. (3.41)–(3.42)), the matrix of
/// dissimilarities is divided by its maximum (eq. (2.20); the diagonal is 0) and complemented
/// (eq. (1.26)).
/// </summary>
public sealed class FuzzyNumberDataMatrix
{
    private readonly double[] _parameters;   // [object, attribute, parameter], row-major

    public FuzzyNumberKind Kind { get; }
    public int Objects { get; }
    public int Attributes { get; }

    /// <summary>Number of parameters of one fuzzy value: 3 (m, a, b), 4 (m̲, m̄, a, b) or 2 (m, σ).</summary>
    public int ParameterCount { get; }

    /// <summary>Names of the attributes (the header names without their suffix, or a1…am).</summary>
    public IReadOnlyList<string> AttributeNames { get; }

    /// <summary>Names of the objects (from a text column of the file), or null.</summary>
    public IReadOnlyList<string>? ObjectNames { get; }

    /// <summary>
    /// Creates the data from a table of parameters indexed [object, attribute, parameter]; the
    /// parameters are in the order (m, a, b), (m̲, m̄, a, b) or (m, σ) according to <paramref name="kind"/>.
    /// </summary>
    public FuzzyNumberDataMatrix(
        FuzzyNumberKind kind, double[,,] parameters,
        IReadOnlyList<string>? attributeNames = null, IReadOnlyList<string>? objectNames = null)
    {
        Kind = kind;
        ParameterCount = ParameterCountOf(kind);
        Objects = parameters.GetLength(0);
        Attributes = parameters.GetLength(1);
        if (parameters.GetLength(2) != ParameterCount)
            throw new ArgumentException(
                $"A {kind} value has {ParameterCount} parameters; the table has {parameters.GetLength(2)}.");
        if (Objects == 0 || Attributes == 0)
            throw new ArgumentException("Fuzzy data need at least one object and one attribute.");
        if (attributeNames is not null && attributeNames.Count != Attributes)
            throw new ArgumentException("One name per attribute is expected.");
        if (objectNames is not null && objectNames.Count != Objects)
            throw new ArgumentException("One name per object is expected.");

        _parameters = new double[Objects * Attributes * ParameterCount];
        for (int objectIndex = 0; objectIndex < Objects; objectIndex++)
            for (int attribute = 0; attribute < Attributes; attribute++)
            {
                for (int parameter = 0; parameter < ParameterCount; parameter++)
                {
                    double value = parameters[objectIndex, attribute, parameter];
                    if (!double.IsFinite(value))
                        throw new ArgumentException($"Object {objectIndex + 1}, attribute {attribute + 1}: the parameters must be finite.");
                    _parameters[(objectIndex * Attributes + attribute) * ParameterCount + parameter] = value;
                }
                Validate(objectIndex, attribute);
            }

        AttributeNames = attributeNames ?? [.. Enumerable.Range(1, Attributes).Select(index => $"a{index}")];
        ObjectNames = objectNames;
    }

    /// <summary>Number of parameters of a fuzzy value of the given kind.</summary>
    public static int ParameterCountOf(FuzzyNumberKind kind) => kind switch
    {
        FuzzyNumberKind.FuzzyNumber => 3,
        FuzzyNumberKind.FuzzyInterval => 4,
        FuzzyNumberKind.Gaussian => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Column suffixes of the parameters of the given kind, in parameter order.</summary>
    public static IReadOnlyList<string> SuffixesOf(FuzzyNumberKind kind) => kind switch
    {
        FuzzyNumberKind.FuzzyNumber => ["m", "a", "b"],
        FuzzyNumberKind.FuzzyInterval => ["ml", "mr", "a", "b"],
        FuzzyNumberKind.Gaussian => ["m", "sigma"],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Parameter <paramref name="parameter"/> of attribute <paramref name="attribute"/> of an object.</summary>
    public double Parameter(int objectIndex, int attribute, int parameter)
        => _parameters[(objectIndex * Attributes + attribute) * ParameterCount + parameter];

    private void Validate(int objectIndex, int attribute)
    {
        string where = $"Object {objectIndex + 1}, attribute {attribute + 1}";
        switch (Kind)
        {
            case FuzzyNumberKind.FuzzyNumber:
                if (Parameter(objectIndex, attribute, 1) < 0.0 || Parameter(objectIndex, attribute, 2) < 0.0)
                    throw new ArgumentException($"{where}: the spreads a and b must not be negative.");
                break;
            case FuzzyNumberKind.FuzzyInterval:
                if (Parameter(objectIndex, attribute, 0) > Parameter(objectIndex, attribute, 1))
                    throw new ArgumentException($"{where}: the lower end of the core exceeds the upper end.");
                if (Parameter(objectIndex, attribute, 2) < 0.0 || Parameter(objectIndex, attribute, 3) < 0.0)
                    throw new ArgumentException($"{where}: the spreads a and b must not be negative.");
                break;
            case FuzzyNumberKind.Gaussian:
                if (Parameter(objectIndex, attribute, 1) < 0.0)
                    throw new ArgumentException($"{where}: the width σ must not be negative.");
                break;
        }
    }

    /// <summary>
    /// Squared distance d² between attribute <paramref name="attribute"/> of two objects, by the
    /// formula of the kind of the data (eqs. (1.107), (3.37)–(3.40)).
    /// </summary>
    public double SquaredComponentDistance(
        int firstObject, int secondObject, int attribute,
        double leftShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral,
        double rightShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral)
    {
        int first = (firstObject * Attributes + attribute) * ParameterCount;
        int second = (secondObject * Attributes + attribute) * ParameterCount;
        var values = _parameters;
        return Kind switch
        {
            FuzzyNumberKind.FuzzyNumber => FuzzyNumberDistances.LeftRightSquared(
                values[first], values[first + 1], values[first + 2],
                values[second], values[second + 1], values[second + 2],
                leftShapeIntegral, rightShapeIntegral),
            FuzzyNumberKind.FuzzyInterval => FuzzyNumberDistances.LeftRightIntervalSquared(
                values[first], values[first + 1], values[first + 2], values[first + 3],
                values[second], values[second + 1], values[second + 2], values[second + 3],
                leftShapeIntegral, rightShapeIntegral),
            FuzzyNumberKind.Gaussian => FuzzyNumberDistances.GaussianSquared(
                values[first], values[first + 1], values[second], values[second + 1]),
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Dissimilarity of two objects, the average of the squared component distances
    /// D(x_i, x_k) = (1/m)·Σ_t d²(V_i^t, V_k^t) (Viattchenin 2013, eqs. (3.41)–(3.42)); no square root is taken.
    /// </summary>
    public double Dissimilarity(
        int firstObject, int secondObject,
        double leftShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral,
        double rightShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral)
    {
        double sum = 0.0;
        for (int attribute = 0; attribute < Attributes; attribute++)
            sum += SquaredComponentDistance(firstObject, secondObject, attribute, leftShapeIntegral, rightShapeIntegral);
        return sum / Attributes;
    }

    /// <summary>The n × n matrix of the dissimilarities <see cref="Dissimilarity"/>, before normalisation.</summary>
    public double[,] Dissimilarities(
        double leftShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral,
        double rightShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral)
    {
        CheckShapeIntegral(leftShapeIntegral, nameof(leftShapeIntegral));
        CheckShapeIntegral(rightShapeIntegral, nameof(rightShapeIntegral));
        var dissimilarities = new double[Objects, Objects];
        for (int row = 0; row < Objects; row++)
            for (int column = row + 1; column < Objects; column++)
            {
                double value = Dissimilarity(row, column, leftShapeIntegral, rightShapeIntegral);
                dissimilarities[row, column] = value;
                dissimilarities[column, row] = value;
            }
        return dissimilarities;
    }

    /// <summary>
    /// The fuzzy tolerance T_ik = 1 − D(x_i, x_k) / max D (Viattchenin 2013, sec. 3.2.2: eq. (3.41),
    /// normalisation (2.20), complement (1.26)). With <paramref name="accuracy"/> the values are
    /// rounded to multiples of ε (<see cref="FuzzyToleranceMatrix.Quantize"/>); otherwise values that
    /// differ by rounding noise only are merged (<see cref="FuzzyToleranceMatrix.SnapLevels"/>).
    /// The shape integrals l and r apply to LR-type values; they are not used for Gaussian values.
    /// </summary>
    public FuzzyToleranceMatrix BuildTolerance(
        double leftShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral,
        double rightShapeIntegral = FuzzyNumberDistances.LinearShapeIntegral,
        double? accuracy = null)
    {
        var dissimilarities = Dissimilarities(leftShapeIntegral, rightShapeIntegral);
        double maximum = 0.0;
        foreach (double value in dissimilarities)
            if (value > maximum) maximum = value;

        var tolerance = new FuzzyToleranceMatrix(Objects);
        for (int row = 0; row < Objects; row++)
            for (int column = row + 1; column < Objects; column++)
                tolerance[row, column] = maximum > 0.0
                    ? Math.Clamp(1.0 - dissimilarities[row, column] / maximum, 0.0, 1.0)
                    : 1.0;

        if (accuracy is double epsilon) return tolerance.Quantize(epsilon);
        tolerance.SnapLevels();
        return tolerance;
    }

    private static void CheckShapeIntegral(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new ArgumentOutOfRangeException(name, "A shape integral must be a finite non-negative number.");
    }

    /// <summary>
    /// Reads fuzzy data from a delimited text file with a header row and one object per row.
    /// Every attribute takes several columns named <c>name_suffix</c>:
    /// <c>name_m, name_a, name_b</c> for LR-type (triangular) fuzzy numbers,
    /// <c>name_ml, name_mr, name_a, name_b</c> for LR-type (trapezoidal) fuzzy intervals,
    /// <c>name_m, name_sigma</c> for Gaussian fuzzy numbers. The columns of an attribute may stand in
    /// any order; the attributes keep the order of their first column. All attributes must be of one
    /// kind. A column whose name has none of these suffixes is not an attribute; the first text
    /// column of that kind gives the object names.
    /// </summary>
    public static FuzzyNumberDataMatrix FromCsv(string path) => FromTable(CsvTable.Load(path));

    /// <summary>Reads fuzzy data from delimited text; see <see cref="FromCsv"/>.</summary>
    public static FuzzyNumberDataMatrix Parse(string text, string sourceName = "input")
        => FromTable(CsvTable.Parse(text, sourceName));

    private static readonly string[] KnownSuffixes = ["ml", "mr", "m", "a", "b", "sigma"];

    private static FuzzyNumberDataMatrix FromTable(CsvTable table)
    {
        if (table.Header is null)
            throw new FormatException(
                $"{table.SourceName}: fuzzy data need a header row whose column names carry the suffixes " +
                "_m, _a, _b (fuzzy numbers), _ml, _mr, _a, _b (fuzzy intervals) or _m, _sigma (Gaussian numbers).");
        if (table.Rows.Length == 0)
            throw new FormatException($"{table.SourceName}: the file contains no data rows.");

        // attribute name → (suffix → column), in the order of first appearance
        var attributeOrder = new List<string>();
        var columnsOfAttribute = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        int nameColumn = -1;
        for (int column = 0; column < table.ColumnCount; column++)
        {
            string header = table.Header[column];
            int separator = header.LastIndexOf('_');
            string suffix = separator > 0 ? header[(separator + 1)..].ToLowerInvariant() : "";
            if (separator <= 0 || !KnownSuffixes.Contains(suffix))
            {
                if (nameColumn < 0 && !table.IsNumericColumn(column)) nameColumn = column;
                continue;
            }

            string attribute = header[..separator];
            if (!columnsOfAttribute.TryGetValue(attribute, out var columns))
            {
                columns = new Dictionary<string, int>(StringComparer.Ordinal);
                columnsOfAttribute[attribute] = columns;
                attributeOrder.Add(attribute);
            }
            if (!columns.TryAdd(suffix, column))
                throw new FormatException($"{table.SourceName}: the column {header} appears twice.");
        }

        if (attributeOrder.Count == 0)
            throw new FormatException(
                $"{table.SourceName}: no fuzzy attribute found; the column names need the suffixes " +
                "_m, _a, _b (fuzzy numbers), _ml, _mr, _a, _b (fuzzy intervals) or _m, _sigma (Gaussian numbers).");

        FuzzyNumberKind? kind = null;
        foreach (string attribute in attributeOrder)
        {
            var present = columnsOfAttribute[attribute].Keys.ToHashSet();
            FuzzyNumberKind? attributeKind = null;
            foreach (var candidate in Enum.GetValues<FuzzyNumberKind>())
                if (present.SetEquals(SuffixesOf(candidate))) attributeKind = candidate;
            if (attributeKind is null)
                throw new FormatException(
                    $"{table.SourceName}: attribute {attribute} has the columns " +
                    $"{string.Join(", ", present.Select(suffix => attribute + "_" + suffix))}; expected _m, _a, _b " +
                    "or _ml, _mr, _a, _b or _m, _sigma.");
            if (kind is not null && kind != attributeKind)
                throw new FormatException(
                    $"{table.SourceName}: all attributes must be fuzzy values of one kind; {attribute} is a {attributeKind}, the first attribute a {kind}.");
            kind = attributeKind;
        }

        var suffixes = SuffixesOf(kind!.Value);
        int objects = table.Rows.Length, attributes = attributeOrder.Count;
        var parameters = new double[objects, attributes, suffixes.Count];
        for (int attribute = 0; attribute < attributes; attribute++)
        {
            var columns = columnsOfAttribute[attributeOrder[attribute]];
            for (int parameter = 0; parameter < suffixes.Count; parameter++)
            {
                int column = columns[suffixes[parameter]];
                for (int row = 0; row < objects; row++)
                    parameters[row, attribute, parameter] = table.Number(row, column);
            }
        }

        string[]? objectNames = nameColumn < 0 ? null : [.. table.Rows.Select(row => row[nameColumn])];
        try
        {
            return new FuzzyNumberDataMatrix(kind.Value, parameters, attributeOrder, objectNames);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException($"{table.SourceName}: {exception.Message}", exception);
        }
    }
}
