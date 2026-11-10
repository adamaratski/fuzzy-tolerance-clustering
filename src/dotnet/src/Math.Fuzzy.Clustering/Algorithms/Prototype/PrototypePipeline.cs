using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// Equivalence classes of the levels of a fuzzy equivalence (a transitive closure or a
/// transitive approximation), shared by the prototype-based and hierarchical algorithms.
/// </summary>
internal static class PrototypePipeline
{
    /// <summary>
    /// Number of equivalence classes of a max-min TRANSITIVE matrix at level alpha.
    /// For a transitive closure T̃ the α-cut relation is an equivalence, so the
    /// α-cut of any object is exactly its class: one row sweep per class suffices.
    /// Valid allotments at this alpha exist only for c = class count, which lets
    /// callers skip the brute-force c = 2..n loop entirely.
    /// </summary>
    internal static int CountAlphaClasses(FuzzyToleranceMatrix Tt, double alpha)
    {
        int n = Tt.Size;
        var assigned = new bool[n];
        int classes = 0;

        for (int i = 0; i < n; i++)
        {
            if (assigned[i]) continue;
            classes++;
            var row = Tt.GetRow(i);
            for (int j = i; j < n; j++)
                if (row[j] >= alpha) assigned[j] = true;
        }

        return classes;
    }

    /// <summary>
    /// Equivalence classes of a max-min TRANSITIVE matrix at level alpha, each as the
    /// ascending α-cut of its smallest member, ordered by that member. On T̃ the α-cut
    /// of every object of a class is the class itself, so these are exactly the member
    /// sets D-AFC would build for any choice of typical points.
    /// </summary>
    internal static List<int[]> AlphaClasses(FuzzyToleranceMatrix closureMatrix, double alpha)
    {
        int objectCount = closureMatrix.Size;
        var assigned = new bool[objectCount];
        var classes = new List<int[]>();

        for (int firstMember = 0; firstMember < objectCount; firstMember++)
        {
            if (assigned[firstMember]) continue;
            var similarities = closureMatrix.GetRow(firstMember);
            var members = new List<int>();
            for (int candidate = 0; candidate < objectCount; candidate++)
            {
                if (similarities[candidate] < alpha) continue;
                members.Add(candidate);
                assigned[candidate] = true;
            }
            classes.Add([.. members]);
        }

        return classes;
    }
}
