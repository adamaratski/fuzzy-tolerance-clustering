using MathFuzzy.Clustering.Results;

namespace MathFuzzy.Clustering.Algorithms.Supervision;

/// <summary>
/// The condition that an allotment is consistent with the labelled objects: there is a
/// one-to-one assignment of the classes of the labelled objects to clusters such that every
/// labelled object belongs to the cluster of its class with the prescribed degrees
/// (Viattchenin 2013, sec. 2.2.2, Step 5; Kacprzyk et al. 2017, Step 10). The class numbers
/// are only names: which cluster carries which class is decided by the allotment.
/// </summary>
public static class LabelConstraint
{
    /// <summary>Degrees that differ by rounding noise are equal.</summary>
    private const double Tolerance = 1e-12;

    /// <summary>The labelled objects grouped by class, in the order of first appearance.</summary>
    internal sealed record LabelClasses(int[][] Members, int[] ObjectIndices)
    {
        public int Count => Members.Length;
    }

    /// <summary>
    /// Checks the object numbers (in range, distinct) and groups the labelled objects into classes;
    /// a labelled object without a class forms a class of its own.
    /// </summary>
    internal static LabelClasses Group(IReadOnlyList<int> objectIndices, IReadOnlyList<int?> classes,
                                       int clusterCount, int objectCount)
    {
        if (objectIndices.Count == 0)
            throw new ArgumentException("Partial supervision needs at least one labelled object.");

        var seen = new HashSet<int>();
        var groups = new List<List<int>>();
        var groupOfClass = new Dictionary<int, int>();
        for (int label = 0; label < objectIndices.Count; label++)
        {
            int objectIndex = objectIndices[label];
            if (objectIndex < 0 || objectIndex >= objectCount)
                throw new ArgumentException(
                    $"Labelled object number {objectIndex + 1} lies outside the set of {objectCount} objects.");
            if (!seen.Add(objectIndex))
                throw new ArgumentException($"Object x{objectIndex + 1} is labelled more than once.");

            if (classes[label] is int className && groupOfClass.TryGetValue(className, out int group))
            {
                groups[group].Add(label);
                continue;
            }
            if (classes[label] is int newClass) groupOfClass[newClass] = groups.Count;
            groups.Add([label]);
        }

        if (groups.Count > clusterCount)
            throw new ArgumentException(
                $"The labelled objects form {groups.Count} classes, more than the number of clusters c = {clusterCount}.");
        return new LabelClasses([.. groups.Select(group => group.ToArray())], [.. objectIndices]);
    }

    /// <summary>
    /// A one-to-one assignment of classes to clusters under which every class fits its cluster,
    /// as the cluster number of each class; null when there is none. Depth-first search over the
    /// classes; the first assignment in lexicographic order is returned.
    /// </summary>
    internal static int[]? Assign(int classCount, int clusterCount, Func<int, int, bool> classFitsCluster)
    {
        var assignment = new int[classCount];
        var used = new bool[clusterCount];

        bool Extend(int classIndex)
        {
            if (classIndex == classCount) return true;
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (used[cluster] || !classFitsCluster(classIndex, cluster)) continue;
                used[cluster] = true;
                assignment[classIndex] = cluster;
                if (Extend(classIndex + 1)) return true;
                used[cluster] = false;
            }
            return false;
        }

        return Extend(0) ? assignment : null;
    }

    /// <summary>True when the degree reaches the least admissible value (up to rounding noise).</summary>
    internal static bool AtLeast(double degree, double bound) => degree >= bound - Tolerance;

    /// <summary>True when the degree does not exceed the largest admissible value (up to rounding noise).</summary>
    internal static bool AtMost(double degree, double bound) => degree <= bound + Tolerance;

    /// <summary>
    /// The cluster of each class of the labelled objects (classes in the order of their first
    /// appearance in <paramref name="labels"/>) in an allotment of D-AFC-PS(c), or null when the
    /// allotment is not consistent with the labels.
    /// </summary>
    public static int[]? AssignClasses(ClusterResult result, IReadOnlyList<LabelledObject> labels)
    {
        var classes = Group([.. labels.Select(label => label.ObjectIndex)], [.. labels.Select(label => label.Class)],
                            result.ClusterCount, result.Membership.GetLength(0));
        return Assign(classes.Count, result.ClusterCount, (classIndex, cluster) =>
            classes.Members[classIndex].All(label =>
            {
                double membership = result.Membership[labels[label].ObjectIndex, cluster];
                return membership > 0.0 && AtLeast(membership, labels[label].Membership);
            }));
    }

    /// <summary>
    /// The cluster of each class of the labelled objects in an allotment of D-AIFC-PS(c), or null
    /// when the allotment is not consistent with the labels. Membership is in the carrier.
    /// </summary>
    public static int[]? AssignClasses(ClusterResult result, IReadOnlyList<IntuitionisticLabelledObject> labels)
    {
        var nonMembership = result.NonMembership
            ?? throw new ArgumentException("The result carries no non-membership degrees; it is not intuitionistic.");
        var classes = Group([.. labels.Select(label => label.ObjectIndex)], [.. labels.Select(label => label.Class)],
                            result.ClusterCount, result.Membership.GetLength(0));
        return Assign(classes.Count, result.ClusterCount, (classIndex, cluster) =>
            classes.Members[classIndex].All(label =>
            {
                var labelled = labels[label];
                return result.MemberSets[cluster].Contains(labelled.ObjectIndex)
                       && AtLeast(result.Membership[labelled.ObjectIndex, cluster], labelled.Membership)
                       && AtMost(nonMembership[labelled.ObjectIndex, cluster], labelled.NonMembership);
            }));
    }
}
