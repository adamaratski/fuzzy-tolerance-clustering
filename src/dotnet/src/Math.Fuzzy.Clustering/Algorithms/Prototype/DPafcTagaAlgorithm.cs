using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Aggregation.Taga;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-PAFC-TAGA — the principal allotment with a transitive approximation
/// (Viattchenin and Damaratski 2013): the rule of D-PAFC-TC applied to T^φ, i.e. the
/// allotments of the lowest level of T^φ that has at least two equivalence classes.
/// </summary>
public sealed class DPafcTagaAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TAGAProcessor          taga,
    ICriterion             criterion,
    int                    minClusterPower = 1,
    int                    maxClusterPower = 0,
    int                    maxResults      = 200) : IPrototypeClusterer
{
    public IReadOnlyList<ClusterResult> Run(RawDataMatrix data, CancellationToken ct = default)
        => Run(toleranceBuilder.Build(data), toleranceBuilder.Normalize(data), ct);

    /// <summary>Runs the algorithm on a given tolerance matrix (see <see cref="DAfcTcAlgorithm.Run(FuzzyToleranceMatrix, RawDataMatrix?, CancellationToken)"/>).</summary>
    public IReadOnlyList<ClusterResult> Run(
        FuzzyToleranceMatrix tolerance, RawDataMatrix? normalizedData, CancellationToken ct = default)
        => DPafcTcAlgorithm.FirstAdmissibleLevel(taga.Apply(tolerance), criterion, minClusterPower, maxClusterPower,
                                                 normalizedData, maxResults, ct);
}
