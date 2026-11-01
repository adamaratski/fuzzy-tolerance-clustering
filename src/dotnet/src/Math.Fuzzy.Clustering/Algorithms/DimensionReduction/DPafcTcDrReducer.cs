using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.DimensionReduction;

/// <summary>
/// D-PAFC-TC-DR — Dimensionality reduction using D-PAFC with transitive closure
/// applied to the attribute space.
/// </summary>
public sealed class DPafcTcDrReducer(
    ToleranceMatrixBuilder  attributeToleranceBuilder,
    MaxMinTransitiveClosure closure,
    ICriterion              criterion,
    int                     minClusterPower = 1) : IDimensionReducer
{
    private int[] _selectedIndices = [];
    public IReadOnlyList<int> SelectedAttributeIndices => _selectedIndices;

    public RawDataMatrix Reduce(RawDataMatrix data, CancellationToken ct = default)
    {
        var attributeData = data.Transpose();

        var T  = attributeToleranceBuilder.Build(attributeData);
        var Tt = closure.Compute(T);

        var dpafc = new DPafcAlgorithm(
            AllDistinctAlphaSelector.Instance,
            criterion,
            minClusterPower);

        var results = dpafc.Run(Tt, ct);
        if (results.Count == 0)
        {
            _selectedIndices = Enumerable.Range(0, data.Cols).ToArray();
            return data;
        }

        var best = results[0];
        _selectedIndices = SelectRepresentatives(Tt, best.MemberSets);
        return data.SelectColumns(_selectedIndices);
    }

    private static int[] SelectRepresentatives(FuzzyToleranceMatrix T, int[][] clusterSets)
    {
        var selected = new int[clusterSets.Length];
        for (int l = 0; l < clusterSets.Length; l++)
        {
            var members = clusterSets[l];
            if (members.Length == 1) { selected[l] = members[0]; continue; }

            int best = members[0];
            double bestScore = double.MinValue;
            foreach (var attr in members)
            {
                double score = members.Where(o => o != attr).Sum(o => T[attr, o]);
                if (score > bestScore) { bestScore = score; best = attr; }
            }
            selected[l] = best;
        }
        return selected;
    }
}
