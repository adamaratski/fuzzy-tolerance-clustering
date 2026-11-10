namespace MathFuzzy.Core.Matrix;

/// <summary>The rule by which <see cref="TransitiveOpening"/> chooses the next two groups to merge.</summary>
public enum TransitiveOpeningMethod
{
    /// <summary>
    /// The merge order of complete linkage: the two groups whose least similarity is largest are
    /// merged next. This is the construction that reproduces the published example of the
    /// opening-based algorithms (Viattchenin and Tati 2013, sec. 3); on ties the last maximal pair
    /// of groups in the scan is merged.
    /// </summary>
    CompleteLinkage,

    /// <summary>
    /// The merge order of the algorithm of Garmendia and Salvador (2005, sec. 5): the pairs of
    /// objects are taken in decreasing order of similarity (equal values by row, then column),
    /// which merges groups in the order of single linkage. The bridging value is bounded by the
    /// least value inside both groups, as in Garmendia et al. (2009, sec. 3); without this bound
    /// the printed algorithm can return a relation that is not transitive. The result is a fuzzy
    /// equivalence below T that need not be maximal.
    /// </summary>
    SingleLinkageOrder
}

/// <summary>One merge of the construction: two groups (0-based object indices, ascending) and the bridging value.</summary>
public sealed record OpeningMerge(int[] FirstGroup, int[] SecondGroup, double Value);

/// <summary>A transitive opening together with the merges that built it, in the order they were made.</summary>
public sealed record TransitiveOpeningResult(FuzzyToleranceMatrix Opening, IReadOnlyList<OpeningMerge> Merges);

/// <summary>
/// Computes a transitive opening of a fuzzy tolerance T: a fuzzy equivalence (reflexive,
/// symmetric, max-min transitive) contained in T (Garmendia and Salvador 2005, def. 5;
/// Viattchenin and Tati 2013, sec. 2.1). Unlike the closure, an opening is not unique, so the
/// construction is part of the definition of the opening-based algorithms.
///
/// Both methods start from the n singleton groups and merge two groups at a time until one group
/// remains; every pair of objects that is joined by a merge receives the bridging value
///   min( min{T(x, y) : x ∈ P, y ∈ Q}, least value inside P, least value inside Q ),
/// which keeps the relation below T and, by the bridging lemma of Garmendia and Salvador
/// (2005, lemma 3.1), transitive. With <see cref="TransitiveOpeningMethod.CompleteLinkage"/> the
/// bound by the group-internal values is never active, because complete linkage has no inversions.
///
/// Values of T that differ by no more than the tie tolerance are first merged into one level
/// (<see cref="FuzzyToleranceMatrix.SnapLevels"/>, the smallest value represents the level), so
/// that pairs that are equally similar in exact arithmetic compete as ties. On a relation that is
/// already a fuzzy equivalence the opening is the relation itself.
/// </summary>
/// <param name="method">The merge order.</param>
/// <param name="tieTolerance">Values of T that differ by no more than this are one level; 0 compares exactly.</param>
public sealed class TransitiveOpening(
    TransitiveOpeningMethod method       = TransitiveOpeningMethod.CompleteLinkage,
    double                  tieTolerance = FuzzyToleranceMatrix.DefaultLevelTolerance)
{
    /// <summary>The merge order of this construction.</summary>
    public TransitiveOpeningMethod Method { get; } = method;

    /// <summary>The transitive opening of <paramref name="tolerance"/>.</summary>
    public FuzzyToleranceMatrix Compute(FuzzyToleranceMatrix tolerance) => ComputeWithMerges(tolerance).Opening;

    /// <summary>The transitive opening of <paramref name="tolerance"/> and the sequence of its merges.</summary>
    public TransitiveOpeningResult ComputeWithMerges(FuzzyToleranceMatrix tolerance)
    {
        var working = tolerance.Clone();
        working.SnapLevels(tieTolerance);

        var builder = new OpeningBuilder(working);
        if (Method == TransitiveOpeningMethod.CompleteLinkage) builder.MergeByCompleteLinkage();
        else builder.MergeBySingleLinkageOrder();
        return new TransitiveOpeningResult(builder.Opening, builder.Merges);
    }

    /// <summary>The groups formed so far, their least internal values and the opening being filled.</summary>
    private sealed class OpeningBuilder
    {
        private readonly FuzzyToleranceMatrix tolerance;
        private readonly int size;

        /// <summary>Members of the group stored in a slot; a slot is emptied when its group is absorbed.</summary>
        private readonly List<int>[] groupMembers;

        /// <summary>Least value of the opening inside the group of a slot (1 for a singleton).</summary>
        private readonly double[] leastInternalValue;

        /// <summary>Slot of the group that currently contains each object.</summary>
        private readonly int[] groupOfObject;

        public FuzzyToleranceMatrix Opening { get; }
        public List<OpeningMerge> Merges { get; } = [];

        public OpeningBuilder(FuzzyToleranceMatrix tolerance)
        {
            this.tolerance = tolerance;
            size = tolerance.Size;
            Opening = new FuzzyToleranceMatrix(size);
            groupMembers = new List<int>[size];
            leastInternalValue = new double[size];
            groupOfObject = new int[size];
            for (int objectIndex = 0; objectIndex < size; objectIndex++)
            {
                groupMembers[objectIndex] = [objectIndex];
                leastInternalValue[objectIndex] = 1.0;
                groupOfObject[objectIndex] = objectIndex;
            }
        }

        /// <summary>
        /// Complete linkage: the link of two groups is their least cross similarity, updated after
        /// a merge by link(P ∪ Q, R) = min(link(P, R), link(Q, R)). The active groups are kept in
        /// the order of their creation, a merged group taking the position of the first of the two;
        /// among the pairs with the largest link the last one in the scan is merged.
        /// </summary>
        public void MergeByCompleteLinkage()
        {
            var link = new double[size, size];
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                    link[row, column] = tolerance[row, column];

            var activeSlots = Enumerable.Range(0, size).ToList();
            while (activeSlots.Count > 1)
            {
                double largestLink = double.NegativeInfinity;
                int firstPosition = -1, secondPosition = -1;
                for (int first = 0; first < activeSlots.Count; first++)
                    for (int second = first + 1; second < activeSlots.Count; second++)
                    {
                        double value = link[activeSlots[first], activeSlots[second]];
                        if (value < largestLink) continue;   // equal values: the later pair wins
                        largestLink = value;
                        firstPosition = first;
                        secondPosition = second;
                    }

                int keptSlot = activeSlots[firstPosition], absorbedSlot = activeSlots[secondPosition];
                Merge(keptSlot, absorbedSlot, largestLink);
                activeSlots.RemoveAt(secondPosition);
                foreach (int otherSlot in activeSlots)
                {
                    if (otherSlot == keptSlot) continue;
                    double combined = Math.Min(link[keptSlot, otherSlot], link[absorbedSlot, otherSlot]);
                    link[keptSlot, otherSlot] = combined;
                    link[otherSlot, keptSlot] = combined;
                }
            }
        }

        /// <summary>
        /// The order of Garmendia and Salvador (2005, sec. 5): the pairs of objects by decreasing
        /// similarity, equal values by row and then by column; a pair whose objects lie in
        /// different groups merges the two groups.
        /// </summary>
        public void MergeBySingleLinkageOrder()
        {
            var pairs = new List<(int Row, int Column)>(size * (size - 1) / 2);
            for (int row = 0; row < size; row++)
                for (int column = row + 1; column < size; column++)
                    pairs.Add((row, column));
            pairs.Sort((first, second) =>
            {
                int order = tolerance[second.Row, second.Column].CompareTo(tolerance[first.Row, first.Column]);
                if (order != 0) return order;
                order = first.Row.CompareTo(second.Row);
                return order != 0 ? order : first.Column.CompareTo(second.Column);
            });

            foreach (var (row, column) in pairs)
            {
                int rowGroup = groupOfObject[row], columnGroup = groupOfObject[column];
                if (rowGroup == columnGroup) continue;

                double leastCross = double.PositiveInfinity;
                foreach (int firstMember in groupMembers[rowGroup])
                    foreach (int secondMember in groupMembers[columnGroup])
                        leastCross = Math.Min(leastCross, tolerance[firstMember, secondMember]);
                Merge(rowGroup, columnGroup, leastCross);
                if (Merges.Count == size - 1) break;
            }
        }

        /// <summary>
        /// Joins the group of <paramref name="absorbedSlot"/> to that of <paramref name="keptSlot"/>
        /// with the bridging value bounded by the least values inside both groups.
        /// </summary>
        private void Merge(int keptSlot, int absorbedSlot, double crossValue)
        {
            double bridge = Math.Min(crossValue, Math.Min(leastInternalValue[keptSlot], leastInternalValue[absorbedSlot]));
            var keptMembers = groupMembers[keptSlot];
            var absorbedMembers = groupMembers[absorbedSlot];
            foreach (int firstMember in keptMembers)
                foreach (int secondMember in absorbedMembers)
                    Opening[firstMember, secondMember] = bridge;   // the setter keeps the matrix symmetric

            Merges.Add(new OpeningMerge([.. keptMembers.Order()], [.. absorbedMembers.Order()], bridge));

            foreach (int member in absorbedMembers) groupOfObject[member] = keptSlot;
            keptMembers.AddRange(absorbedMembers);
            absorbedMembers.Clear();
            leastInternalValue[keptSlot] = bridge;
        }
    }
}
