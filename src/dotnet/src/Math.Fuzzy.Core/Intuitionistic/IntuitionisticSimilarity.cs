namespace MathFuzzy.Core.Intuitionistic;

/// <summary>Similarity measure between intuitionistic fuzzy sets used to build a tolerance from attribute data.</summary>
public enum IntuitionisticMeasure
{
    /// <summary>
    /// Hamming-type measure (Wang, Xu, Liu and Tang 2011, as used by Viattchenin and Shyrai 2015):
    /// ν_ij = (1/m)·Σ_t |ν_i^t − ν_j^t|, μ_ij = 1 − ν_ij − (1/m)·Σ_t |π_i^t − π_j^t|.
    /// </summary>
    Hamming,

    /// <summary>
    /// Euclidean-type measure (Kacprzyk, Viattchenin, Shyrai and Szmidt 2016, eq. (6)):
    /// μ_ij = 1 − (1/(2m))·Σ_t √((Δν^t)² + (Δπ^t)²), ν_ij = (1/(2m))·Σ_t |Δν^t|.
    /// </summary>
    Euclidean
}

/// <summary>
/// Construction of an intuitionistic fuzzy tolerance from intuitionistic attribute data, and
/// its max-min / min-max transitive closure.
/// </summary>
public static class IntuitionisticSimilarity
{
    /// <summary>The intuitionistic fuzzy tolerance of the objects of <paramref name="data"/>.</summary>
    public static IntuitionisticToleranceMatrix BuildTolerance(IntuitionisticDataMatrix data, IntuitionisticMeasure measure)
    {
        int objects = data.Objects;
        var membership = new double[objects, objects];
        var nonMembership = new double[objects, objects];
        for (int first = 0; first < objects; first++)
        {
            membership[first, first] = 1.0;
            for (int second = first + 1; second < objects; second++)
            {
                var (degree, opposite) = Similarity(
                    data.Attributes,
                    attribute => (data.NonMembership(first, attribute), data.Hesitancy(first, attribute)),
                    attribute => (data.NonMembership(second, attribute), data.Hesitancy(second, attribute)),
                    measure);
                membership[first, second] = membership[second, first] = degree;
                nonMembership[first, second] = nonMembership[second, first] = opposite;
            }
        }
        return new IntuitionisticToleranceMatrix(membership, nonMembership);
    }

    /// <summary>
    /// Similarity (μ, ν) of two intuitionistic fuzzy sets given by the non-membership degree and
    /// the hesitancy of every attribute.
    /// </summary>
    public static (double Membership, double NonMembership) Similarity(
        int attributes,
        Func<int, (double NonMembership, double Hesitancy)> first,
        Func<int, (double NonMembership, double Hesitancy)> second,
        IntuitionisticMeasure measure)
    {
        double nonMembershipDifference = 0.0, hesitancyDifference = 0.0, euclidean = 0.0;
        for (int attribute = 0; attribute < attributes; attribute++)
        {
            var (firstOpposite, firstHesitancy) = first(attribute);
            var (secondOpposite, secondHesitancy) = second(attribute);
            double deltaOpposite = Math.Abs(firstOpposite - secondOpposite);
            double deltaHesitancy = Math.Abs(firstHesitancy - secondHesitancy);
            nonMembershipDifference += deltaOpposite;
            hesitancyDifference += deltaHesitancy;
            euclidean += Math.Sqrt(deltaOpposite * deltaOpposite + deltaHesitancy * deltaHesitancy);
        }

        if (measure == IntuitionisticMeasure.Hamming)
        {
            double opposite = nonMembershipDifference / attributes;
            return (Math.Max(0.0, 1.0 - opposite - hesitancyDifference / attributes), opposite);
        }
        return (Math.Max(0.0, 1.0 - euclidean / (2.0 * attributes)), nonMembershipDifference / (2.0 * attributes));
    }

    /// <summary>
    /// Transitive closure of an intuitionistic fuzzy tolerance with the minimum t-norm and the
    /// maximum s-norm: μ̃(x, z) = max_y min(μ̃(x, y), μ̃(y, z)), ν̃(x, z) = min_y max(ν̃(x, y), ν̃(y, z)),
    /// iterated until nothing changes. The membership part is the max-min closure of μ and the
    /// non-membership part the min-max closure of ν.
    /// </summary>
    public static IntuitionisticToleranceMatrix TransitiveClosure(IntuitionisticToleranceMatrix relation)
    {
        int size = relation.Size;
        var membership = relation.MembershipMatrix();
        var nonMembership = relation.NonMembershipMatrix();

        // Floyd–Warshall order: after pivot k every chain through objects 0..k is accounted for.
        for (int pivot = 0; pivot < size; pivot++)
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                {
                    double through = Math.Min(membership[row, pivot], membership[pivot, column]);
                    if (through > membership[row, column]) membership[row, column] = through;
                    double oppositeThrough = Math.Max(nonMembership[row, pivot], nonMembership[pivot, column]);
                    if (oppositeThrough < nonMembership[row, column]) nonMembership[row, column] = oppositeThrough;
                }

        return new IntuitionisticToleranceMatrix(membership, nonMembership);
    }
}
