using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.Services;

namespace MathFuzzy.Desktop.ViewModels.Theory;

/// <summary>
/// "Fuzzy sets" theory page: membership-function shapes and their characteristics, operations
/// on fuzzy sets with the inference operators, and the construction of an antecedent term from
/// a fuzzy cluster of the current solution. All math comes from <c>Math.Fuzzy.Inference</c>.
/// </summary>
public sealed partial class FuzzySetsPageViewModel : ObservableObject
{
    public ShapesViewModel Shapes { get; } = new();
    public OperationsViewModel Operations { get; } = new();
    public ClusterConstructionViewModel FromCluster { get; }

    [ObservableProperty] private int _selectedTab;

    public FuzzySetsPageViewModel(IWorkspaceService workspace) => FromCluster = new ClusterConstructionViewModel(workspace);
}
