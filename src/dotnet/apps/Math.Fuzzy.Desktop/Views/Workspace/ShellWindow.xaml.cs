using MathFuzzy.Desktop.ViewModels.Workspace;
using System.Windows;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;

namespace MathFuzzy.Desktop.Views.Workspace;

public partial class ShellWindow : FluentWindow
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Drop-down buttons of the command bar open their menu on a left click, below the button.</summary>
    private void OnDropDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.ContextMenu is not { } menu) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
