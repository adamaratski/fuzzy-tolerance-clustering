using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Clustering.Validation;

/// <summary>
/// Validity measures of an allotment among fuzzy clusters (Viattchenin 2013, sec. 2.4;
/// Viattchenin 2016): the linear and quadratic measures of fuzziness and the measure of
/// separation and compactness. Smaller fuzziness and larger separation–compactness are better.
/// </summary>
/// <param name="LinearFuzziness">V_LMF = Σ_l I_L(A^l): sum of the linear indices of fuzziness of the clusters.</param>
/// <param name="QuadraticFuzziness">V_QMF = Σ_l I_Q(A^l): sum of the quadratic indices of fuzziness.</param>
/// <param name="SeparationCompactness">
/// V_MSC = (1/c)·Σ_l D(A^l) + (c/n)·Σ_{x ∈ Ξ} Σ_{l: x ∈ A^l} μ_l(x) − α, where D is the density of a
/// cluster and Ξ is the set of objects that belong to more than one cluster.
/// </param>
/// <param name="Clusters">The per-cluster indices, in the order of the clusters of the allotment.</param>
public sealed record AllotmentMeasures(
    double LinearFuzziness,
    double QuadraticFuzziness,
    double SeparationCompactness,
    IReadOnlyList<ClusterIndices> Clusters)
{
    /// <summary>
    /// Computes the measures of an allotment. For a cluster with typical point τ and support of
    /// n_l objects with memberships μ_j, and the nearest crisp set (1 where μ_j &gt; 0.5, else 0):
    ///   I_L = (2/n_l)·Σ_j |μ_j − crisp_j|,
    ///   I_Q = (2/√n_l)·√(Σ_j (μ_j − crisp_j)²),
    ///   D   = (1/n_l)·Σ_j μ_j.
    /// </summary>
    public static AllotmentMeasures Compute(ClusterResult allotment)
    {
        int clusterCount = allotment.ClusterCount;
        int objectCount = allotment.Membership.GetLength(0);
        var clusters = new ClusterIndices[clusterCount];
        var coverage = new int[objectCount];
        double linearSum = 0.0, quadraticSum = 0.0, densitySum = 0.0;

        for (int cluster = 0; cluster < clusterCount; cluster++)
        {
            var members = allotment.MemberSets[cluster];
            double absoluteDeviation = 0.0, squaredDeviation = 0.0, membershipSum = 0.0;
            foreach (int member in members)
            {
                double membership = allotment.Membership[member, cluster];
                double deviation = membership > 0.5 ? 1.0 - membership : membership;
                absoluteDeviation += deviation;
                squaredDeviation += deviation * deviation;
                membershipSum += membership;
                coverage[member]++;
            }

            double linear = 2.0 * absoluteDeviation / members.Length;
            double quadratic = 2.0 * Math.Sqrt(squaredDeviation) / Math.Sqrt(members.Length);
            double density = membershipSum / members.Length;
            clusters[cluster] = new ClusterIndices(linear, quadratic, density);
            linearSum += linear;
            quadraticSum += quadratic;
            densitySum += density;
        }

        // memberships of the objects that lie in intersection areas, over all their clusters
        double sharedMembership = 0.0;
        for (int cluster = 0; cluster < clusterCount; cluster++)
            foreach (int member in allotment.MemberSets[cluster])
                if (coverage[member] > 1)
                    sharedMembership += allotment.Membership[member, cluster];

        double separationCompactness = densitySum / clusterCount
                                       + (double)clusterCount / objectCount * sharedMembership
                                       - allotment.Alpha;
        return new AllotmentMeasures(linearSum, quadraticSum, separationCompactness, clusters);
    }

    /// <summary>The measure named <c>lmf</c>, <c>qmf</c> or <c>msc</c>.</summary>
    public double Value(string measure) => measure.ToLowerInvariant().Trim() switch
    {
        "lmf" => LinearFuzziness,
        "qmf" => QuadraticFuzziness,
        "msc" => SeparationCompactness,
        _ => throw new ArgumentException($"Unknown validity measure: '{measure}'. Valid names: lmf, qmf, msc")
    };
}

/// <summary>Linear and quadratic index of fuzziness and density of one fuzzy cluster.</summary>
public sealed record ClusterIndices(double LinearFuzziness, double QuadraticFuzziness, double Density);
