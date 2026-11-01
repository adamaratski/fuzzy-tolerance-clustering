using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Core.Distance;

namespace MathFuzzy.Desktop.ViewModels.Algorithm;

public sealed record DistanceInfo(string Name, string Formula);

public interface IDistanceParameterViewModel
{
    bool HasParameters { get; }
    IDistance Build();
}

internal sealed class FixedDistanceViewModel(IDistance instance) : IDistanceParameterViewModel
{
    public bool HasParameters => false;
    public IDistance Build() => instance;
}

public sealed partial class MinkowskiParameterViewModel : ObservableObject, IDistanceParameterViewModel
{
    [ObservableProperty] private string _pText = "3";

    public bool HasParameters => true;

    public IDistance Build()
    {
        double p = double.TryParse(PText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0
            ? parsed : 3.0;
        return new MinkowskiDistance(p);
    }
}

internal static class DistanceParameterFactory
{
    public static readonly IReadOnlyList<DistanceInfo> Items =
    [
        new("Euclidean norm.",  "√(Σ(aᵢ−bᵢ)²/d)"),
        new("Sq. Euclidean",    "Σ(aᵢ−bᵢ)²/d"),
        new("Manhattan norm.",  "Σ|aᵢ−bᵢ|/d"),
        new("Chebyshev",        "maxᵢ|aᵢ−bᵢ|"),
        new("Minkowski",        "(Σ|aᵢ−bᵢ|ᵖ/d)¹ᵀᵖ"),
        new("Cosine",           "1−a·b/(‖a‖‖b‖)"),
        new("Hamming norm.",    "Σ|aᵢ−bᵢ|/d"),
    ];

    public static IDistanceParameterViewModel Create(DistanceInfo info) => info.Name switch
    {
        "Minkowski"        => new MinkowskiParameterViewModel(),
        "Manhattan norm." => new FixedDistanceViewModel(ManhattanDistance.Instance),
        "Chebyshev"        => new FixedDistanceViewModel(ChebyshevDistance.Instance),
        "Cosine"           => new FixedDistanceViewModel(CosineDistance.Instance),
        "Hamming norm."    => new FixedDistanceViewModel(HammingDistance.Instance),
        "Sq. Euclidean"    => new FixedDistanceViewModel(SquaredEuclideanDistance.Instance),
        _                  => new FixedDistanceViewModel(EuclideanDistance.Instance)
    };
}
