using MathFuzzy.Clustering.Applications.Preference;
using MathFuzzy.Core.Preference;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// Analysis of fuzzy preference relations (book chapter 6, Fuzzy Preference Relation Analysis;
/// Viattchenin 2013, sec. 4.4.3): the example of six weak fuzzy preference relations on four
/// alternatives (book tables tbl-pref-relations, tbl-pref-distances, tbl-pref-nondominated,
/// tbl-pref-fuzziness).
/// </summary>
public class PreferenceRelationTests
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

    private static FuzzyPreferenceRelationSet SixRelations() =>
        FuzzyPreferenceRelationSet.FromCsv(DatasetPath("preference_six_relations.csv"));

    [Fact]
    public void Dataset_HoldsSixReflexiveRelationsOnFourAlternatives()
    {
        var relations = SixRelations();
        Assert.Equal(6, relations.ExpertCount);
        Assert.Equal(4, relations.AlternativeCount);
        Assert.Equal(["R1", "R2", "R3", "R4", "R5", "R6"], relations.ExpertNames);
        Assert.Equal(["a1", "a2", "a3", "a4"], relations.AlternativeNames);
        Assert.All(Enumerable.Range(0, 6), expert => Assert.True(relations.IsReflexive(expert)));
        // Two cells of the book table (rows are the first argument): R1(a2, a4) = 0.6, R5(a1, a2) = 0.5.
        Assert.Equal(0.6, relations[0, 1, 3]);
        Assert.Equal(0.5, relations[4, 0, 1]);
    }

    [Fact]
    public void KuzminDistances_ReproduceTable47()
    {
        var distances = FuzzyPreferenceAnalysis.KuzminDistances(SixRelations());
        double[,] printed =
        {
            { 0.0, 1.2, 0.6, 2.5, 2.9, 2.3 },
            { 1.2, 0.0, 0.6, 2.5, 2.9, 2.3 },
            { 0.6, 0.6, 0.0, 2.3, 2.7, 2.1 },
            { 2.5, 2.5, 2.3, 0.0, 1.2, 0.6 },
            { 2.9, 2.9, 2.7, 1.2, 0.0, 1.8 },
            { 2.3, 2.3, 2.1, 0.6, 1.8, 0.0 },
        };
        for (int row = 0; row < 6; row++)
            for (int column = 0; column < 6; column++)
                Assert.Equal(printed[row, column], distances[row, column], 1e-9);
    }

    [Fact]
    public void Tolerance_IsTheComplementOfTheDistancesDividedBy29()
    {
        var tolerance = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken).Tolerance;
        Assert.Equal(0.7931, tolerance[0, 2], 4);   // 1 − 0.6/2.9
        Assert.Equal(0.5862, tolerance[0, 1], 4);   // 1 − 1.2/2.9
        Assert.Equal(0.0, tolerance[0, 4], 12);     // the largest distance
        for (int row = 0; row < 6; row++)
            Assert.Equal(1.0, tolerance[row, row]);
    }

    [Fact]
    public void DPafc_FindsTheTwoClustersOfFig46WithTypicalPointsR1AndR4()
    {
        var result = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken);
        var allotment = result.Allotment;

        Assert.Equal(2, allotment.ClusterCount);
        Assert.Equal([0, 3], result.TypicalRelations);
        Assert.Equal([0, 1, 2], allotment.MemberSets[0].Order());
        Assert.Equal([3, 4, 5], allotment.MemberSets[1].Order());

        // Fig. 4.6: memberships 1, 0.5862, 0.7931 in each cluster.
        Assert.Equal(1.0, allotment.Membership[0, 0], 4);
        Assert.Equal(0.5862, allotment.Membership[1, 0], 4);
        Assert.Equal(0.7931, allotment.Membership[2, 0], 4);
        Assert.Equal(1.0, allotment.Membership[3, 1], 4);
        Assert.Equal(0.5862, allotment.Membership[4, 1], 4);
        Assert.Equal(0.7931, allotment.Membership[5, 1], 4);

        // Within the second cluster the memberships are ordered R4 > R6 > R5 (book).
        Assert.True(allotment.Membership[3, 1] > allotment.Membership[5, 1]);
        Assert.True(allotment.Membership[5, 1] > allotment.Membership[4, 1]);
    }

    [Fact]
    public void DPafc_StopsAtTheRecomputedLevel()
    {
        // The book prints no threshold for this example. The recomputation made for the book scans
        // the levels 0.069, 0.1379, 0.2069 (each skipped: one neighbourhood covers all relations)
        // and stops at 0.2759 = 1 − 2.1/2.9; the allotment stays valid up to 0.5862.
        var allotment = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken).Allotment;
        Assert.Equal(0.2759, allotment.Alpha, 4);
    }

    [Fact]
    public void DPafc_ReportsTheTieBetweenR1AndR2()
    {
        // Book: R1 and R2 have equal distances to all other relations, so the centres R1 and R2 give
        // the same value of F1; the monograph takes R1, the first in the order of the relations.
        var result = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken);
        Assert.NotNull(result.AllotmentTie);
        Assert.Contains("the clusters are the same", result.AllotmentTie);
        Assert.Equal(0, result.TypicalRelations[0]);
    }

    [Fact]
    public void FuzzinessIndices_ReproduceTable49()
    {
        var indices = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken).FuzzinessIndices;
        // Printed l(R1) = 0.4250; the book's footnote gives 0.4375 recomputed from the printed R1.
        Assert.Equal(0.4375, indices[0], 4);
        Assert.Equal(0.4625, indices[1], 4);
        Assert.Equal(0.4875, indices[2], 4);
        Assert.Equal(0.5000, indices[3], 4);
        Assert.Equal(0.4750, indices[4], 4);
        Assert.Equal(0.5750, indices[5], 4);
        // l(R5) < l(R4) < l(R6) within the second cluster (book).
        Assert.True(indices[4] < indices[3] && indices[3] < indices[5]);
    }

    [Fact]
    public void Selection_TakesR1AndDesignatesA3WithDegree08()
    {
        var result = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken);
        Assert.Equal([0], result.SelectedRelations);
        double[] printed = [0.5, 0.6, 0.8, 0.5];
        for (int alternative = 0; alternative < 4; alternative++)
            Assert.Equal(printed[alternative], result.NonDominated[alternative], 9);
        Assert.Equal([2], result.BestAlternatives);
        Assert.Equal(0.8, result.AcceptanceDegree, 9);
        Assert.Equal(2, result.Ranking[0]);
        Assert.Equal([2, 1, 0, 3], result.Ranking);   // 0.8, 0.6, then 0.5 and 0.5 in the order of the alternatives
    }

    [Fact]
    public void NonDominatedSets_ReproduceTable48()
    {
        var relations = SixRelations();
        double[,] printed =   // [alternative, relation]
        {
            { 0.5, 0.5, 0.6, 0.8, 1.0, 0.7 },
            { 0.6, 0.6, 0.7, 0.8, 0.6, 0.9 },
            { 0.8, 0.8, 0.7, 1.0, 0.8, 1.0 },
            { 0.5, 0.5, 0.4, 0.8, 0.6, 0.9 },
        };
        for (int expert = 0; expert < 6; expert++)
        {
            var nonDominated = FuzzyPreferenceAnalysis.NonDominatedSet(relations.Relation(expert));
            for (int alternative = 0; alternative < 4; alternative++)
                Assert.Equal(printed[alternative, expert], nonDominated[alternative], 9);
        }
    }

    [Fact]
    public void BestAlternativesOfTheIndividualRelations_AreAsTheBookStates()
    {
        // a3 is best for R1, R2, R4, R6; a1 for R5; a2 and a3 tie for R3.
        var relations = SixRelations();
        int[][] expected = [[2], [2], [1, 2], [2], [0], [2]];
        for (int expert = 0; expert < 6; expert++)
        {
            var analysis = FuzzyPreferenceAnalysis.Analyze(relations.Select([expert]), ct: TestContext.Current.CancellationToken);
            Assert.Equal(expected[expert], analysis.BestAlternatives);
        }
    }

    [Fact]
    public void AggregationOfAllSixRelations_ReproducesTheComparisonSet()
    {
        // Orlovsky's method on all six relations with equal weights 1/6:
        // {(a1, 0.73), (a2, 0.83), (a3, 0.97), (a4, 0.67)}, printed to two decimals.
        var aggregated = FuzzyPreferenceAnalysis.AggregatedNonDominatedSet(SixRelations().Relations);
        double[] printed = [0.73, 0.83, 0.97, 0.67];
        for (int alternative = 0; alternative < 4; alternative++)
            Assert.Equal(printed[alternative], aggregated[alternative], 0.005);
        Assert.Equal([2], Enumerable.Range(0, 4).Where(alternative => aggregated[alternative] == aggregated.Max()));
    }

    [Fact]
    public void AggregationOfOneRelation_IsItsNonDominatedSet()
    {
        var relation = SixRelations().Relation(3);
        Assert.Equal(FuzzyPreferenceAnalysis.NonDominatedSet(relation),
                     FuzzyPreferenceAnalysis.AggregatedNonDominatedSet([relation]));
    }

    [Fact]
    public void Analysis_IsDeterministic()
    {
        var first = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken);
        var second = FuzzyPreferenceAnalysis.Analyze(SixRelations(), ct: TestContext.Current.CancellationToken);
        Assert.Equal(first.TypicalRelations, second.TypicalRelations);
        Assert.Equal(first.NonDominated, second.NonDominated);
        Assert.Equal(first.Allotment.Alpha, second.Allotment.Alpha);
    }

    [Fact]
    public void Csv_RoundTripsAndAcceptsRowsInAnyOrder()
    {
        var relations = SixRelations();
        var again = FuzzyPreferenceRelationSet.Parse(relations.ToCsv());
        Assert.Equal(relations.ExpertNames, again.ExpertNames);
        Assert.Equal(relations.AlternativeNames, again.AlternativeNames);
        for (int expert = 0; expert < 6; expert++)
            Assert.Equal(relations.Relation(expert), again.Relation(expert));

        var shuffled = FuzzyPreferenceRelationSet.Parse(
            "expert;alternative;x;y\nB;y;0,4;1\nA;x;1;0,3\nB;x;1;0,2\nA;y;0,7;1\n");
        Assert.Equal(["B", "A"], shuffled.ExpertNames);
        Assert.Equal(0.4, shuffled[0, 1, 0]);
        Assert.Equal(0.3, shuffled[1, 0, 1]);
    }

    [Fact]
    public void Csv_RejectsMalformedFiles()
    {
        Assert.Throws<FormatException>(() => FuzzyPreferenceRelationSet.Parse("expert,alternative,x,y\nA,x,1,0.3\n"));          // row for y missing
        Assert.Throws<FormatException>(() => FuzzyPreferenceRelationSet.Parse("expert,alternative,x,y\nA,x,1,1.3\nA,y,0,1\n")); // outside [0, 1]
        Assert.Throws<FormatException>(() => FuzzyPreferenceRelationSet.Parse("expert,alternative,x,y\nA,z,1,0\nA,y,0,1\n"));   // unknown alternative
        Assert.Throws<FormatException>(() => FuzzyPreferenceRelationSet.Parse("expert,alternative,x,y\nA,x,1,0\nA,x,0,1\n"));   // repeated row
    }

    [Fact]
    public void MatrixFiles_OneFilePerExpert()
    {
        var directory = Directory.CreateTempSubdirectory("preference");
        try
        {
            var relations = SixRelations();
            var paths = new List<string>();
            for (int expert = 0; expert < relations.ExpertCount; expert++)
            {
                var lines = new List<string> { "alternative," + string.Join(',', relations.AlternativeNames) };
                for (int row = 0; row < 4; row++)
                    lines.Add(relations.AlternativeNames[row] + "," + string.Join(',',
                        Enumerable.Range(0, 4).Select(column => relations[expert, row, column].ToString(System.Globalization.CultureInfo.InvariantCulture))));
                string path = Path.Combine(directory.FullName, relations.ExpertNames[expert] + ".csv");
                File.WriteAllLines(path, lines);
                paths.Add(path);
            }

            var loaded = FuzzyPreferenceRelationSet.FromMatrixFiles(paths);
            Assert.Equal(relations.ExpertNames, loaded.ExpertNames);
            Assert.Equal(relations.AlternativeNames, loaded.AlternativeNames);
            Assert.Equal([0, 3], FuzzyPreferenceAnalysis.Analyze(loaded, ct: TestContext.Current.CancellationToken).TypicalRelations);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
