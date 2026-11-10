using MathFuzzy.Clustering.Algorithms.Interval;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Clustering.Validation;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Distance;
using MathFuzzy.Core.Interval;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Tests;

/// <summary>
/// Interval-valued data against the published examples: the oil data of Ichino and Yaguchi
/// (Viattchenin 2011, sec. 4.2), the artificial data of Sato-Ilic and Jain (Viattchenin 2013,
/// sec. 3.4.2) and the paths of a project network (Viattchenin 2015, sec. 3). Unless a comment
/// says otherwise the expected values are the recomputed values of the book (chapter 5,
/// "Clustering Interval-Valued Data"; chapter 6, "Selecting Paths in a Project Network") and of
/// the source reports written for it; the sources print them truncated to four decimals.
/// </summary>
public class IntervalDataTests
{
    private static string DataFile(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datasets", file);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"datasets/{file} not found above the test binaries.");
    }

    private static IntervalDataMatrix Oils() => IntervalDataMatrix.FromCsv(DataFile("ichino_oils_interval.csv"));
    private static IntervalDataMatrix SatoJain() => IntervalDataMatrix.FromCsv(DataFile("sato_jain_interval.csv"));
    private static IntervalDataMatrix Paths() => IntervalDataMatrix.FromCsv(DataFile("project_paths_interval.csv"));

    private static IntervalStabilityResult Analyze(IntervalDataMatrix data, IntervalStabilityOptions? options = null) =>
        IntervalStability.Analyze(data, options, TestContext.Current.CancellationToken);

    private static StableStructureResult Run(IntervalDataMatrix data, StableStructureOptions? options = null) =>
        StableStructureTechnique.Run(data, options, TestContext.Current.CancellationToken);

    private static ProjectPathSelectionResult SelectPaths(IntervalDataMatrix data, StableStructureOptions? options = null) =>
        ProjectPathSelection.Select(data, options, PathSelectionRule.MaximalDensity, TestContext.Current.CancellationToken);

    /// <summary>Member sets as 1-based object numbers, sorted, for readable assertions.</summary>
    private static string Classes(ClusterResult result) =>
        string.Join(" | ", result.MemberSets.Select(set => string.Join(",", set.Order().Select(member => member + 1)))
                                            .Order(StringComparer.Ordinal));

    private static int[] TypicalPoints(ClusterResult result) => [.. result.Centers.Select(center => center + 1).Order()];

    // ── Reading and normalisation ───────────────────────────────────────────────────────

    [Fact]
    public void Oil_file_is_read_as_paired_bound_columns()
    {
        var oils = Oils();
        Assert.Equal(8, oils.Objects);
        Assert.Equal(3, oils.Attributes);
        Assert.Equal(["specific_gravity", "iodine_value", "saponification_value"], oils.AttributeNames);
        Assert.Equal("linseed", oils.ObjectNames![0]);
        Assert.Equal(118, oils.Lower(0, 2));
        Assert.Equal(196, oils.Upper(0, 2));
        Assert.Equal(0.858, oils.Lower(7, 0));
        Assert.False(oils.IsDegenerate);
    }

    [Fact]
    public void Reader_rejects_a_lower_bound_above_the_upper_bound_and_an_odd_number_of_columns()
    {
        Assert.Throws<FormatException>(() => IntervalDataMatrix.Parse("a_lo,a_hi\n2,1\n0,3\n"));
        Assert.Throws<FormatException>(() => IntervalDataMatrix.Parse("a_lo,a_hi,b_lo\n1,2,3\n0,3,4\n"));
    }

    [Fact]
    public void Oil_data_joint_normalisation()
    {
        // Source report (interval section, test vector): x₁ and x₈ after eq. (23).
        var normalized = Oils().NormalizeJointly();
        double[] linseed = [0.9351, 1.0, 0.7738, 0.9762, 0.0, 0.9286];
        double[] hogFat = [0.0, 0.0779, 0.0774, 0.2202, 0.8571, 1.0];
        for (int attribute = 0; attribute < 3; attribute++)
        {
            Assert.Equal(linseed[2 * attribute], normalized.Lower(0, attribute), 4);
            Assert.Equal(linseed[2 * attribute + 1], normalized.Upper(0, attribute), 4);
            Assert.Equal(hogFat[2 * attribute], normalized.Lower(7, attribute), 4);
            Assert.Equal(hogFat[2 * attribute + 1], normalized.Upper(7, attribute), 4);
        }
    }

    // ── Tolerances ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Oil_cross_bound_squared_euclidean_tolerance_keeps_its_diagonal()
    {
        var tolerance = IntervalTolerance.Build(Oils(), IntervalMeasure.SquaredEuclidean);
        double[] perilla = [0.8608, 0.9959, 0.8784, 0.8955, 0.8160, 0.8243, 0.4516, 0.4908];
        double[] hogFat = [0.3854, 0.4908, 0.7980, 0.7530, 0.8185, 0.8158, 0.9873, 0.9922];
        double[] diagonal = [0.8488, 0.9959, 0.9968, 0.9973, 0.9996, 0.9967, 0.9949, 0.9922];
        for (int column = 0; column < 8; column++)
        {
            Assert.Equal(perilla[column], tolerance[1, column], 4);
            Assert.Equal(hogFat[column], tolerance[7, column], 4);
            Assert.Equal(diagonal[column], tolerance[column, column], 4);
        }
    }

    [Fact]
    public void Ju_Yuan_tolerance_row_of_sesame()
    {
        var tolerance = IntervalTolerance.Build(Oils(), IntervalMeasure.JuYuan, lambda: 2.0);
        double[] sesame = [0.6443, 0.6821, 0.9472, 1, 0.8888, 0.8992, 0.5085, 0.5083];
        for (int column = 0; column < 8; column++)
            Assert.Equal(sesame[column], tolerance[3, column], 4);
    }

    [Theory]
    [InlineData(IntervalMeasure.BurilloBustince)]
    [InlineData(IntervalMeasure.Hausdorff)]
    [InlineData(IntervalMeasure.JuYuan)]
    [InlineData(IntervalMeasure.SquaredEuclidean)]
    [InlineData(IntervalMeasure.Euclidean)]
    [InlineData(IntervalMeasure.SquaredEuclideanMatchingBounds)]
    [InlineData(IntervalMeasure.EuclideanMatchingBounds)]
    public void Tolerances_are_symmetric_in_the_unit_interval_and_reflexive_except_the_cross_bound_forms(IntervalMeasure measure)
    {
        var data = Oils();
        var tolerance = IntervalTolerance.Build(data, measure);
        var normalized = data.NormalizeJointly();
        for (int row = 0; row < tolerance.Size; row++)
        {
            for (int column = 0; column < tolerance.Size; column++)
            {
                Assert.Equal(tolerance[row, column], tolerance[column, row]);
                Assert.InRange(tolerance[row, column], 0.0, 1.0);
            }
            if (!IntervalTolerance.IsCrossBound(measure))
            {
                Assert.Equal(1.0, tolerance[row, row]);
                continue;
            }
            // ε(x, x) = (1/2m)·Σ_t (μ^max − μ^min)²
            double widths = Enumerable.Range(0, data.Attributes)
                .Sum(attribute => System.Math.Pow(normalized.Upper(row, attribute) - normalized.Lower(row, attribute), 2));
            double selfDistance = widths / (2.0 * data.Attributes);
            double expected = measure == IntervalMeasure.SquaredEuclidean ? 1.0 - selfDistance : 1.0 - System.Math.Sqrt(selfDistance);
            Assert.Equal(expected, tolerance[row, row], 12);
        }
    }

    [Fact]
    public void Degenerate_intervals_give_the_tolerance_of_the_point_data()
    {
        var lower = SatoJain().LowerBounds();
        var values = new double[lower.Rows, lower.Cols];
        for (int row = 0; row < lower.Rows; row++)
            for (int column = 0; column < lower.Cols; column++)
                values[row, column] = lower[row, column];
        var degenerate = new IntervalDataMatrix(values, values);
        Assert.True(degenerate.IsDegenerate);

        var pointTolerance = new ToleranceMatrixBuilder(SquaredEuclideanDistance.Instance,
            MathFuzzy.Core.Normalization.MinMaxNormalization.Instance).Build(lower);
        var intervalTolerance = IntervalTolerance.Build(degenerate, IntervalMeasure.SquaredEuclidean);
        for (int row = 0; row < lower.Rows; row++)
            for (int column = 0; column < lower.Rows; column++)
                Assert.Equal(pointTolerance[row, column], intervalTolerance[row, column], 12);
    }

    // ── Stability of the clustering structure ───────────────────────────────────────────

    [Fact]
    public void Oil_structure_is_unstable_with_separately_normalised_bounds()
    {
        var stability = Analyze(Oils());
        Assert.Equal(ClusteringStructure.Unstable, stability.Structure);

        Assert.Equal(2, stability.Lower.ClusterCount);
        Assert.Equal(0.789977, stability.Lower.Alpha, 6);
        Assert.Equal("1 | 2,3,4,5,6,7,8", Classes(stability.Lower.Allotment));

        Assert.Equal(3, stability.Upper.ClusterCount);
        Assert.Equal(0.949632, stability.Upper.Alpha, 6);
        Assert.Equal("1,2 | 3,4,5,6 | 7,8", Classes(stability.Upper.Allotment));
    }

    [Fact]
    public void Oil_structure_with_the_printed_joint_normalisation()
    {
        // Open point of the source report: with the joint normalisation both matrices give c = 3,
        // the structure would be quasi-stable, contradicting Fig. 1 of the source.
        var stability = Analyze(Oils(), new IntervalStabilityOptions(Normalization: BoundNormalization.Joint));
        Assert.Equal(ClusteringStructure.QuasiStable, stability.Structure);
        Assert.Equal(0.902872, stability.Lower.Alpha, 6);
        Assert.Equal("1 | 2,3,4,5,6 | 7,8", Classes(stability.Lower.Allotment));
        Assert.Equal(0.987618, stability.Upper.Alpha, 6);
        Assert.Equal("1,2 | 3,4,5,6 | 7,8", Classes(stability.Upper.Allotment));
    }

    [Fact]
    public void Degenerate_interval_data_have_a_stable_structure()
    {
        var oils = Oils();
        var lower = new double[oils.Objects, oils.Attributes];
        for (int row = 0; row < oils.Objects; row++)
            for (int attribute = 0; attribute < oils.Attributes; attribute++)
                lower[row, attribute] = oils.Lower(row, attribute);
        var stability = Analyze(new IntervalDataMatrix(lower, lower));
        Assert.Equal(ClusteringStructure.Stable, stability.Structure);
    }

    [Theory]
    [InlineData(SNorm.Maximum, new[] { 1.0, 1.0, 0.8, 0.6, 0.4 }, new[] { 2, 3, 4 })]
    [InlineData(SNorm.ProbabilisticSum, new[] { 1.0, 1.0, 0.933, 0.8, 0.6 }, new[] { 2, 3, 4, 5 })]
    [InlineData(SNorm.BoundedSum, new[] { 1.0, 1.0, 1.0, 1.0, 0.733 }, new[] { 2, 3, 4, 5 })]
    public void Oil_possibility_of_the_numbers_of_clusters(SNorm norm, double[] degrees, int[] expectedCounts)
    {
        var plausible = PlausibleClusterCounts.Compute(Analyze(Oils()), 8, norm);
        Assert.Equal(0.789977, plausible.Level, 6);
        for (int count = 2; count <= 6; count++)
            Assert.Equal(degrees[count - 2], plausible.Possibility[count], 3);
        Assert.Equal(expectedCounts, plausible.Counts);
    }

    // ── The stable clustering structure ─────────────────────────────────────────────────

    [Fact]
    public void Oil_stable_structure_with_the_cross_bound_squared_euclidean_measure()
    {
        var result = Run(Oils(), new StableStructureOptions(IntervalMeasure.SquaredEuclidean));
        Assert.Equal([2, 3, 4], result.Plausible!.Counts);
        Assert.Equal([2, 3, 4], result.Candidates.Select(candidate => candidate.ClusterCount));
        Assert.True(result.Candidates[0].IsAdmissible);
        Assert.True(result.Candidates[1].IsAdmissible);

        var two = result.Candidates[0];
        Assert.Equal(0.4568, two.Validity!.Value, 4);
        Assert.Equal(0.818499, two.Allotment!.Alpha, 6);          // printed 0.8184 (truncated)
        Assert.Equal(0.186656, two.Allotment.Score, 6);
        Assert.Equal("1,2,3,4,6 | 5,7,8", Classes(two.Allotment));
        Assert.Equal([2, 8], TypicalPoints(two.Allotment));

        var three = result.Candidates[1];
        Assert.Equal(0.3281, three.Validity!.Value, 4);
        Assert.Equal(0.831375, three.Allotment!.Alpha, 6);
        Assert.Equal(0.343538, three.Allotment.Score, 6);
        Assert.Equal("1,2 | 3,4,5,6 | 7,8", Classes(three.Allotment));
        Assert.Equal([1, 6, 8], TypicalPoints(three.Allotment));

        Assert.Same(two.Allotment, result.Selected);
        // camellia oil: membership in the second cluster equal to the threshold; similarity to x₂ 0.8160
        int animalCluster = Array.IndexOf(two.Allotment.Centers, 7);
        Assert.Equal(0.8185, two.Allotment.Membership[4, animalCluster], 4);
        Assert.Equal(0.8160, result.Tolerance[4, 1], 4);
    }

    /// <summary>
    /// The bound w is the number of objects shared by the clusters of an allotment, and two typical
    /// points never lie in each other's clusters. Under this reading the oil data admit no allotment
    /// among four clusters for the squared Euclidean measure. The value V_QMF = 0.4451 of the source
    /// belongs to the typical points x2, x6, x7, x8 at α = 0.831375, where x7 and x8 lie in each
    /// other's clusters and five objects are shared: it needs w as a bound on each pairwise
    /// intersection.
    /// </summary>
    [Fact]
    public void Oil_cross_bound_squared_euclidean_measure_admits_no_four_clusters()
    {
        var result = Run(Oils(), new StableStructureOptions(IntervalMeasure.SquaredEuclidean));
        var four = result.Candidates[2];
        Assert.Equal(4, four.ClusterCount);
        Assert.Null(four.Allotment);
        Assert.False(four.IsAdmissible);
    }

    [Fact]
    public void Oil_stable_structure_with_the_Ju_Yuan_measure()
    {
        var result = Run(Oils(), new StableStructureOptions(IntervalMeasure.JuYuan, Lambda: 2.0));
        var two = result.Candidates[0];
        Assert.True(two.IsAdmissible);
        Assert.Equal(0.5221, two.Validity!.Value, 4);
        Assert.Equal(0.585281, two.Allotment!.Alpha, 6);          // printed 0.5852 (truncated)
        Assert.Equal(0.633627, two.Allotment.Score, 6);
        Assert.Equal("1,2,3,4,5,6 | 7,8", Classes(two.Allotment));
        Assert.Equal([4, 8], TypicalPoints(two.Allotment));
        Assert.False(result.Candidates[1].IsUnique);               // c = 3: maximiser not unique
        Assert.False(result.Candidates[2].IsUnique);               // c = 4: maximiser not unique
        Assert.Same(two.Allotment, result.Selected);
    }

    [Fact]
    public void Oil_stable_structure_with_the_Hausdorff_measure()
    {
        var result = Run(Oils(), new StableStructureOptions(IntervalMeasure.Hausdorff));
        var two = result.Candidates[0];
        Assert.Equal(0.5704, two.Validity!.Value, 4);
        Assert.Equal(0.524338, two.Allotment!.Alpha, 6);
        Assert.Equal("1 | 2,3,4,5,6,7,8", Classes(two.Allotment));
        Assert.Equal([1, 6], TypicalPoints(two.Allotment));

        var three = result.Candidates[1];
        Assert.True(three.IsAdmissible);
        Assert.Equal(0.5005, three.Validity!.Value, 4);
        Assert.Equal(0.569653, three.Allotment!.Alpha, 6);
        Assert.Equal([1, 3, 8], TypicalPoints(three.Allotment));

        Assert.False(result.Candidates[2].IsUnique);
        Assert.Same(two.Allotment, result.Selected);
    }

    [Fact]
    public void Oil_printed_matching_bound_form_does_not_reproduce_the_published_allotment()
    {
        // Source report, open point 1: the printed form gives α* = 0.910043 and {x1, x2}, {x3..x8}.
        var tolerance = IntervalTolerance.Build(Oils(), IntervalMeasure.SquaredEuclideanMatchingBounds);
        var candidate = StableStructureTechnique.RunForCount(tolerance, 2, F1Criterion.Instance, ct: TestContext.Current.CancellationToken);
        Assert.Equal(0.910043, candidate.Allotment!.Alpha, 6);
        Assert.Equal("1,2 | 3,4,5,6,7,8", Classes(candidate.Allotment));
        Assert.Equal([1, 5], TypicalPoints(candidate.Allotment));
    }

    // ── Sato-Ilic and Jain ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sato_Jain_structure_is_quasi_stable()
    {
        // Normalisation by the maximum (eq. (23), jointly over both bounds), normalised Euclidean
        // distance. The printed thresholds 0.6853 and 0.8151 could not be reproduced; the book's
        // recomputation gives 0.8377 for both.
        var stability = Analyze(SatoJain(), new IntervalStabilityOptions(
            Normalization: BoundNormalization.Joint, Scaling: IntervalScaling.Maximum, Distance: EuclideanDistance.Instance));
        Assert.Equal(ClusteringStructure.QuasiStable, stability.Structure);
        foreach (var bound in stability.Bounds)
        {
            Assert.Equal(2, bound.ClusterCount);
            Assert.Equal(0.8377, bound.Alpha, 4);
            Assert.Equal("1,2,3,4 | 5,6,7,8", Classes(bound.Allotment));
        }

        static double[] PrototypeOf(BoundClustering bound, int member) =>
            bound.Prototypes[Array.FindIndex(bound.Allotment.MemberSets, set => set.Contains(member))];
        Assert.Equal([11.5, 2.75, 3.25], PrototypeOf(stability.Lower, 0));
        Assert.Equal([11.5, 2.75, 3.25], PrototypeOf(stability.Upper, 0));
        Assert.Equal([3.75, 9.5, 1.5], PrototypeOf(stability.Lower, 4));
        Assert.Equal([9.5, 9.5, 10.0], PrototypeOf(stability.Upper, 4));
    }

    [Theory]
    // Values as printed (Viattchenin 2013, sec. 3.4.2), truncated to five decimals there.
    [InlineData(IntervalMeasure.JuYuan, 0.57507, 1.0)]
    [InlineData(IntervalMeasure.Euclidean, 0.52647, 0.64035)]
    public void Sato_Jain_interval_objects_with_D_AFC_2(IntervalMeasure measure, double printedAlpha, double secondHeight)
    {
        var tolerance = IntervalTolerance.Build(SatoJain(), measure, 2.0, IntervalScaling.Maximum);
        var candidate = StableStructureTechnique.RunForCount(tolerance, 2, F1Criterion.Instance,
                                                             ct: TestContext.Current.CancellationToken);
        Assert.True(candidate.IsAdmissible);
        var allotment = candidate.Allotment!;
        Assert.InRange(allotment.Alpha, printedAlpha, printedAlpha + 1e-5);
        Assert.Equal("1,2,3,4 | 5,6,7,8", Classes(allotment));
        Assert.Equal([1, 5], TypicalPoints(allotment));
        int second = Array.IndexOf(allotment.Centers, 4);
        Assert.Equal(secondHeight, allotment.Membership[4, second], 5);
    }

    // ── Paths of a project network ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(IntervalMeasure.BurilloBustince, new[] { 0.4015, 0.3965, 0.2704, 0.4641, 0.3252, 0.4516 })]
    [InlineData(IntervalMeasure.Hausdorff, new[] { 0.2929, 0.2929, 0.1633, 0.3675, 0.2254, 0.3675 })]
    [InlineData(IntervalMeasure.JuYuan, new[] { 0.4195, 0.4127, 0.2843, 0.4770, 0.3361, 0.4609 })]
    [InlineData(IntervalMeasure.Euclidean, new[] { 0.3976, 0.3926, 0.2704, 0.4395, 0.3089, 0.4295 })]
    public void Path_tolerances(IntervalMeasure measure, double[] upperTriangle)
    {
        var tolerance = IntervalTolerance.Build(Paths(), measure);
        int position = 0;
        for (int row = 0; row < 4; row++)
            for (int column = row + 1; column < 4; column++)
                Assert.Equal(upperTriangle[position++], tolerance[row, column], 4);
        if (measure == IntervalMeasure.Euclidean)
            Assert.Equal([0.9038, 0.7936, 0.8035, 0.8239],
                         Enumerable.Range(0, 4).Select(row => System.Math.Round(tolerance[row, row], 4)));
    }

    [Fact]
    public void Path_structure_is_quasi_stable_with_separately_normalised_bounds()
    {
        var stability = Analyze(Paths());
        Assert.Equal(ClusteringStructure.QuasiStable, stability.Structure);
        foreach (var bound in stability.Bounds)
        {
            Assert.Equal(0.6, bound.Alpha, 12);
            Assert.Equal("1 | 2,3,4", Classes(bound.Allotment));
        }
    }

    [Fact]
    public void Path_structure_is_unstable_with_the_joint_normalisation()
    {
        // Source report, open point 3: c = 3 on the lower bounds at α = 0.8256, c = 2 on the upper ones.
        var stability = Analyze(Paths(), new IntervalStabilityOptions(Normalization: BoundNormalization.Joint));
        Assert.Equal(ClusteringStructure.Unstable, stability.Structure);
        Assert.Equal(0.8256, stability.Lower.Alpha, 4);
        Assert.Equal("1 | 2,3 | 4", Classes(stability.Lower.Allotment));
        Assert.Equal(2, stability.Upper.ClusterCount);
    }

    [Theory]
    // Measure, α*, I_L cl. 1, I_L cl. 2, I_Q cl. 1, I_Q cl. 2, D cl. 1, D cl. 2 (Table 3 of the
    // source, truncated there; the six-decimal values of the source report, the thresholds recomputed).
    [InlineData(IntervalMeasure.BurilloBustince, 0.4516, 0.0, 0.610429, 0.0, 0.747690, 1.0, 0.638548)]
    [InlineData(IntervalMeasure.Hausdorff, 0.3675, 0.0, 0.490059, 0.0, 0.600198, 1.0, 0.578363)]
    [InlineData(IntervalMeasure.JuYuan, 0.4609, 0.0, 0.625223, 0.0, 0.765852, 1.0, 0.645945)]
    [InlineData(IntervalMeasure.Euclidean, 0.4295, 0.1923, 0.710322, 0.1923, 0.744967, 0.9038, 0.557500)]
    public void Path_selection_keeps_the_isolated_path(IntervalMeasure measure, double alpha,
        double linearFirst, double linearSecond, double quadraticFirst, double quadraticSecond,
        double densityFirst, double densitySecond)
    {
        var selection = SelectPaths(Paths(), new StableStructureOptions(measure));
        Assert.Equal([2], selection.Structure.Plausible!.Counts);
        var allotment = selection.Allotment;
        Assert.Equal(alpha, allotment.Alpha, 4);
        Assert.Equal("1 | 2,3,4", Classes(allotment));
        Assert.Equal([1, 3], TypicalPoints(allotment));

        int first = Array.IndexOf(allotment.Centers, 0), second = Array.IndexOf(allotment.Centers, 2);
        Assert.Equal(linearFirst, selection.Indices[first].LinearFuzziness, 4);
        Assert.Equal(quadraticFirst, selection.Indices[first].QuadraticFuzziness, 4);
        Assert.Equal(densityFirst, selection.Indices[first].Density, 4);
        Assert.Equal(linearSecond, selection.Indices[second].LinearFuzziness, 6);
        Assert.Equal(quadraticSecond, selection.Indices[second].QuadraticFuzziness, 6);
        Assert.Equal(densitySecond, selection.Indices[second].Density, 6);

        Assert.Equal([0], selection.SelectedPaths);
        foreach (var rule in new[] { PathSelectionRule.MinimalLinearFuzziness, PathSelectionRule.MinimalQuadraticFuzziness })
            Assert.Equal([0], ProjectPathSelection.SelectFrom(selection.Structure, rule).SelectedPaths);
    }

    [Fact]
    public void Path_selection_with_the_printed_matching_bound_form_differs_from_the_published_table()
    {
        // Source report: the printed form gives α* = 0.6122, I_L = 0.5112, I_Q = 0.6261, D = 0.7444
        // for the second cluster — not Table 3 of the source.
        var selection = SelectPaths(Paths(), new StableStructureOptions(IntervalMeasure.EuclideanMatchingBounds));
        var allotment = selection.Allotment;
        Assert.Equal(0.6122, allotment.Alpha, 4);
        Assert.Equal([1, 3], TypicalPoints(allotment));
        int second = Array.IndexOf(allotment.Centers, 2);
        Assert.Equal(0.5112, selection.Indices[second].LinearFuzziness, 4);
        Assert.Equal(0.6261, selection.Indices[second].QuadraticFuzziness, 4);
        Assert.Equal(0.7444, selection.Indices[second].Density, 4);
    }

    [Fact]
    public void Leap_form_matters_for_the_oil_lower_bounds()
    {
        // The difference form of the leap heuristic does not give the published c = 2 on X^min.
        var difference = Analyze(Oils(), new IntervalStabilityOptions(LeapForm: LeapForm.Difference));
        Assert.NotEqual(2, difference.Lower.ClusterCount);
    }
}
