using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.DimensionReduction;

/// <summary>
/// D-PAFC-DR — Dimensionality reduction via D-PAFC on the attribute space.
///
/// Treats each attribute (column) as an "object" and applies D-PAFC
/// to find groups of similar attributes. One representative is selected
/// per cluster (the attribute with highest average similarity to others in the cluster).
/// Original data is then projected onto the selected attributes.
/// </summary>
public sealed class DPafcDrReducer(
    ToleranceMatrixBuilder attributeToleranceBuilder,
    ICriterion             criterion,
    int                    minClusterPower = 1) : IDimensionReducer
{
    private int[] _selectedIndices = [];
    public IReadOnlyList<int> SelectedAttributeIndices => _selectedIndices;

    public RawDataMatrix Reduce(RawDataMatrix data, CancellationToken ct = default)
    {
        // Treat attributes as objects: transpose → each row = one attribute
        var attributeData = data.Transpose();

        // Build tolerance matrix over attributes
        var T = attributeToleranceBuilder.Build(attributeData);

        // D-PAFC on attribute space
        var dpafc = new DPafcAlgorithm(
            AllDistinctAlphaSelector.Instance,
            criterion,
            minClusterPower);

        var results = dpafc.Run(T, ct);
        if (results.Count == 0)
        {
            // No reduction possible — return all attributes
            _selectedIndices = Enumerable.Range(0, data.Cols).ToArray();
            return data;
        }

        var best = results[0];
        _selectedIndices = SelectRepresentatives(T, best.MemberSets);

        return data.SelectColumns(_selectedIndices);
    }

    /// <summary>
    /// From each cluster of attributes, picks the attribute with the
    /// highest total similarity to all other attributes in its cluster.
    /// </summary>
    private static int[] SelectRepresentatives(FuzzyToleranceMatrix T, int[][] clusterSets)
    {
        var selected = new int[clusterSets.Length];
        for (int l = 0; l < clusterSets.Length; l++)
        {
            var members = clusterSets[l];
            if (members.Length == 1)
            {
                selected[l] = members[0];
                continue;
            }

            int bestAttr = members[0];
            double bestScore = double.MinValue;
            foreach (var attr in members)
            {
                double score = members
                    .Where(other => other != attr)
                    .Sum(other => T[attr, other]);
                if (score > bestScore) { bestScore = score; bestAttr = attr; }
            }
            selected[l] = bestAttr;
        }
        return selected;
    }
}
