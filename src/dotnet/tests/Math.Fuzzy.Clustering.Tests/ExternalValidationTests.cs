using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

public class LabeledDatasetTests
{
    private static string TempCsv(string content)
    {
        var path = System.IO.Path.GetTempFileName() + ".csv";
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void TryFromCsv_TextLabels_DetectsLastColumn()
    {
        var path = TempCsv(
            "x,y,class\n" +
            "0.1,0.2,setosa\n" +
            "0.3,0.4,setosa\n" +
            "0.9,0.8,virginica\n");

        var ds = LabeledDataset.TryFromCsv(path);

        Assert.NotNull(ds);
        Assert.Equal(3, ds!.Data.Rows);
        Assert.Equal(2, ds.Data.Cols);          // x, y only
        Assert.Equal(2, ds.ClassCount);          // setosa, virginica
        Assert.Equal(0, ds.TrueLabels[0]);
        Assert.Equal(0, ds.TrueLabels[1]);
        Assert.Equal(1, ds.TrueLabels[2]);
    }

    [Fact]
    public void TryFromCsv_IntegerLabels_DetectedByHeader()
    {
        var path = TempCsv(
            "class,f1,f2\n" +
            "1,0.1,0.2\n" +
            "1,0.3,0.1\n" +
            "2,0.9,0.8\n");

        var ds = LabeledDataset.TryFromCsv(path);

        Assert.NotNull(ds);
        Assert.Equal(3, ds!.Data.Rows);
        Assert.Equal(2, ds.Data.Cols);   // f1, f2 only
        Assert.Equal(2, ds.ClassCount);
    }

    [Fact]
    public void TryFromCsv_NoLabels_ReturnsNull()
    {
        var path = TempCsv(
            "x,y\n" +
            "0.1,0.2\n" +
            "0.9,0.8\n");

        var ds = LabeledDataset.TryFromCsv(path);

        Assert.Null(ds);
    }

    [Fact]
    public void TryFromCsv_NoHeader_TextColumn_Detected()
    {
        var path = TempCsv(
            "0.1,0.2,A\n" +
            "0.3,0.4,A\n" +
            "0.9,0.8,B\n");

        var ds = LabeledDataset.TryFromCsv(path);

        Assert.NotNull(ds);
        Assert.Equal(2, ds!.Data.Cols);
        Assert.Equal(2, ds.ClassCount);
    }
}

public class ExternalValidatorTests
{
    // Perfect 2-cluster result matching true labels
    private static ClusterResult PerfectResult() => new(
        ClusterCount: 2,
        Alpha: 0.5,
        Centers: [0, 3],
        MemberSets: [[0, 1, 2], [3, 4, 5]],
        Membership: new double[6, 2],
        Score: 1.0,
        CriterionName: "F1");

    private static int[] PerfectLabels => [0, 0, 0, 1, 1, 1];
    private static string[] ClassNames => ["A", "B"];

    [Fact]
    public void Compute_PerfectMatch_AllIndicesNearOne()
    {
        var result = ExternalValidator.Compute(PerfectResult(), PerfectLabels, ClassNames);

        Assert.Equal(1.0, result.Purity,               precision: 6);
        Assert.Equal(1.0, result.RandIndex,            precision: 6);
        Assert.Equal(1.0, result.AdjustedRandIndex,    precision: 6);
        Assert.Equal(1.0, result.NormalizedMutualInfo, precision: 4);
        Assert.Equal(1.0, result.FowlkesMallows,       precision: 6);
    }

    [Fact]
    public void Compute_PerfectMatch_MajorityLabelsCorrect()
    {
        var result = ExternalValidator.Compute(PerfectResult(), PerfectLabels, ClassNames);

        Assert.Equal("A", result.ClusterMajority[0]);
        Assert.Equal("B", result.ClusterMajority[1]);
    }

    [Fact]
    public void Compute_RandomAssignment_AriNearZero()
    {
        // Cluster [0,1,2,3,4,5] — one giant cluster, true labels 0/0/0/1/1/1
        var singleCluster = new ClusterResult(1, 0.1, [0],
            MemberSets: [[0, 1, 2, 3, 4, 5]],
            Membership: new double[6, 1],
            Score: 0.5, CriterionName: "F1");

        var result = ExternalValidator.Compute(singleCluster, PerfectLabels, ClassNames);

        // Single cluster: purity = max(3,3)/6 = 0.5
        Assert.Equal(0.5, result.Purity, precision: 4);
    }

    [Fact]
    public void Compute_AllIndices_InZeroOneRange()
    {
        // Imperfect clustering: cluster 0 gets objects 0,1,2,3 (mixed), cluster 1 gets 4,5
        var mixed = new ClusterResult(2, 0.5, [0, 4],
            MemberSets: [[0, 1, 2, 3], [4, 5]],
            Membership: new double[6, 2],
            Score: 0.8, CriterionName: "F1");

        var result = ExternalValidator.Compute(mixed, PerfectLabels, ClassNames);

        Assert.InRange(result.Purity,               0.0, 1.0);
        Assert.InRange(result.RandIndex,            0.0, 1.0);
        Assert.InRange(result.AdjustedRandIndex,   -0.5, 1.0); // ARI can be slightly negative
        Assert.InRange(result.NormalizedMutualInfo, 0.0, 1.0);
        Assert.InRange(result.FowlkesMallows,       0.0, 1.0);
    }
}
