using MathFuzzy.Clustering.Results;
using MathFuzzy.Core.Criteria;
using MathFuzzy.Core.Matrix;

namespace MathFuzzy.Clustering.Algorithms.Prototype;

/// <summary>
/// D-PAFC-TO — the principal allotment with a transitive opening (Viattchenin and Tati 2013,
/// sec. 2.4): the rule of D-PAFC-TC applied to T̆, i.e. the allotments of the lowest level of the
/// opening that has at least two equivalence classes (and satisfies the power bounds).
/// </summary>
public sealed class DPafcToAlgorithm(
    ToleranceMatrixBuilder toleranceBuilder,
    TransitiveOpening      opening,
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
        => DPafcTcAlgorithm.FirstAdmissibleLevel(opening.Compute(tolerance), criterion, minClusterPower, maxClusterPower,
                                                 normalizedData, maxResults, ct);

    /// <summary>Exposes the transitive opening of the data for inspection (e.g. UI).</summary>
    public FuzzyToleranceMatrix BuildOpeningMatrix(RawDataMatrix data)
        => opening.Compute(toleranceBuilder.Build(data));
}
