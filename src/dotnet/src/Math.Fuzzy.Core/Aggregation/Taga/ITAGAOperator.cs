namespace MathFuzzy.Core.Aggregation.Taga;

public interface ITAGAOperator
{
    string Name { get; }
    double Aggregate(IReadOnlyList<double> sortedValues);
}
