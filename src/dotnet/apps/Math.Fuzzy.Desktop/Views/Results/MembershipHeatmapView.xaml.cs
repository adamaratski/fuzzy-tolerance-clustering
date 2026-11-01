using MathFuzzy.Desktop.ViewModels.Results;
using OxyPlot;
using System.Windows.Controls;
using System.Windows.Input;

namespace MathFuzzy.Desktop.Views.Results;

public partial class MembershipHeatmapView : UserControl
{
    public MembershipHeatmapView() => InitializeComponent();

    private void OnPlotMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MembershipHeatmapTabViewModel vm) return;
        if (vm.Plot is not { DefaultXAxis: { } xa, DefaultYAxis: { } ya }) return;

        var p  = e.GetPosition(PlotControl);
        var dp = OxyPlot.Axes.Axis.InverseTransform(new ScreenPoint(p.X, p.Y), xa, ya);
        vm.SelectDisplayRow((int)System.Math.Round(dp.Y));
    }
}
