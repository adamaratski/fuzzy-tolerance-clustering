using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Supervision;

/// <summary>Which form of Pedrycz's objective function is minimised (Viattchenin 2013, sec. 4.1.1).</summary>
public enum SupervisionWeighting
{
    /// <summary>
    /// Q⁽¹⁾ = Σ_l Σ_i u_li² d²(x_i, τ_l) + Σ_l Σ_i (u_li − y_li s_i)² d²(x_i, τ_l), eq. (4.2) of the monograph.
    /// </summary>
    Plain,

    /// <summary>
    /// Q⁽²⁾ = a₁ Σ u_li² d² + a₂ Σ (u_li − y_li s_i)² d² with a₁ = 1/(n − n_L), a₂ = 1/n_L, eq. (4.3):
    /// labelled and unlabelled objects have a balanced influence.
    /// </summary>
    Balanced
}

/// <summary>A fuzzy c-partition computed by <see cref="PartiallySupervisedFuzzyCMeans"/>.</summary>
/// <param name="Membership">Membership degrees [object, cluster]; every row sums to 1.</param>
/// <param name="Prototypes">The prototypes τ_l of the clusters, in the space of the given data.</param>
/// <param name="Assignment">The cluster of each object by the maximum-membership rule (ties: the lower cluster number).</param>
/// <param name="Iterations">Number of iterations made.</param>
/// <param name="Converged">True when the prototypes moved less than the tolerance in the last iteration.</param>
public sealed record SupervisedPartition(
    double[,] Membership, double[][] Prototypes, int[] Assignment, int Iterations, bool Converged);

/// <summary>
/// Pedrycz's fuzzy c-means algorithm with partial supervision, the ssfcm-algorithm
/// (Pedrycz 1985; Pedrycz and Waletzky 1997; as stated in Viattchenin 2013, sec. 4.1.1, eqs. (4.1)–(4.3)),
/// with the weighting exponent 2 and the squared Euclidean distance.
///
/// The labelled objects (s_i = 1) carry membership degrees y_li: the degree given for the class of
/// the labelled object, and 0 in the other classes. With the ratio λ = a₂/a₁ of the two terms
/// (λ = 1 for <see cref="SupervisionWeighting.Plain"/>, λ = (n − n_L)/n_L for
/// <see cref="SupervisionWeighting.Balanced"/>), the conditions of the minimum under Σ_l u_li = 1 give
///   u_li = [ (1 + λ(1 − s_i Σ_k y_ki)) / Σ_k d²_li / d²_ki + λ y_li s_i ] / (1 + λ),
///   τ_l = Σ_i w_li x_i / Σ_i w_li,  w_li = u_li² + λ (u_li − y_li s_i)².
/// The prototypes start at the mean of the labelled objects of each class; the iteration stops
/// when no prototype coordinate moves by more than the tolerance. The monograph does not state
/// the initialisation or the stopping rule; these are the choices made here.
/// </summary>
public sealed class PartiallySupervisedFuzzyCMeans(
    SupervisionWeighting weighting = SupervisionWeighting.Plain,
    int maxIterations = 1000,
    double tolerance = 1e-9)
{
    /// <summary>
    /// Computes the fuzzy c-partition of the data. The class of each labelled object is its cluster
    /// number (0 … c−1); a labelled object without a class takes its position in the list. Every
    /// cluster needs at least one labelled object.
    /// </summary>
    public SupervisedPartition Run(RawDataMatrix data, int clusterCount, IReadOnlyList<LabelledObject> labels,
                                   CancellationToken ct = default)
    {
        int objectCount = data.Rows, attributeCount = data.Cols;
        if (clusterCount < 2 || clusterCount >= objectCount)
            throw new ArgumentException($"The number of clusters must satisfy 2 ≤ c < n = {objectCount}; got {clusterCount}.");

        // eq. (4.1) and the partition matrix Y of the labelled objects
        var supervised = new bool[objectCount];
        var given = new double[objectCount, clusterCount];
        var labelledPerCluster = new int[clusterCount];
        for (int position = 0; position < labels.Count; position++)
        {
            var label = labels[position];
            int cluster = label.Class ?? position;
            if (label.ObjectIndex < 0 || label.ObjectIndex >= objectCount)
                throw new ArgumentException($"Labelled object number {label.ObjectIndex + 1} lies outside the set of {objectCount} objects.");
            if (cluster < 0 || cluster >= clusterCount)
                throw new ArgumentException($"The class of the labelled object x{label.ObjectIndex + 1} must be a cluster number 0 … {clusterCount - 1}; got {cluster}.");
            if (supervised[label.ObjectIndex])
                throw new ArgumentException($"Object x{label.ObjectIndex + 1} is labelled more than once.");
            if (!(label.Membership > 0.0 && label.Membership <= 1.0))
                throw new ArgumentException($"The membership degree of the labelled object x{label.ObjectIndex + 1} must lie in (0, 1].");
            supervised[label.ObjectIndex] = true;
            given[label.ObjectIndex, cluster] = label.Membership;
            labelledPerCluster[cluster]++;
        }
        for (int cluster = 0; cluster < clusterCount; cluster++)
            if (labelledPerCluster[cluster] == 0)
                throw new ArgumentException($"Cluster {cluster} has no labelled object; every cluster needs one.");

        int labelledCount = labels.Count;
        double ratio = weighting == SupervisionWeighting.Plain || labelledCount == objectCount
            ? 1.0
            : (double)(objectCount - labelledCount) / labelledCount;

        // initial prototypes: the mean of the labelled objects of each cluster
        var prototypes = new double[clusterCount][];
        for (int cluster = 0; cluster < clusterCount; cluster++) prototypes[cluster] = new double[attributeCount];
        for (int position = 0; position < labels.Count; position++)
        {
            int cluster = labels[position].Class ?? position;
            for (int attribute = 0; attribute < attributeCount; attribute++)
                prototypes[cluster][attribute] += data[labels[position].ObjectIndex, attribute] / labelledPerCluster[cluster];
        }

        var membership = new double[objectCount, clusterCount];
        var squaredDistance = new double[clusterCount];
        int iteration = 0;
        bool converged = false;
        while (iteration < maxIterations && !converged)
        {
            ct.ThrowIfCancellationRequested();
            iteration++;
            UpdateMembership(data, prototypes, supervised, given, ratio, membership, squaredDistance);

            converged = true;
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                var weightedSum = new double[attributeCount];
                double weightTotal = 0.0;
                for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
                {
                    double target = supervised[objectIndex] ? given[objectIndex, cluster] : 0.0;
                    double degree = membership[objectIndex, cluster];
                    double weight = degree * degree + ratio * (degree - target) * (degree - target);
                    weightTotal += weight;
                    for (int attribute = 0; attribute < attributeCount; attribute++)
                        weightedSum[attribute] += weight * data[objectIndex, attribute];
                }
                if (weightTotal <= 0.0) continue;   // no weight: the prototype stays where it is
                for (int attribute = 0; attribute < attributeCount; attribute++)
                {
                    double moved = weightedSum[attribute] / weightTotal;
                    if (Math.Abs(moved - prototypes[cluster][attribute]) > tolerance) converged = false;
                    prototypes[cluster][attribute] = moved;
                }
            }
        }
        UpdateMembership(data, prototypes, supervised, given, ratio, membership, squaredDistance);

        var assignment = new int[objectCount];
        for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
        {
            int best = 0;
            for (int cluster = 1; cluster < clusterCount; cluster++)
                if (membership[objectIndex, cluster] > membership[objectIndex, best]) best = cluster;
            assignment[objectIndex] = best;
        }
        return new SupervisedPartition(membership, prototypes, assignment, iteration, converged);
    }

    /// <summary>The membership update of the ssfcm-algorithm for fixed prototypes.</summary>
    private static void UpdateMembership(RawDataMatrix data, double[][] prototypes, bool[] supervised, double[,] given,
                                         double ratio, double[,] membership, double[] squaredDistance)
    {
        int clusterCount = prototypes.Length;
        for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
        {
            var row = data.GetRow(objectIndex);
            int coincident = 0;
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                double sum = 0.0;
                for (int attribute = 0; attribute < row.Length; attribute++)
                {
                    double difference = row[attribute] - prototypes[cluster][attribute];
                    sum += difference * difference;
                }
                squaredDistance[cluster] = sum;
                if (sum == 0.0) coincident++;
            }

            double givenTotal = 0.0;
            if (supervised[objectIndex])
                for (int cluster = 0; cluster < clusterCount; cluster++) givenTotal += given[objectIndex, cluster];
            double free = 1.0 + ratio * (1.0 - givenTotal);

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                // the unsupervised share: 1 / Σ_k d²_l / d²_k, or an equal split among coincident prototypes
                double share;
                if (coincident > 0)
                    share = squaredDistance[cluster] == 0.0 ? 1.0 / coincident : 0.0;
                else
                {
                    double denominator = 0.0;
                    for (int other = 0; other < clusterCount; other++)
                        denominator += squaredDistance[cluster] / squaredDistance[other];
                    share = 1.0 / denominator;
                }
                double target = supervised[objectIndex] ? given[objectIndex, cluster] : 0.0;
                membership[objectIndex, cluster] = (free * share + ratio * target) / (1.0 + ratio);
            }
        }
    }
}
