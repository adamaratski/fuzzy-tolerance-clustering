using System.Text.Json.Serialization;

namespace MathFuzzy.Inference.MembershipFunctions;

/// <summary>Shape of a membership function, used to choose how terms are fitted from data.</summary>
public enum MfShape { Triangular, Trapezoidal, Gaussian }

/// <summary>
/// Membership function μ: ℝ → [0, 1] of a linguistic term. Immutable: editing a
/// parameter produces a new instance via <see cref="WithParameters"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TriangularMf),  "triangular")]
[JsonDerivedType(typeof(TrapezoidalMf), "trapezoidal")]
[JsonDerivedType(typeof(GaussianMf),    "gaussian")]
public abstract record MembershipFunction
{
    public abstract double Evaluate(double value);

    [JsonIgnore] public abstract MfShape Shape { get; }

    /// <summary>Parameter values in canonical order (a, b, c[, d]) or (c, σ).</summary>
    [JsonIgnore] public abstract IReadOnlyList<double> Parameters { get; }

    /// <summary>Display names of <see cref="Parameters"/> in the usual notation (a, b, c, d; c, σ).</summary>
    [JsonIgnore] public abstract IReadOnlyList<string> ParameterNames { get; }

    /// <summary>Interval outside which μ is (numerically) zero; used for plotting and universes.</summary>
    [JsonIgnore] public abstract (double Min, double Max) Support { get; }

    /// <summary>Middle of the set where μ = 1.</summary>
    [JsonIgnore] public abstract double CoreCenter { get; }

    /// <summary>Area under μ: the continuous analogue of the scalar cardinality (Σ-count) of a fuzzy set.</summary>
    [JsonIgnore] public abstract double Area { get; }

    /// <summary>
    /// α-cut {x : μ(x) ≥ α} for α ∈ (0, 1]. Every shape here is convex and normal,
    /// so the cut is a non-empty closed interval.
    /// </summary>
    public (double Min, double Max) AlphaCut(double alpha)
    {
        if (!(alpha > 0 && alpha <= 1)) throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "α must lie in (0, 1].");
        return AlphaCutCore(alpha);
    }

    protected abstract (double Min, double Max) AlphaCutCore(double alpha);

    /// <summary>
    /// Returns a copy with new parameter values. Values are repaired so the shape
    /// stays valid (sorted break points, σ &gt; 0) rather than rejected, because
    /// they come from interactive editing.
    /// </summary>
    public abstract MembershipFunction WithParameters(IReadOnlyList<double> values);

    /// <summary>Same as <see cref="WithParameters"/> with one parameter replaced.</summary>
    public MembershipFunction WithParameter(int parameterIndex, double value)
    {
        var values = Parameters.ToArray();
        values[parameterIndex] = value;
        return WithParameters(values);
    }

    protected static void Require(IReadOnlyList<double> values, int expectedCount)
    {
        if (values.Count != expectedCount)
            throw new ArgumentException($"{expectedCount} parameters expected, got {values.Count}.", nameof(values));
        foreach (var value in values)
            if (!double.IsFinite(value)) throw new ArgumentException("Parameters must be finite.", nameof(values));
    }
}

/// <summary>
/// Triangular MF (a, b, c): feet a ≤ c and apex b. Degenerate sides (a = b or b = c)
/// are vertical: μ(b) = 1.
/// </summary>
public sealed record TriangularMf(double LeftFoot, double Apex, double RightFoot) : MembershipFunction
{
    public override double Evaluate(double value)
    {
        if (value < LeftFoot || value > RightFoot) return 0;
        if (value == Apex) return 1;
        return value < Apex
            ? (value - LeftFoot) / (Apex - LeftFoot)
            : (RightFoot - value) / (RightFoot - Apex);
    }

    [JsonIgnore] public override MfShape Shape => MfShape.Triangular;
    [JsonIgnore] public override IReadOnlyList<double> Parameters => [LeftFoot, Apex, RightFoot];
    [JsonIgnore] public override IReadOnlyList<string> ParameterNames => ["a", "b", "c"];
    [JsonIgnore] public override (double Min, double Max) Support => (LeftFoot, RightFoot);
    [JsonIgnore] public override double CoreCenter => Apex;
    [JsonIgnore] public override double Area => (RightFoot - LeftFoot) / 2;

    protected override (double Min, double Max) AlphaCutCore(double alpha) =>
        (LeftFoot + alpha * (Apex - LeftFoot), RightFoot - alpha * (RightFoot - Apex));

    public override MembershipFunction WithParameters(IReadOnlyList<double> values)
    {
        Require(values, 3);
        var sorted = values.Order().ToArray();
        return new TriangularMf(sorted[0], sorted[1], sorted[2]);
    }
}

/// <summary>Trapezoidal MF (a, b, c, d): rises on [a, b], equals 1 on [b, c], falls on [c, d].</summary>
public sealed record TrapezoidalMf(double LeftFoot, double LeftShoulder, double RightShoulder, double RightFoot) : MembershipFunction
{
    public override double Evaluate(double value)
    {
        if (value < LeftFoot || value > RightFoot) return 0;
        if (value >= LeftShoulder && value <= RightShoulder) return 1;
        return value < LeftShoulder
            ? (value - LeftFoot) / (LeftShoulder - LeftFoot)
            : (RightFoot - value) / (RightFoot - RightShoulder);
    }

    [JsonIgnore] public override MfShape Shape => MfShape.Trapezoidal;
    [JsonIgnore] public override IReadOnlyList<double> Parameters => [LeftFoot, LeftShoulder, RightShoulder, RightFoot];
    [JsonIgnore] public override IReadOnlyList<string> ParameterNames => ["a", "b", "c", "d"];
    [JsonIgnore] public override (double Min, double Max) Support => (LeftFoot, RightFoot);
    [JsonIgnore] public override double CoreCenter => (LeftShoulder + RightShoulder) / 2;
    [JsonIgnore] public override double Area => ((RightFoot - LeftFoot) + (RightShoulder - LeftShoulder)) / 2;

    protected override (double Min, double Max) AlphaCutCore(double alpha) =>
        (LeftFoot + alpha * (LeftShoulder - LeftFoot), RightFoot - alpha * (RightFoot - RightShoulder));

    public override MembershipFunction WithParameters(IReadOnlyList<double> values)
    {
        Require(values, 4);
        var sorted = values.Order().ToArray();
        return new TrapezoidalMf(sorted[0], sorted[1], sorted[2], sorted[3]);
    }
}

/// <summary>Gaussian MF μ(x) = exp(−(x − c)² / 2σ²).</summary>
public sealed record GaussianMf(double Center, double Sigma) : MembershipFunction
{
    /// <summary>Smallest admissible σ; editing to σ ≤ 0 is clamped to this value.</summary>
    public const double MinSigma = 1e-9;

    public override double Evaluate(double value)
    {
        double standardized = (value - Center) / Sigma;
        return System.Math.Exp(-0.5 * standardized * standardized);
    }

    [JsonIgnore] public override MfShape Shape => MfShape.Gaussian;
    [JsonIgnore] public override IReadOnlyList<double> Parameters => [Center, Sigma];
    [JsonIgnore] public override IReadOnlyList<string> ParameterNames => ["c", "σ"];
    [JsonIgnore] public override (double Min, double Max) Support => (Center - 4 * Sigma, Center + 4 * Sigma);
    [JsonIgnore] public override double CoreCenter => Center;
    [JsonIgnore] public override double Area => Sigma * System.Math.Sqrt(2 * System.Math.PI);

    protected override (double Min, double Max) AlphaCutCore(double alpha)
    {
        double halfWidth = Sigma * System.Math.Sqrt(-2 * System.Math.Log(alpha));
        return (Center - halfWidth, Center + halfWidth);
    }

    public override MembershipFunction WithParameters(IReadOnlyList<double> values)
    {
        Require(values, 2);
        return new GaussianMf(values[0], System.Math.Max(System.Math.Abs(values[1]), MinSigma));
    }
}
