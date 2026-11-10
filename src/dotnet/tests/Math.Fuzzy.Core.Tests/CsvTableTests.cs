using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Core.Tests;

public class CsvTableTests
{
    [Fact]
    public void Parse_CommaSeparatedWithHeader_ReadsCells()
    {
        var table = CsvTable.Parse("length,width\n5.1,3.5\n4.9,3.0\n");

        Assert.Equal(',', table.Separator);
        Assert.Equal(["length", "width"], table.Header!);
        Assert.Equal(2, table.Rows.Length);
        Assert.Equal(3.0, table.Number(1, 1));
    }

    [Fact]
    public void Parse_SemicolonSeparator_AcceptsDecimalComma()
    {
        var table = CsvTable.Parse("a;b\n1,5;2,25\n3;4\n");

        Assert.Equal(';', table.Separator);
        Assert.Equal(1.5, table.Number(0, 0));
        Assert.Equal(2.25, table.Number(0, 1));
    }

    [Fact]
    public void Parse_TabSeparator_IsDetected()
    {
        var table = CsvTable.Parse("1\t2\t3\n4\t5\t6\n");

        Assert.Equal('\t', table.Separator);
        Assert.Null(table.Header);
        Assert.Equal(6.0, table.Number(1, 2));
    }

    [Fact]
    public void Parse_QuotedFields_KeepSeparatorsAndQuotes()
    {
        var table = CsvTable.Parse("name,value\n\"Smith, John\",1\n\"say \"\"hi\"\"\",2\n");

        Assert.Equal("Smith, John", table.Rows[0][0]);
        Assert.Equal("say \"hi\"", table.Rows[1][0]);
    }

    [Fact]
    public void Parse_NoHeader_FirstRowIsData()
    {
        var table = CsvTable.Parse("setosa,5.1,3.5\nvirginica,6.3,3.3\n");

        Assert.Null(table.Header);
        Assert.Equal(2, table.Rows.Length);
    }

    [Fact]
    public void Parse_TrailingSeparator_DoesNotAddAColumn()
    {
        var table = CsvTable.Parse("1,2,\n3,4,\n");

        Assert.Equal(2, table.ColumnCount);
    }

    [Fact]
    public void Parse_RaggedRow_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => CsvTable.Parse("1,2,3\n4,5\n", "ragged.csv"));
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void Parse_EmptyText_IsAnError()
    {
        Assert.Throws<FormatException>(() => CsvTable.Parse("\n\n"));
    }

    [Theory]
    [InlineData("?")]
    [InlineData("NA")]
    [InlineData("NaN")]
    [InlineData("")]
    public void Number_MissingValue_IsReportedWithItsPosition(string marker)
    {
        var table = CsvTable.Parse($"a,b\n1,2\n3,{marker}\n5,6\n", "data.csv");

        var error = Assert.Throws<FormatException>(() => table.Number(1, 1));
        Assert.Contains("missing value at line 3", error.Message);
        Assert.Contains("b", error.Message);
    }
}

public class DataFileTests
{
    private static DataFile Load(string text) => DataFile.FromTable(CsvTable.Parse(text, "test.csv"));

    [Fact]
    public void Load_TextLabelColumn_IsSeparatedFromAttributes()
    {
        var file = Load("sl,sw,species\n5.1,3.5,setosa\n4.9,3.0,setosa\n6.3,3.3,virginica\n");

        Assert.Equal(2, file.Data.Cols);
        Assert.Equal(["sl", "sw"], file.FeatureNames);
        Assert.Equal(["setosa", "virginica"], file.ClassNames!);
        Assert.Equal([0, 0, 1], file.TrueLabels!);
    }

    [Fact]
    public void Load_NumericLabelColumn_IsRecognisedByItsHeader()
    {
        var file = Load("class,a,b\n1,0.5,0.7\n2,0.1,0.2\n1,0.6,0.8\n");

        Assert.Equal(2, file.Data.Cols);
        Assert.Equal([0, 1, 0], file.TrueLabels!);
    }

    [Fact]
    public void Load_ColumnOfObjectNames_IsNeitherAttributeNorLabel()
    {
        var file = Load("name,a,b\nfirst,1,2\nsecond,3,4\nthird,5,6\n");

        Assert.False(file.HasLabels);
        Assert.Equal(2, file.Data.Cols);
        Assert.Equal(["a", "b"], file.FeatureNames);
    }

    [Fact]
    public void Load_LabelInFirstColumnWithoutHeader_KeepsEveryRow()
    {
        var file = Load("setosa,5.1,3.5\nsetosa,4.9,3.0\nvirginica,6.3,3.3\n");

        Assert.Equal(3, file.Data.Rows);
        Assert.Equal(["x1", "x2"], file.FeatureNames);
        Assert.Equal(2, file.ClassNames!.Length);
    }

    [Fact]
    public void Load_HeaderOnly_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => Load("a,b,c\n"));
        Assert.Contains("no data rows", error.Message);
    }

    [Fact]
    public void Load_MissingAttributeValue_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => Load("a,b\n1,2\n?,4\n5,6\n"));
        Assert.Contains("missing value", error.Message);
    }

    [Fact]
    public void Load_MalformedNumberInAnAttribute_IsAnErrorNotALabel()
    {
        var error = Assert.Throws<FormatException>(() => Load("a,b\n1,2\n3,4\n5,x6\n7,8\n"));
        Assert.Contains("'x6'", error.Message);
    }

    [Fact]
    public void Load_SemicolonFileWithDecimalCommas_GivesTheSameNumbers()
    {
        var comma = Load("a,b\n1.5,2.5\n3.5,4.5\n");
        var semicolon = Load("a;b\n1,5;2,5\n3,5;4,5\n");

        Assert.Equal(comma.Data.ToFlatArray(), semicolon.Data.ToFlatArray());
    }
}

public class ToleranceMatrixFileTests
{
    private static FuzzyToleranceMatrix Load(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"mfuzzy-matrix-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, text);
        try { return FuzzyToleranceMatrix.FromCsv(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FromCsv_PlainMatrix_IsLoaded()
    {
        var matrix = Load("1,0.8,0.3\n0.8,1,0.5\n0.3,0.5,1\n");

        Assert.Equal(3, matrix.Size);
        Assert.Equal(0.8, matrix[0, 1]);
        Assert.Equal(0.5, matrix[2, 1]);
    }

    [Fact]
    public void FromCsv_HeaderAndRowNames_AreSkipped()
    {
        var matrix = Load("object,a,b,c\na,1,0.8,0.3\nb,0.8,1,0.5\nc,0.3,0.5,1\n");

        Assert.Equal(3, matrix.Size);
        Assert.Equal(0.3, matrix[0, 2]);
    }

    [Fact]
    public void FromCsv_NotSquare_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => Load("1,0.8,0.3\n0.8,1,0.5\n"));
        Assert.Contains("square", error.Message);
    }

    [Fact]
    public void FromCsv_NotSymmetric_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => Load("1,0.8,0.3\n0.7,1,0.5\n0.3,0.5,1\n"));
        Assert.Contains("not symmetric", error.Message);
    }

    [Fact]
    public void FromCsv_ValueOutsideUnitInterval_IsAnError()
    {
        var error = Assert.Throws<FormatException>(() => Load("1,3.2\n3.2,1\n"));
        Assert.Contains("outside [0, 1]", error.Message);
    }
}

public class LevelSnappingTests
{
    [Fact]
    public void SnapLevels_ValuesThatDifferByRoundingNoise_BecomeOneLevel()
    {
        var matrix = new FuzzyToleranceMatrix(4);
        matrix[0, 1] = 0.7;
        matrix[0, 2] = 0.7 + 2e-16;
        matrix[1, 2] = 0.7 - 3e-15;
        matrix[0, 3] = 0.2;
        matrix[1, 3] = 0.4;
        matrix[2, 3] = 0.4 + 1e-9;   // a real difference

        int removed = matrix.SnapLevels();

        Assert.Equal(2, removed);
        Assert.Equal(matrix[0, 1], matrix[0, 2]);
        Assert.Equal(matrix[0, 1], matrix[1, 2]);
        Assert.NotEqual(matrix[1, 3], matrix[2, 3]);
        Assert.Equal(4, matrix.GetDistinctValues().Count);
        Assert.Equal(matrix[1, 0], matrix[0, 1]);   // symmetry is preserved
    }

    [Fact]
    public void SnapLevels_CleanMatrix_IsLeftUntouched()
    {
        var matrix = new FuzzyToleranceMatrix(3);
        matrix[0, 1] = 0.9;
        matrix[0, 2] = 0.5;
        matrix[1, 2] = 0.6;
        var before = matrix.ToFlatArray();

        Assert.Equal(0, matrix.SnapLevels());
        Assert.Equal(before, matrix.ToFlatArray());
    }

    [Fact]
    public void Build_PointsAtEqualDistances_GiveOneLevelPerDistance()
    {
        // A regular grid: many pairs are at exactly the same distance, but the sums of
        // squares are accumulated from different coordinates.
        var data = new RawDataMatrix(9, 2);
        for (int index = 0; index < 9; index++)
        {
            data[index, 0] = 0.1 * (index % 3) + 0.3;
            data[index, 1] = 0.7 * (index / 3) + 0.1;
        }
        var builder = new ToleranceMatrixBuilder(
            MathFuzzy.Core.Distance.EuclideanDistance.Instance, MathFuzzy.Core.Normalization.MinMaxNormalization.Instance);

        var levels = builder.Build(data).GetDistinctValues();

        // On a 3 × 3 unit grid scaled to the unit square the distinct distances are
        // 0.5, √0.5, 1, √1.25, √2 (after division by √m they stay distinct): five levels,
        // of which those equal to 0 or 1 are not counted.
        for (int index = 1; index < levels.Count; index++)
            Assert.True(levels[index - 1] - levels[index] > 1e-9,
                $"levels {levels[index - 1]:R} and {levels[index]:R} differ by rounding noise only");
    }
}
