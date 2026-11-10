using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Applications.LargeData;

/// <summary>
/// Hard partition of object data by the k-means method (Lloyd's iteration with k-means++ seeding).
/// It serves as the "clustering procedure" of the first step of the reduction of large data sets
/// (Viattchenin 2013, "A new approach to reduction of large data set", CDAM'2013), whose example
/// uses k-means; the source states neither the initialisation nor the distance, so this
/// implementation fixes them: squared Euclidean distance on the data as given, k-means++ seeding
/// from a seeded pseudo-random sequence, several restarts, and the partition with the smallest
/// within-class sum of squares.
///
/// The procedure is deterministic: the same data, class count, seed and restart count always give
/// the same partition (no threads are used; ties in the assignment go to the class with the
/// smaller index; equal sums of squares keep the earlier restart).
/// </summary>
public static class KMeansPartition
{
    /// <summary>
    /// Partitions the objects (rows) of <paramref name="data"/> into at most
    /// <paramref name="classCount"/> non-empty classes. Returns the class index of every object;
    /// the classes are numbered in the order of their first object (object 0 is in class 0), so
    /// equal partitions are returned as equal arrays. Fewer classes are returned only when the
    /// data contain fewer distinct objects than <paramref name="classCount"/>.
    /// </summary>
    /// <param name="data">Object data, one row per object.</param>
    /// <param name="classCount">Number of classes k, 1 ≤ k ≤ n.</param>
    /// <param name="restarts">Number of independent seedings; the best partition is kept.</param>
    /// <param name="seed">Seed of the pseudo-random sequence used by the k-means++ seeding.</param>
    /// <param name="maxIterations">Largest number of Lloyd iterations of one restart.</param>
    public static int[] Partition(RawDataMatrix data, int classCount, int restarts = 10, int seed = 0, int maxIterations = 300)
    {
        int objectCount = data.Rows;
        if (objectCount == 0) return [];
        if (classCount < 1 || classCount > objectCount)
            throw new ArgumentOutOfRangeException(nameof(classCount), $"The number of classes must lie in [1, {objectCount}].");
        if (restarts < 1) throw new ArgumentOutOfRangeException(nameof(restarts), "At least one restart is needed.");
        if (maxIterations < 1) throw new ArgumentOutOfRangeException(nameof(maxIterations), "At least one iteration is needed.");

        var random = new Random(seed);
        int[]? bestLabels = null;
        double bestSumOfSquares = double.PositiveInfinity;
        for (int restart = 0; restart < restarts; restart++)
        {
            var centres = SeedCentres(data, classCount, random);
            var labels = Iterate(data, centres, maxIterations);
            double sumOfSquares = WithinClassSumOfSquares(data, labels, centres);
            if (sumOfSquares < bestSumOfSquares)
            {
                bestSumOfSquares = sumOfSquares;
                bestLabels = labels;
            }
        }
        return Canonical(bestLabels!);
    }

    /// <summary>k-means++ seeding: each further centre is drawn with probability proportional to D².</summary>
    private static double[][] SeedCentres(RawDataMatrix data, int classCount, Random random)
    {
        int objectCount = data.Rows;
        var chosen = new List<int> { random.Next(objectCount) };
        var nearest = new double[objectCount];
        for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            nearest[objectIndex] = SquaredDistance(data.GetRow(objectIndex), data.GetRow(chosen[0]));

        while (chosen.Count < classCount)
        {
            double total = nearest.Sum();
            int next = -1;
            if (total > 0.0)
            {
                double target = random.NextDouble() * total;
                double accumulated = 0.0;
                for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
                {
                    if (nearest[objectIndex] <= 0.0) continue;
                    accumulated += nearest[objectIndex];
                    next = objectIndex;
                    if (accumulated > target) break;
                }
            }
            if (next < 0)   // every object coincides with a centre: take the first object not chosen
                next = Enumerable.Range(0, objectCount).First(objectIndex => !chosen.Contains(objectIndex));
            chosen.Add(next);
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
                nearest[objectIndex] = Math.Min(nearest[objectIndex], SquaredDistance(data.GetRow(objectIndex), data.GetRow(next)));
        }
        return [.. chosen.Select(objectIndex => data.GetRow(objectIndex).ToArray())];
    }

    private static int[] Iterate(RawDataMatrix data, double[][] centres, int maxIterations)
    {
        int objectCount = data.Rows, attributeCount = data.Cols, classCount = centres.Length;
        var labels = new int[objectCount];
        Array.Fill(labels, -1);
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool changed = false;
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                int label = NearestCentre(data.GetRow(objectIndex), centres);
                if (label != labels[objectIndex]) { labels[objectIndex] = label; changed = true; }
            }

            // An empty class takes the object farthest from its own centre (ties: smaller index),
            // provided that object does not leave its class empty.
            var sizes = new int[classCount];
            foreach (int label in labels) sizes[label]++;
            for (int classIndex = 0; classIndex < classCount; classIndex++)
            {
                if (sizes[classIndex] > 0) continue;
                int farthest = -1;
                double largest = 0.0;
                for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
                {
                    if (sizes[labels[objectIndex]] < 2) continue;
                    double distance = SquaredDistance(data.GetRow(objectIndex), centres[labels[objectIndex]]);
                    if (distance > largest) { largest = distance; farthest = objectIndex; }
                }
                if (farthest < 0) continue;   // all objects coincide with their centres
                sizes[labels[farthest]]--;
                labels[farthest] = classIndex;
                sizes[classIndex] = 1;
                changed = true;
            }

            if (!changed) break;

            for (int classIndex = 0; classIndex < classCount; classIndex++)
            {
                if (sizes[classIndex] == 0) continue;
                Array.Clear(centres[classIndex]);
            }
            for (int objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                var row = data.GetRow(objectIndex);
                var centre = centres[labels[objectIndex]];
                for (int attribute = 0; attribute < attributeCount; attribute++)
                    centre[attribute] += row[attribute];
            }
            for (int classIndex = 0; classIndex < classCount; classIndex++)
            {
                if (sizes[classIndex] == 0) continue;
                for (int attribute = 0; attribute < attributeCount; attribute++)
                    centres[classIndex][attribute] /= sizes[classIndex];
            }
        }
        return labels;
    }

    private static int NearestCentre(ReadOnlySpan<double> row, double[][] centres)
    {
        int best = 0;
        double bestDistance = SquaredDistance(row, centres[0]);
        for (int classIndex = 1; classIndex < centres.Length; classIndex++)
        {
            double distance = SquaredDistance(row, centres[classIndex]);
            if (distance < bestDistance) { bestDistance = distance; best = classIndex; }
        }
        return best;
    }

    private static double WithinClassSumOfSquares(RawDataMatrix data, int[] labels, double[][] centres)
    {
        double total = 0.0;
        for (int objectIndex = 0; objectIndex < data.Rows; objectIndex++)
            total += SquaredDistance(data.GetRow(objectIndex), centres[labels[objectIndex]]);
        return total;
    }

    private static double SquaredDistance(ReadOnlySpan<double> first, ReadOnlySpan<double> second)
    {
        double total = 0.0;
        for (int attribute = 0; attribute < first.Length; attribute++)
        {
            double difference = first[attribute] - second[attribute];
            total += difference * difference;
        }
        return total;
    }

    /// <summary>Renumbers the classes in the order of their first object and drops empty ones.</summary>
    internal static int[] Canonical(int[] labels)
    {
        var renumbering = new Dictionary<int, int>();
        var result = new int[labels.Length];
        for (int objectIndex = 0; objectIndex < labels.Length; objectIndex++)
        {
            if (!renumbering.TryGetValue(labels[objectIndex], out int number))
                renumbering[labels[objectIndex]] = number = renumbering.Count;
            result[objectIndex] = number;
        }
        return result;
    }
}
