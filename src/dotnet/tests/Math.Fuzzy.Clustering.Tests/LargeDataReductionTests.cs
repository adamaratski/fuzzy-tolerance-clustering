using MathFuzzy.Clustering.Applications.LargeData;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// Reduction of large data sets (book chapter 6, Large Data Reduction; Viattchenin 2013, CDAM'2013).
/// The source publishes only the twelve interval-valued objects of the Iris data obtained by a
/// k-means run whose initialisation is not stated, and it does not cluster them; no published
/// vector exists for the k-means partition or for the clustering of the reduced data. The tests
/// therefore check the numbers the book gives (ñ = ⌊√n⌋) and the invariants of the procedure.
/// </summary>
public class LargeDataReductionTests
{
    private static string DatasetPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }

    private static RawDataMatrix Iris() => RawDataMatrix.FromCsv(DatasetPath("iris.csv"));

    private static IReadOnlyList<ClusterResult> DAfc3(FuzzyToleranceMatrix tolerance, CancellationToken ct) =>
        AlgorithmFactory.Run("dafc", tolerance, new AlgorithmOptions(ClusterCount: 3), ct);

    [Theory]
    [InlineData(150, 12)]     // Iris, as in the source
    [InlineData(10_000, 100)] // the book's remark
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(15, 3)]
    [InlineData(16, 4)]
    public void RepresentativeCount_IsTheFloorOfTheSquareRoot(int objectCount, int expected) =>
        Assert.Equal(expected, LargeDataReduction.RepresentativeCount(objectCount));

    [Fact]
    public void BuildRepresentatives_TakesMinimaAndMaximaPerClassAndAttribute()
    {
        var data = RawDataMatrix.FromArray(new double[,]
        {
            { 1.0, 5.0 }, { 2.0, 4.0 }, { 10.0, 0.0 }, { 1.5, 6.0 }, { 12.0, 1.0 },
        });
        var representatives = LargeDataReduction.BuildRepresentatives(data, [0, 0, 1, 0, 1]);
        Assert.Equal(2, representatives.Count);
        Assert.Equal(new[] { 0, 1, 3 }, representatives.ClassMembers[0]);
        Assert.Equal(new[] { 2, 4 }, representatives.ClassMembers[1]);
        Assert.Equal(1.0, representatives.Lower[0, 0]);
        Assert.Equal(2.0, representatives.Upper[0, 0]);
        Assert.Equal(4.0, representatives.Lower[0, 1]);
        Assert.Equal(6.0, representatives.Upper[0, 1]);
        Assert.Equal(10.0, representatives.Lower[1, 0]);
        Assert.Equal(12.0, representatives.Upper[1, 0]);
        Assert.Equal(0.0, representatives.Lower[1, 1]);
        Assert.Equal(1.0, representatives.Upper[1, 1]);

        Assert.Throws<ArgumentException>(() => LargeDataReduction.BuildRepresentatives(data, [0, 0, 2, 0, 2]));
    }

    [Fact]
    public void Iris_IsReducedToTwelveBoxesThatContainTheirClassesAndAttainTheirBounds()
    {
        var data = Iris();
        var representatives = LargeDataReduction.Reduce(data, new LargeDataOptions());
        Assert.Equal(12, representatives.Count);
        Assert.Equal(150, representatives.ClassMembers.Sum(members => members.Length));

        for (int classIndex = 0; classIndex < representatives.Count; classIndex++)
        {
            var members = representatives.ClassMembers[classIndex];
            for (int attribute = 0; attribute < data.Cols; attribute++)
            {
                Assert.All(members, objectIndex =>
                {
                    Assert.InRange(data[objectIndex, attribute], representatives.Lower[classIndex, attribute],
                                   representatives.Upper[classIndex, attribute]);
                    Assert.Equal(classIndex, representatives.ClassOfObject[objectIndex]);
                });
                Assert.Contains(members, objectIndex => data[objectIndex, attribute] == representatives.Lower[classIndex, attribute]);
                Assert.Contains(members, objectIndex => data[objectIndex, attribute] == representatives.Upper[classIndex, attribute]);
            }
        }
    }

    [Fact]
    public void Iris_EveryObjectIsAssignedWithTheMembershipOfItsClass()
    {
        var data = Iris();
        var result = LargeDataReduction.Run(data, new LargeDataOptions(), DAfc3, TestContext.Current.CancellationToken);
        var best = result.Best;
        var representativeBest = result.RepresentativeResults[0];

        Assert.Equal(3, best.ClusterCount);
        Assert.Equal(150, best.Membership.GetLength(0));
        Assert.Equal(Enumerable.Range(0, 150), best.MemberSets.SelectMany(set => set).Distinct().Order());

        for (int objectIndex = 0; objectIndex < 150; objectIndex++)
        {
            int classIndex = result.Representatives.ClassOfObject[objectIndex];
            for (int cluster = 0; cluster < 3; cluster++)
            {
                Assert.Equal(representativeBest.Membership[classIndex, cluster], best.Membership[objectIndex, cluster]);
                Assert.Equal(representativeBest.MemberSets[cluster].Contains(classIndex), best.MemberSets[cluster].Contains(objectIndex));
            }
        }

        // The typical point of a cluster is an object of its typical class.
        for (int cluster = 0; cluster < 3; cluster++)
            Assert.Equal(representativeBest.Centers[cluster], result.Representatives.ClassOfObject[best.Centers[cluster]]);
        Assert.Equal(representativeBest.Alpha, best.Alpha);
        Assert.Equal(representativeBest.Score, best.Score);
    }

    [Fact]
    public void WithoutReduction_TheResultEqualsTheDirectRun()
    {
        var data = Iris();
        var reduced = LargeDataReduction.Run(data, new LargeDataOptions(Reduce: false), DAfc3, TestContext.Current.CancellationToken).Best;
        var direct = AlgorithmFactory.Run("dafc", data, new AlgorithmOptions(ClusterCount: 3), TestContext.Current.CancellationToken)[0];

        Assert.True(direct.HasSameClusters(reduced));
        Assert.Equal(direct.Centers, reduced.Centers);
        Assert.Equal(direct.Alpha, reduced.Alpha, 1e-12);
        Assert.Equal(direct.Score, reduced.Score, 1e-9);
        for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
            for (int cluster = 0; cluster < 3; cluster++)
                Assert.Equal(direct.Membership[objectIndex, cluster], reduced.Membership[objectIndex, cluster], 1e-12);
    }

    [Fact]
    public void DegenerateIntervals_GiveTheToleranceOfThePointData()
    {
        var data = Iris();
        var interval = LargeDataReduction.BurilloBustinceTolerance(data, data);
        var point = new ToleranceMatrixBuilder(MathFuzzy.Core.Distance.EuclideanDistance.Instance,
                                               MathFuzzy.Core.Normalization.MinMaxNormalization.Instance).Build(data);
        for (int row = 0; row < data.Rows; row++)
            for (int column = 0; column < data.Rows; column++)
                Assert.Equal(point[row, column], interval[row, column], 1e-12);
    }

    [Fact]
    public void Reduction_IsDeterministic()
    {
        var data = Iris();
        var first = LargeDataReduction.Run(data, new LargeDataOptions(), DAfc3, TestContext.Current.CancellationToken);
        var second = LargeDataReduction.Run(data, new LargeDataOptions(), DAfc3, TestContext.Current.CancellationToken);
        Assert.Equal(first.Representatives.ClassOfObject, second.Representatives.ClassOfObject);
        Assert.Equal(first.Best.Centers, second.Best.Centers);
        Assert.Equal(first.Best.Alpha, second.Best.Alpha);
        Assert.Equal(first.Best.Membership, second.Best.Membership);
    }

    [Fact]
    public void SuppliedPartition_ReplacesKMeans()
    {
        var data = Iris();
        int[] bySpecies = [.. Enumerable.Range(0, 150).Select(objectIndex => objectIndex / 50)];
        var representatives = LargeDataReduction.Reduce(data, new LargeDataOptions(Partition: bySpecies));
        Assert.Equal(3, representatives.Count);
        Assert.Equal(Enumerable.Range(50, 50), representatives.ClassMembers[1]);
    }

    [Fact]
    public void KMeans_SeparatesWellSeparatedGroupsAndNumbersClassesByFirstObject()
    {
        var data = RawDataMatrix.FromArray(new double[,]
        {
            { 10.0, 10.0 }, { 0.0, 0.1 }, { 10.1, 10.0 }, { 0.1, 0.0 }, { 20.0, 0.0 }, { 20.1, 0.1 },
        });
        var labels = KMeansPartition.Partition(data, 3);
        Assert.Equal(new[] { 0, 1, 0, 1, 2, 2 }, labels);
    }

    [Fact]
    public void KMeans_ReturnsFewerClassesWhenObjectsCoincide()
    {
        var data = RawDataMatrix.FromArray(new double[,] { { 1.0 }, { 1.0 }, { 2.0 }, { 2.0 } });
        var labels = KMeansPartition.Partition(data, 3);
        Assert.Equal(new[] { 0, 0, 1, 1 }, labels);
    }
}
