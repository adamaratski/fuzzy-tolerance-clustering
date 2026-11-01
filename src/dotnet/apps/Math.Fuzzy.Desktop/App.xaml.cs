using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using MathFuzzy.Desktop.Services;
using MathFuzzy.Desktop.Views.Workspace;
using MathFuzzy.Desktop.ViewModels.Workspace;
using MathFuzzy.Desktop.ViewModels.Data;
using MathFuzzy.Desktop.ViewModels.Inference;
using MathFuzzy.Desktop.ViewModels.Theory;
using MathFuzzy.Desktop.ViewModels.Algorithm;
using MathFuzzy.Desktop.ViewModels.Results;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MathFuzzy.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Light theme per the approved mockup, WITHOUT system accent override
        ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.None, updateAccent: false);
        // Apply design blue (#378ADD) as app accent color (matches mockup palette)
        ApplicationAccentColorManager.Apply(
            Color.FromArgb(0xFF, 0x37, 0x8A, 0xDD),
            ApplicationTheme.Light,
            systemGlassColor: false,
            systemAccentColor: false
        );

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var shell = Services.GetRequiredService<ShellWindow>();
        shell.Closed += (_, _) => (Services as IDisposable)?.Dispose();
        shell.Show();

        // Optional CSV path as a command-line argument: open it immediately.
        if (e.Args.FirstOrDefault(a => a.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) is { } csv
            && shell.DataContext is ShellViewModel vm)
        {
            try { vm.LoadCsvFromPath(csv); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Load error",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IAlgorithmRunnerService, AlgorithmRunnerService>();

        services.AddTransient<ShellViewModel>();
        services.AddTransient<DataPanelViewModel>();
        services.AddTransient<AlgorithmPanelViewModel>();
        services.AddTransient<ResultsPanelViewModel>();
        services.AddTransient<VisualizePanelViewModel>();
        services.AddTransient<FuzzySetsPageViewModel>();
        services.AddTransient<InferencePageViewModel>();
        services.AddSingleton<ResourceMonitorViewModel>();

        services.AddTransient<ShellWindow>();
    }
}
