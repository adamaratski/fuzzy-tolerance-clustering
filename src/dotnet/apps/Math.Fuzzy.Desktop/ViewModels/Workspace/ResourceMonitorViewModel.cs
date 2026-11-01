using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.Services.Monitoring;

namespace MathFuzzy.Desktop.ViewModels.Workspace;

/// <summary>One row of the resource monitor: a utilization thumbnail plus labels.</summary>
public sealed partial class ResourceMeterViewModel : ObservableObject
{
    public const int Capacity = 60;
    private readonly Queue<double> _samples = new(Capacity);

    public required string Title    { get; init; }
    public required Brush  Accent   { get; init; }
    public string AutomationName => $"{Title}: {ValueText}, {Detail}";

    [ObservableProperty] private string _subtitle  = "";
    [ObservableProperty] private string _valueText = "—";
    [ObservableProperty] private string _detail    = "";
    [ObservableProperty] private bool   _isAvailable = true;
    [ObservableProperty] private IReadOnlyList<double> _history = [];

    public void Push(double value, string valueText, string detail)
    {
        if (_samples.Count == Capacity) _samples.Dequeue();
        _samples.Enqueue(value);
        History   = _samples.ToArray();
        ValueText = valueText;
        Detail    = detail;
        OnPropertyChanged(nameof(AutomationName));
    }

    public override string ToString() => AutomationName;
}

/// <summary>
/// Task-Manager-style view of the machine: CPU, memory, every GPU and NPU, sampled once per
/// second on a worker thread. Process figures ("app") show what the clustering run itself uses.
/// </summary>
public sealed partial class ResourceMonitorViewModel : ObservableObject, IDisposable
{
    private static readonly Brush CpuBrush = Frozen(0x11, 0x7D, 0xBB);
    private static readonly Brush MemBrush = Frozen(0x8B, 0x12, 0xAE);
    private static readonly Brush GpuBrush = Frozen(0xC2, 0x5B, 0x0A);
    private static readonly Brush NpuBrush = Frozen(0x1A, 0x8F, 0x7E);

    private readonly ResourceSampler? _sampler;
    private readonly DispatcherTimer  _timer;
    private readonly ResourceMeterViewModel _cpu;
    private readonly ResourceMeterViewModel _mem;
    private readonly List<(ResourceMeterViewModel Meter, string Luid)> _accelerators = [];
    private bool _sampling;

    public ObservableCollection<ResourceMeterViewModel> Meters { get; } = [];

    [ObservableProperty] private bool _isPaused;

    public ResourceMonitorViewModel()
    {
        try { _sampler = new ResourceSampler(); }
        catch (Exception) { _sampler = null; }   // monitoring is best-effort; never break the app

        _cpu = new ResourceMeterViewModel { Title = "CPU", Accent = CpuBrush, Subtitle = _sampler?.CpuName ?? "" };
        _mem = new ResourceMeterViewModel { Title = "Memory", Accent = MemBrush };
        Meters.Add(_cpu);
        Meters.Add(_mem);

        var adapters = _sampler?.Adapters ?? [];
        int gpuIndex = 0;
        foreach (var a in adapters.Where(a => a.Kind == AcceleratorKind.Gpu))
            AddAccelerator(a, $"GPU {gpuIndex++}", GpuBrush);
        foreach (var a in adapters.Where(a => a.Kind == AcceleratorKind.Npu))
            AddAccelerator(a, "NPU", NpuBrush);

        if (!adapters.Any(a => a.Kind == AcceleratorKind.Npu))
            Meters.Add(new ResourceMeterViewModel
            {
                Title = "NPU", Accent = NpuBrush, Subtitle = "Not detected on this system",
                IsAvailable = false
            });

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync();
        if (_sampler is not null) _timer.Start();
    }

    partial void OnIsPausedChanged(bool value)
    {
        if (_sampler is null) return;
        if (value) _timer.Stop(); else _timer.Start();
    }

    private void AddAccelerator(AcceleratorInfo a, string title, Brush brush)
    {
        var meter = new ResourceMeterViewModel { Title = title, Accent = brush, Subtitle = ShortName(a.Name) };
        _accelerators.Add((meter, a.Luid));
        Meters.Add(meter);
    }

    private async Task TickAsync()
    {
        if (_sampling || _sampler is null) return;
        _sampling = true;
        try
        {
            var s = await Task.Run(_sampler.Sample);

            _cpu.Push(s.CpuPercent, Pct(s.CpuPercent),
                $"app {Pct(s.ProcessCpuPercent)} · {_sampler.LogicalProcessors} threads");

            double memPct = s.MemoryTotal > 0 ? 100.0 * s.MemoryUsed / s.MemoryTotal : 0;
            _mem.Push(memPct, Pct(memPct),
                $"{Gb(s.MemoryUsed)} / {Gb(s.MemoryTotal)} GB · app {Mb(s.ProcessWorkingSet)}");

            foreach (var (meter, luid) in _accelerators)
            {
                var a = s.Accelerators.FirstOrDefault(x => x.Adapter.Luid == luid);
                if (a is null) continue;
                string mem = a.Adapter.DedicatedMemory > 0
                    ? $"{Gb(a.DedicatedUsed)} / {Gb(a.Adapter.DedicatedMemory)} GB"
                    : "shared memory";
                meter.Push(a.Utilization, Pct(a.Utilization), mem);
            }
        }
        catch (Exception)
        {
            // A failed sample (e.g. counters being reset by the OS) is skipped.
        }
        finally { _sampling = false; }
    }

    private static string Pct(double v) => string.Create(CultureInfo.InvariantCulture, $"{v:0}%");
    private static string Gb(double bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture);
    private static string Mb(long bytes) => bytes >= 1L << 30
        ? (bytes / (1024.0 * 1024 * 1024)).ToString("0.0 GB", CultureInfo.InvariantCulture)
        : (bytes / (1024.0 * 1024)).ToString("0 MB", CultureInfo.InvariantCulture);

    private static string ShortName(string name) =>
        name.Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        _timer.Stop();
        _sampler?.Dispose();
    }
}
