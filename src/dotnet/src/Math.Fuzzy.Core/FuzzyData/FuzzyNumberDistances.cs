namespace MathFuzzy.Core.FuzzyData;

/// <summary>
/// The kind of fuzzy values that describe the attributes of an object (Viattchenin 2013,
/// sec. 3.2.1). All attributes of an object are of the same kind (def. 3.15).
/// </summary>
public enum FuzzyNumberKind
{
    /// <summary>
    /// LR-type fuzzy number V = (m, a, b)_LR with modal value m and left and right spreads a, b;
    /// with the shape integrals l = r = 1/2 it is a triangular fuzzy number.
    /// </summary>
    FuzzyNumber,

    /// <summary>
    /// LR-type fuzzy interval V = (m̲, m̄, a, b)_LR whose core is [m̲, m̄]; with l = r = 1/2 it is
    /// a trapezoidal fuzzy interval.
    /// </summary>
    FuzzyInterval,

    /// <summary>Gaussian fuzzy number V = (m, σ)_G with mean m and width σ.</summary>
    Gaussian,
}

/// <summary>
/// Squared distances between two fuzzy numbers of the same kind (Viattchenin 2013,
/// eqs. (1.107), (3.37)–(3.40); Yang and Ko 1996). The shape integrals
/// l = ∫₀¹ L⁻¹(ω) dω and r = ∫₀¹ R⁻¹(ω) dω of the shape functions L and R are 1/2 for the
/// linear shape L(u) = R(u) = max(0, 1 − u), i.e. for triangular numbers and trapezoidal intervals.
/// </summary>
public static class FuzzyNumberDistances
{
    /// <summary>Shape integral of the linear shape function max(0, 1 − u): triangular and trapezoidal values.</summary>
    public const double LinearShapeIntegral = 0.5;

    /// <summary>
    /// Squared distance of Yang and Ko between two triangular fuzzy numbers (m, a, b)
    /// (Viattchenin 2013, eq. (1.107)):
    /// d² = (m_i − m_j)² + [(m_i − m_j) − ½(a_i − a_j)]² + [(m_i − m_j) + ½(b_i − b_j)]².
    /// </summary>
    public static double TriangularSquared(
        double modalFirst, double leftSpreadFirst, double rightSpreadFirst,
        double modalSecond, double leftSpreadSecond, double rightSpreadSecond)
        => LeftRightSquared(modalFirst, leftSpreadFirst, rightSpreadFirst,
                            modalSecond, leftSpreadSecond, rightSpreadSecond,
                            LinearShapeIntegral, LinearShapeIntegral);

    /// <summary>
    /// Squared distance between two LR-type fuzzy numbers (m, a, b)_LR (Viattchenin 2013, eq. (3.39)):
    /// d² = (m_i − m_j)² + [(m_i − l·a_i) − (m_j − l·a_j)]² + [(m_i + r·b_i) − (m_j + r·b_j)]².
    /// </summary>
    public static double LeftRightSquared(
        double modalFirst, double leftSpreadFirst, double rightSpreadFirst,
        double modalSecond, double leftSpreadSecond, double rightSpreadSecond,
        double leftShapeIntegral, double rightShapeIntegral)
    {
        double modalDifference = modalFirst - modalSecond;
        double leftDifference = (modalFirst - leftShapeIntegral * leftSpreadFirst)
                              - (modalSecond - leftShapeIntegral * leftSpreadSecond);
        double rightDifference = (modalFirst + rightShapeIntegral * rightSpreadFirst)
                               - (modalSecond + rightShapeIntegral * rightSpreadSecond);
        return modalDifference * modalDifference + leftDifference * leftDifference + rightDifference * rightDifference;
    }

    /// <summary>
    /// Squared distance between two LR-type fuzzy intervals (m̲, m̄, a, b)_LR (Viattchenin 2013,
    /// eq. (3.37); trapezoidal intervals with l = r = 1/2, eq. (3.38)):
    /// d² = (m̲_i − m̲_j)² + (m̄_i − m̄_j)² + [(m̲_i − l·a_i) − (m̲_j − l·a_j)]² + [(m̄_i + r·b_i) − (m̄_j + r·b_j)]².
    /// </summary>
    public static double LeftRightIntervalSquared(
        double lowerModalFirst, double upperModalFirst, double leftSpreadFirst, double rightSpreadFirst,
        double lowerModalSecond, double upperModalSecond, double leftSpreadSecond, double rightSpreadSecond,
        double leftShapeIntegral, double rightShapeIntegral)
    {
        double lowerDifference = lowerModalFirst - lowerModalSecond;
        double upperDifference = upperModalFirst - upperModalSecond;
        double leftDifference = (lowerModalFirst - leftShapeIntegral * leftSpreadFirst)
                              - (lowerModalSecond - leftShapeIntegral * leftSpreadSecond);
        double rightDifference = (upperModalFirst + rightShapeIntegral * rightSpreadFirst)
                               - (upperModalSecond + rightShapeIntegral * rightSpreadSecond);
        return lowerDifference * lowerDifference + upperDifference * upperDifference
             + leftDifference * leftDifference + rightDifference * rightDifference;
    }

    /// <summary>
    /// Squared distance between two Gaussian fuzzy numbers (m, σ)_G (Viattchenin 2013, eq. (3.40)):
    /// d² = 3(m_i − m_j)² + (π/2)(σ_i − σ_j)².
    /// </summary>
    public static double GaussianSquared(double meanFirst, double widthFirst, double meanSecond, double widthSecond)
    {
        double meanDifference = meanFirst - meanSecond;
        double widthDifference = widthFirst - widthSecond;
        return 3.0 * meanDifference * meanDifference + Math.PI / 2.0 * widthDifference * widthDifference;
    }
}
