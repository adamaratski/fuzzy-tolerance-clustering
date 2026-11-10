using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MathFuzzy.Desktop.ViewModels.Results;

namespace MathFuzzy.Desktop.Views.Results;

public partial class ClusterTableView : UserControl
{
    private ClusterTableTabViewModel? _viewModel;

    public ClusterTableView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ClusterTableTabViewModel);
    }

    // DataGrid columns are not in the visual tree, so the β column is switched here and not by a binding
    private void Attach(ClusterTableTabViewModel? viewModel)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = viewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
        ShowBeta(_viewModel?.HasBeta == true);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClusterTableTabViewModel.HasBeta)) ShowBeta(_viewModel?.HasBeta == true);
    }

    private void ShowBeta(bool show)
    {
        BetaColumn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ScoreColumn.Header    = show ? "F" : "F₁";
    }
}
