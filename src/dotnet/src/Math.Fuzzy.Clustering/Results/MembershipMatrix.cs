using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Results;

/// <summary>Membership matrix of an allotment, shared by the D-AFC family of algorithms.</summary>
internal static class MembershipMatrix
{
    /// <summary>
    /// membership[objectIndex, clusterIndex] = T(center, objectIndex) when the object
    /// belongs to the cluster, otherwise 0. Uses a merge scan (no HashSet), which is
    /// valid because member sets are ascending.
    /// </summary>
    internal static double[,] Build(
        FuzzyToleranceMatrix matrix,
        int[] centers,
        int[][] memberSets,
        int objectCount)
    {
        var membership = new double[objectCount, centers.Length];

        for (int clusterIndex = 0; clusterIndex < centers.Length; clusterIndex++)
        {
            var similarities = matrix.GetRow(centers[clusterIndex]);
            var members = memberSets[clusterIndex];

            int memberPosition = 0;
            for (int objectIndex = 0; objectIndex < objectCount && memberPosition < members.Length; objectIndex++)
            {
                if (members[memberPosition] == objectIndex)
                {
                    membership[objectIndex, clusterIndex] = similarities[objectIndex];
                    memberPosition++;
                }
            }
        }

        return membership;
    }
}
