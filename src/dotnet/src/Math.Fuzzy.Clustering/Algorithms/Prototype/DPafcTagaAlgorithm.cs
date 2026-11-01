using MathFuzzy.Clustering.Algorithms.Relational;
using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.AlphaSelection;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-PAFC-TAGA — Principal Allotment AFC with TAGA aggregation.
///
/// Same as D-AFC-TAGA but explicitly returns all principal allotments
/// (not just the best by geometric deviation). The full ranked list
/// allows the researcher to inspect all valid structures.
/// </summary>
public sealed class DPafcTagaAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TAGAProcessor          taga,
    ICriterion             criterion,
    int                    minClusterPower  = 1,
    int                    maxClusterPower  = 0,
    int                    maxIntersections = DAfcOptions.AutoIntersections) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
    {
        var Tp = PrototypePipeline.BuildWithTaga(data, toleranceBuilder, taga);

        var dpafc = new DPafcAlgorithm(
            AllDistinctAlphaSelector.Instance,
            criterion,
            minClusterPower,
            maxClusterPower,
            maxIntersections);

        return dpafc.Run(Tp, ct);
    }
}
