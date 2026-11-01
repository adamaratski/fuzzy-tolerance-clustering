using OxyPlot;

namespace MathFuzzy.Desktop.ViewModels.Results;

/// <summary>
/// Plot colors matching the approved UI mockup (light theme): blue ramp for
/// heatmaps/series, coral for α* markers, design-system grays for axes.
/// </summary>
public static class DesignPalette
{
    public static readonly OxyColor Blue50  = OxyColor.Parse("#E6F1FB");
    public static readonly OxyColor Blue100 = OxyColor.Parse("#B5D4F4");
    public static readonly OxyColor Blue200 = OxyColor.Parse("#85B7EB");
    public static readonly OxyColor Blue400 = OxyColor.Parse("#378ADD");
    public static readonly OxyColor Blue600 = OxyColor.Parse("#185FA5");
    public static readonly OxyColor Blue800 = OxyColor.Parse("#0C447C");
    public static readonly OxyColor Blue900 = OxyColor.Parse("#042C53");
    public static readonly OxyColor Coral   = OxyColor.Parse("#D85A30");

    public static readonly OxyColor PlotBackground = OxyColor.Parse("#FFFFFF");
    public static readonly OxyColor AxisText       = OxyColor.Parse("#5F5E5A");
    public static readonly OxyColor AxisLine       = OxyColor.Parse("#C9C7BE");

    /// <summary>Low μ = light blue, high μ = dark blue (per mockup, light theme).</summary>
    public static OxyPalette BlueRamp(int steps = 256) =>
        OxyPalette.Interpolate(steps, Blue50, Blue100, Blue200, Blue400, Blue600, Blue800, Blue900);
}
