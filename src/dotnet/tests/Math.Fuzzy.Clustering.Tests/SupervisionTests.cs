using MathFuzzy.Clustering.Algorithms.Intuitionistic;
using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Algorithms.Supervision;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Intuitionistic;
using MathFuzzy.Core.IO;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Core.Normalization;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// The algorithms with partial supervision against the published examples: D-AFC-PS(c) on the
/// five-object matrix of the book and on the Yang–Wu and Sneath–Sokal data (Viattchenin 2013,
/// sec. 2.2.6), D-AIFC-PS(c) on the five cars and on Hung's relation (Kacprzyk, Owsiński, Shyrai,
/// Szmidt and Viattchenin 2017, the table of results of the book), and the two-step technique of
/// partially supervised classification on Anderson's Iris data (Viattchenin 2013, sec. 4.1.2).
/// </summary>
public class SupervisionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static int[] OneBased(IEnumerable<int> indices) => [.. indices.Select(index => index + 1)];

    private static string DatasetPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }

    /// <summary>Normalisation (2.11), squared normalised Euclidean distance (2.15), T = 1 − d (1.26), ε.</summary>
    private static ToleranceMatrixBuilder MonographBuilder(IDistance distance, double accuracy) =>
        new(distance, MaxNormalization.Instance, accuracy);

    // ───────────────────────── D-AFC-PS(c) ─────────────────────────

    /// <summary>Matrix T of the worked example of chapter 2 (five objects).</summary>
    private static FuzzyToleranceMatrix BookMatrix()
    {
        var matrix = new FuzzyToleranceMatrix(5);
        matrix[0, 1] = 0.8; matrix[0, 2] = 0.0; matrix[0, 3] = 0.1; matrix[0, 4] = 0.2;
        matrix[1, 2] = 0.4; matrix[1, 3] = 0.0; matrix[1, 4] = 0.9;
        matrix[2, 3] = 0.0; matrix[2, 4] = 0.0;
        matrix[3, 4] = 0.5;
        return matrix;
    }

    private static IReadOnlyList<ClusterResult> SupervisedRun(FuzzyToleranceMatrix matrix, int clusterCount,
                                                              params LabelledObject[] labels) =>
        new DAfcPsAlgorithm(new DAfcOptions(clusterCount), labels).Run(matrix, Token);

    [Fact]
    public void DAfcPs_BookMatrix_LabelSelectsTheSecondAllotmentOfB3()
    {
        // B(3) = {P(0.8), P(0.5)}; x2 with membership 1 is the typical point of P(0.8) only
        var found = SupervisedRun(BookMatrix(), 3, new LabelledObject(1, 1.0));

        var best = Assert.Single(found);
        Assert.Equal(0.8, best.Alpha, precision: 10);
        Assert.Equal([2, 3, 4], OneBased(best.Centers));
        Assert.Equal(0.5, best.Score, precision: 10);   // F1(P(0.8), 0.8)
    }

    [Fact]
    public void DAfcPs_BookMatrix_LabelsKeepOnlyTheBestAllotment()
    {
        // x1 lies in the cluster of P(0.5) with degree 1, x5 with T(x4, x5) = 0.5; in P(0.8) the degree
        // of x1 is T(x2, x1) = 0.8 < 1, so P(0.8) leaves B(3)
        var best = Assert.Single(SupervisedRun(BookMatrix(), 3, new LabelledObject(0, 1.0), new LabelledObject(4, 0.5)));

        Assert.Equal(0.5, best.Alpha, precision: 10);
        Assert.Equal([1, 3, 4], OneBased(best.Centers));
        Assert.Equal(1.15, best.Score, precision: 10);
    }

    [Fact]
    public void DAfcPs_BookMatrix_ObjectsOfOneClassMustShareACluster()
    {
        // x1 and x5 lie together only in the cluster {x1, x2, x5} of P(0.8)
        var together = SupervisedRun(BookMatrix(), 3, new LabelledObject(0, 0.1, Class: 7), new LabelledObject(4, 0.1, Class: 7));
        Assert.Equal(0.8, Assert.Single(together).Alpha, precision: 10);

        // x1 and x2 lie together in every allotment of B(3): as different classes they are inconsistent
        // at w = 0, so w is increased until an allotment separates them
        var apart = SupervisedRun(BookMatrix(), 3, new LabelledObject(0, 0.1), new LabelledObject(1, 0.1));
        Assert.NotEmpty(apart);
        Assert.All(apart, result => Assert.True(result.IntersectionCount > 0));
        Assert.All(apart, result => Assert.NotNull(LabelConstraint.AssignClasses(result, new LabelledObject[] { new(0, 0.1), new(1, 0.1) })));
    }

    [Fact]
    public void DAfcPs_YangWu_ReproducesTheMonographThreshold()
    {
        // c = 2, ε = 1e-5, x1 and x9 labelled with y = 1 (Viattchenin 2013, sec. 2.2.6, p. 83):
        // an allotment among two partially separated clusters at α = 0.92663
        var data = DataFile.Load(DatasetPath("yang_wu.csv")).Data;
        var labels = new[] { new LabelledObject(0, 1.0), new LabelledObject(8, 1.0) };

        var found = new DAfcPsAlgorithm(new DAfcOptions(2), labels)
            .Run(data, MonographBuilder(SquaredEuclideanDistance.Instance, 1e-5), Token);

        var best = found[0];
        Assert.Equal(0.92663, best.Alpha, precision: 5);
        Assert.True(best.IntersectionCount > 0);                // partially, not fully, separated
        Assert.Equal([1, 9], OneBased(best.Centers));           // y = 1: the labelled objects are typical points
        Assert.Equal([0, 1], LabelConstraint.AssignClasses(best, labels)!);
    }

    [Fact]
    public void DAfcPs_SneathSokal_EqualsTheResultOfDAfc()
    {
        // x8 (y = 0.85351) and x13 (y = 0.85916): "the result is equal to the result obtained by the
        // D-AFC(c)-algorithm" (Viattchenin 2013, p. 89): classes of 8 and 9 objects at α = 0.81944,
        // the fifth object in both
        var tolerance = MonographBuilder(SquaredEuclideanDistance.Instance, 1e-5).Build(BookExampleTests.SneathSokal());
        var supervised = SupervisedRun(tolerance, 2, new LabelledObject(7, 0.85351), new LabelledObject(12, 0.85916));
        var unsupervised = new DAfcAlgorithm(new DAfcOptions(2), Core.AlphaSelection.AllDistinctAlphaSelector.Instance,
                                             Core.Criteria.F1Criterion.Instance).Run(tolerance, Token);

        var best = supervised[0];
        Assert.Equal(0.81944, best.Alpha, precision: 5);
        Assert.Equal([8, 9], best.MemberSets.Select(set => set.Length).Order());
        Assert.All(best.MemberSets, set => Assert.Contains(4, set));
        Assert.Equal(unsupervised[0].Centers, best.Centers);
        Assert.Equal(unsupervised[0].Alpha, best.Alpha);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void DAfcPs_MembershipOutsideTheUnitInterval_IsRejected(double membership)
    {
        Assert.Throws<ArgumentException>(() => new DAfcPsAlgorithm(new DAfcOptions(3), [new LabelledObject(0, membership)]));
    }

    [Fact]
    public void DAfcPs_InvalidLabelSets_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => SupervisedRun(BookMatrix(), 2, new LabelledObject(5, 1.0)));
        Assert.Throws<ArgumentException>(() => SupervisedRun(BookMatrix(), 2, new LabelledObject(1, 1.0), new LabelledObject(1, 0.5)));
        Assert.Throws<ArgumentException>(() => SupervisedRun(BookMatrix(), 2,
            new LabelledObject(0, 1.0), new LabelledObject(2, 1.0), new LabelledObject(3, 1.0)));   // three classes, c = 2
        Assert.Throws<ArgumentException>(() => SupervisedRun(BookMatrix(), 2));
    }

    [Fact]
    public void DAfcPs_RandomTolerances_EveryResultIsConsistent_AndUnsupervisedOptimumKeptWhenConsistent()
    {
        var random = new Random(20261110);
        for (int trial = 0; trial < 40; trial++)
        {
            var matrix = new FuzzyToleranceMatrix(9);
            for (int row = 0; row < 9; row++)
                for (int column = row + 1; column < 9; column++)
                    matrix[row, column] = System.Math.Round(random.NextDouble(), 1);
            var labels = new[] { new LabelledObject(random.Next(0, 4), 0.5), new LabelledObject(random.Next(4, 9), 0.5) };

            var supervised = SupervisedRun(matrix, 3, labels);
            var unsupervised = new DAfcAlgorithm(new DAfcOptions(3), Core.AlphaSelection.AllDistinctAlphaSelector.Instance,
                                                 Core.Criteria.F1Criterion.Instance).Run(matrix, Token);

            Assert.All(supervised, result => Assert.NotNull(LabelConstraint.AssignClasses(result, labels)));
            if (unsupervised.Count > 0 && LabelConstraint.AssignClasses(unsupervised[0], labels) is not null)
            {
                Assert.Equal(unsupervised[0].Alpha, supervised[0].Alpha);
                Assert.Equal(unsupervised[0].Centers, supervised[0].Centers);
            }
        }
    }

    // ───────────────────────── D-AIFC-PS(c) ─────────────────────────

    /// <summary>The published table of the Hamming-based five-car tolerance, two decimals.</summary>
    private static IntuitionisticToleranceMatrix RoundedFiveCars() => IntuitionisticTests.Relation(
    [
        [0.80, 0.10],
        [0.72, 0.12, 0.82, 0.08],
        [0.75, 0.13, 0.72, 0.10, 0.70, 0.05],
        [0.65, 0.22, 0.68, 0.18, 0.63, 0.23, 0.63, 0.25],
    ]);

    private static IntuitionisticLabelledObject Label(int objectNumber, double membership, double nonMembership) =>
        new(objectNumber - 1, membership, nonMembership);

    private static IReadOnlyList<ClusterResult> Supervised(
        IntuitionisticToleranceMatrix relation, int clusterCount, ThresholdScanOrder order,
        SupervisedIntersectionRule rule, params IntuitionisticLabelledObject[] labels) =>
        new DAifcPsAlgorithm(clusterCount, labels, new IntuitionisticOptions(ScanOrder: order), rule).Run(relation, Token);

    public static TheoryData<SupervisedIntersectionRule> BothRules =>
        [SupervisedIntersectionRule.AtCurrentPair, SupervisedIntersectionRule.AfterFullScan];

    [Theory]
    [MemberData(nameof(BothRules))]
    public void DAifcPs_FiveCars_X1X5_BetaOuterReproducesThePublishedPair(SupervisedIntersectionRule rule)
    {
        // reported: two fully separated clusters at (0.70, 0.25); {x1, …, x4} (typical point x1), {x5}
        var labels = new[] { Label(1, 1.0, 0.0), Label(5, 1.0, 0.0) };
        var outer = Supervised(RoundedFiveCars(), 2, ThresholdScanOrder.BetaOuter, rule, labels);
        var inner = Supervised(RoundedFiveCars(), 2, ThresholdScanOrder.BetaInner, rule, labels);

        var best = Assert.Single(outer);
        Assert.Equal((0.70, 0.25), (best.Alpha, best.Beta!.Value));
        Assert.Equal(0, best.IntersectionBound);
        Assert.Equal([1, 5], OneBased(best.Centers));
        Assert.Equal([[0, 1, 2, 3], [4]], best.MemberSets);
        Assert.Equal(0.83, best.Score, precision: 12);

        // the printed order stops earlier, with the same classes (F = 0.73: ifs_spec TV-10)
        var printed = Assert.Single(inner);
        Assert.Equal((0.63, 0.13), (printed.Alpha, printed.Beta!.Value));
        Assert.Equal([1, 5], OneBased(printed.Centers));
        Assert.Equal([[0, 1, 2, 3], [4]], printed.MemberSets);
        Assert.Equal(0.73, printed.Score, precision: 12);
    }

    [Theory]
    [MemberData(nameof(BothRules))]
    public void DAifcPs_FiveCars_X4X5_BetaOuterReproducesThePublishedPair(SupervisedIntersectionRule rule)
    {
        // reported: (0.70, 0.25); {x1, …, x4} (typical point x4), {x5}
        var labels = new[] { Label(4, 1.0, 0.0), Label(5, 0.9, 0.1) };
        var outer = Supervised(RoundedFiveCars(), 2, ThresholdScanOrder.BetaOuter, rule, labels);
        var inner = Supervised(RoundedFiveCars(), 2, ThresholdScanOrder.BetaInner, rule, labels);

        var best = Assert.Single(outer);
        Assert.Equal((0.70, 0.25), (best.Alpha, best.Beta!.Value));
        Assert.Equal([4, 5], OneBased(best.Centers));
        Assert.Equal([[0, 1, 2, 3], [4]], best.MemberSets);
        Assert.Equal(0.8225, best.Score, precision: 12);

        var printed = Assert.Single(inner);
        Assert.Equal((0.63, 0.13), (printed.Alpha, printed.Beta!.Value));
        Assert.Equal([[0, 1, 2, 3], [4]], printed.MemberSets);
    }

    [Theory]
    [InlineData(ThresholdScanOrder.BetaInner, SupervisedIntersectionRule.AtCurrentPair)]
    [InlineData(ThresholdScanOrder.BetaOuter, SupervisedIntersectionRule.AtCurrentPair)]
    [InlineData(ThresholdScanOrder.BetaInner, SupervisedIntersectionRule.AfterFullScan)]
    [InlineData(ThresholdScanOrder.BetaOuter, SupervisedIntersectionRule.AfterFullScan)]
    public void DAifcPs_FiveCars_X3X4_NoAllotmentExists(ThresholdScanOrder order, SupervisedIntersectionRule rule)
    {
        // reported: no allotment exists; reproduced under both orders (and under both rules for w)
        Assert.Empty(Supervised(RoundedFiveCars(), 2, order, rule,
                                Label(3, 0.8, 0.1), Label(4, 0.7, 0.2)));
    }

    [Fact]
    public void DAifcPs_FiveCars_DPaifcComparison()
    {
        // D-PAIFC in the same manuscript: (0.63, 0.13), typical points x2, x5; F = 0.765, the maximum of four allotments
        var found = new DPaifcAlgorithm().Run(RoundedFiveCars(), Token);

        Assert.Equal(4, found.Count);
        Assert.Equal((0.63, 0.13), (found[0].Alpha, found[0].Beta!.Value));
        Assert.Equal([2, 5], OneBased(found[0].Centers));
        Assert.Equal(0.765, found[0].Score, precision: 12);
    }

    private static readonly IntuitionisticLabelledObject[] HungLabels =
        [Label(2, 0.7, 0.2), Label(3, 0.5, 0.3), Label(5, 0.6, 0.3), Label(8, 0.5, 0.4)];

    [Theory]
    [InlineData(ThresholdScanOrder.BetaInner)]
    [InlineData(ThresholdScanOrder.BetaOuter)]
    public void DAifcPs_Hung_PrintedStep10_FindsNoSolution(ThresholdScanOrder order)
    {
        // not reproduced: with w increased at the current pair as in Step 10, no solution is found
        Assert.Empty(Supervised(IntuitionisticTests.Hung(), 4, order, SupervisedIntersectionRule.AtCurrentPair, HungLabels));
    }

    [Theory]
    [InlineData(ThresholdScanOrder.BetaInner, 0.1, 0.3)]
    [InlineData(ThresholdScanOrder.BetaOuter, 0.6, 0.4)]
    public void DAifcPs_Hung_FullScan_FindsAnAllotmentWithW1First(ThresholdScanOrder order, double alpha, double beta)
    {
        // with w increased only after a full threshold scan, an admissible allotment with w = 1 is
        // found first: at (0.1, 0.3) with beta inner, at (0.6, 0.4) with beta outer
        var found = Supervised(IntuitionisticTests.Hung(), 4, order, SupervisedIntersectionRule.AfterFullScan, HungLabels);

        Assert.NotEmpty(found);
        Assert.Equal((alpha, beta), (found[0].Alpha, found[0].Beta!.Value));
        Assert.All(found, result => Assert.Equal(1, result.IntersectionBound));
        Assert.All(found, result => Assert.NotNull(LabelConstraint.AssignClasses(result, HungLabels)));
    }

    [Fact]
    public void DAifcPs_Hung_ReportedClustersFormAConsistentAllotmentWithW2()
    {
        // the reported clusters at (0.1, 0.4): {x2, x6, x9} (x6), {x3, x5, x10} (x10), {x1, x4, x5, x8} (x1),
        // {x1, x2, x7, x8} (x8); they cover X, are consistent with the labels, and |C1 ∩ C8| = 2
        var relation = IntuitionisticTests.Hung();
        const double alpha = 0.1, beta = 0.4;
        int[] typicalPoints = [5, 9, 0, 7];
        var memberSets = typicalPoints
            .Select(row => Enumerable.Range(0, relation.Size)
                .Where(column => relation.Membership(row, column) >= alpha && relation.NonMembership(row, column) <= beta)
                .ToArray())
            .ToArray();

        Assert.Equal([[1, 5, 8], [2, 4, 9], [0, 3, 4, 7], [0, 1, 6, 7]], memberSets);
        Assert.Equal(10, memberSets.SelectMany(set => set).Distinct().Count());
        Assert.Equal(2, memberSets[2].Intersect(memberSets[3]).Count());

        var membership = new double[relation.Size, 4];
        var nonMembership = new double[relation.Size, 4];
        for (int cluster = 0; cluster < 4; cluster++)
            foreach (int member in memberSets[cluster])
            {
                membership[member, cluster] = relation.Membership(typicalPoints[cluster], member);
                nonMembership[member, cluster] = relation.NonMembership(typicalPoints[cluster], member);
            }
        var reported = new ClusterResult(4, alpha, typicalPoints, memberSets, membership, 0.0, "F_IFS",
                                         Beta: beta, NonMembership: nonMembership, IntersectionBound: 2);
        Assert.NotNull(LabelConstraint.AssignClasses(reported, HungLabels));
    }

    [Fact]
    public void DAifcPs_Hung_DAifcComparison()
    {
        // D-AIFC(c) for comparison in the same manuscript: c = 4 at (0.1, 0.3)
        var found = new DAifcAlgorithm(4).Run(IntuitionisticTests.Hung(), Token);
        Assert.Equal((0.1, 0.3), (found[0].Alpha, found[0].Beta!.Value));
    }

    [Theory]
    [InlineData(0.7, 0.4)]    // y + f > 1
    [InlineData(0.0, 0.2)]    // y must be positive
    [InlineData(0.5, 1.0)]    // f must be below 1
    public void DAifcPs_IncorrectConditionsOfClassification_AreRejected(double membership, double nonMembership)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DAifcPsAlgorithm(2, [Label(1, membership, nonMembership), Label(5, 1.0, 0.0)]));
        Assert.Contains("conditions of classification are incorrect", exception.Message);
    }

    [Fact]
    public void DAifcPs_OrderFree_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DAifcPsAlgorithm(2, [Label(1, 1.0, 0.0), Label(5, 1.0, 0.0)], new IntuitionisticOptions(ThresholdScanOrder.OrderFree))
                .Run(RoundedFiveCars(), Token));
        Assert.Contains("beta-inner or beta-outer", exception.Message);
    }

    [Fact]
    public void DAifcPs_ClusterCountOutOfRange_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new DAifcPsAlgorithm(5, [Label(1, 1.0, 0.0)]).Run(RoundedFiveCars(), Token));
    }

    // ─────────────── partially supervised classification (two steps) ───────────────

    [Fact]
    public void Ssfcm_Properties_OnSneathSokal()
    {
        var data = MaxNormalization.Instance.Apply(BookExampleTests.SneathSokal());
        LabelledObject[] labels = [new(1, 1.0, 0), new(12, 1.0, 1)];

        foreach (var weighting in new[] { SupervisionWeighting.Plain, SupervisionWeighting.Balanced })
        {
            var partition = new PartiallySupervisedFuzzyCMeans(weighting).Run(data, 2, labels, Token);

            Assert.True(partition.Converged);
            for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
            {
                double total = partition.Membership[objectIndex, 0] + partition.Membership[objectIndex, 1];
                Assert.Equal(1.0, total, precision: 12);
                Assert.InRange(partition.Membership[objectIndex, 0], 0.0, 1.0);
            }
            Assert.Equal(0, partition.Assignment[1]);
            Assert.Equal(1, partition.Assignment[12]);
            // the two groups of the data lie on the two sides of the line x = 4
            for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
                if (objectIndex != 4)
                    Assert.Equal(BookExampleTests.SneathSokal()[objectIndex, 0] < 4 ? 0 : 1, partition.Assignment[objectIndex]);
        }
    }

    [Fact]
    public void Ssfcm_LabelledClassesMustCoverTheClusters()
    {
        var data = BookExampleTests.SneathSokal();
        Assert.Throws<ArgumentException>(() =>
            new PartiallySupervisedFuzzyCMeans().Run(data, 2, [new LabelledObject(1, 1.0, 0)], Token));
        Assert.Throws<ArgumentException>(() =>
            new PartiallySupervisedFuzzyCMeans().Run(data, 2, [new LabelledObject(1, 1.0, 0), new LabelledObject(2, 1.0, 2)], Token));
    }

    [Fact]
    public void TypicalPointClassification_Iris_StableWithRespectToTheDistance()
    {
        // Viattchenin 2013, sec. 4.1.2: normalisation (2.11), ε = 1e-5, D-AFC(3) with three distances,
        // its typical points as labelled objects, then Pedrycz's algorithm. The first step reproduces the
        // thresholds of the monograph (Table 2.6); two of the three typical points are common to all three
        // experiments (x95 and x98 in the numbering of the monograph). The monograph reports five
        // misclassified objects in each experiment, against 15, 6 and 6 for D-AFC(3) alone. The
        // recomputation with eq. (4.2) or (4.3), prototypes started at the labelled objects, gives the
        // same assignment in all three experiments, as the monograph says, but six misclassified objects
        // (two versicolor plants as virginica, four virginica as versicolor), not five; the initialisation
        // and the stopping rule of the original computation are not stated. Here the stability and the
        // bound by the first step are tested.
        var iris = LabeledDataset.TryFromCsv(DatasetPath("iris_labeled.csv"))!;
        (IDistance Distance, double Alpha, int FirstStepErrors)[] experiments =
        [
            (HammingDistance.Instance, 0.81916, 15),
            (EuclideanDistance.Instance, 0.81044, 6),
            (SquaredEuclideanDistance.Instance, 0.96418, 6),
        ];

        var assignments = new List<int[]>();
        var labelSets = new List<int[]>();
        foreach (var (distance, alpha, firstStepErrors) in experiments)
        {
            var result = new TypicalPointClassification(MonographBuilder(distance, 1e-5)).Run(iris.Data, 3, Token)!;

            Assert.Equal(alpha, result.Allotment.Alpha, precision: 5);
            Assert.True(result.Partition.Converged);
            Assert.Equal(result.Allotment.Centers, result.Labels.Select(label => label.ObjectIndex));
            Assert.True(Misclassified(result.Partition.Assignment, iris.TrueLabels) <= firstStepErrors);
            assignments.Add(result.Partition.Assignment);
            labelSets.Add(result.Allotment.Centers);
        }

        Assert.Equal(2, labelSets[0].Intersect(labelSets[1]).Intersect(labelSets[2]).Count());
        Assert.Equal(assignments[0], assignments[1]);
        Assert.Equal(assignments[0], assignments[2]);
    }

    /// <summary>Misclassified objects under the best one-to-one matching of clusters and classes.</summary>
    private static int Misclassified(int[] assignment, int[] truth)
    {
        int clusterCount = assignment.Max() + 1;
        int best = int.MaxValue;
        foreach (var permutation in Permutations(Enumerable.Range(0, System.Math.Max(clusterCount, truth.Max() + 1)).ToArray()))
            best = System.Math.Min(best, assignment.Where((cluster, objectIndex) => permutation[cluster] != truth[objectIndex]).Count());
        return best;
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (int first = 0; first < items.Length; first++)
            foreach (var rest in Permutations([.. items.Where((_, position) => position != first)]))
                yield return [items[first], .. rest];
    }
}
