using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using static MathFuzzy.Desktop.Services.Monitoring.NativeMethods;

namespace MathFuzzy.Desktop.Services.Monitoring;

public enum AcceleratorKind { Gpu, Npu }

/// <summary>A graphics or compute-only adapter as reported by the kernel graphics stack.</summary>
public sealed record AcceleratorInfo(string Luid, string Name, AcceleratorKind Kind, ulong DedicatedMemory);

/// <summary>Utilization of one accelerator in one sample.</summary>
public sealed record AcceleratorSample(AcceleratorInfo Adapter, double Utilization, ulong DedicatedUsed);

/// <summary>One sample of the machine and of this process.</summary>
public sealed record ResourceSnapshot(
    double CpuPercent,
    double ProcessCpuPercent,
    ulong  MemoryTotal,
    ulong  MemoryUsed,
    long   ProcessWorkingSet,
    IReadOnlyList<AcceleratorSample> Accelerators);

/// <summary>
/// Samples CPU, memory and GPU/NPU utilization. CPU uses GetSystemTimes deltas, memory
/// GlobalMemoryStatusEx; accelerators are enumerated with D3DKMTEnumAdapters2 (which,
/// unlike DXGI, also lists compute-only MCDM adapters such as NPUs) and their load is read
/// from the "GPU Engine" performance counters, aggregated as Task Manager does: per engine
/// the sum over processes, per adapter the busiest engine.
/// </summary>
public sealed partial class ResourceSampler : IDisposable
{
    private long _prevIdle, _prevKernel, _prevUser;
    private TimeSpan _prevProcCpu;
    private long _prevStamp;
    private readonly Process _process = Process.GetCurrentProcess();

    private IntPtr _query;
    private IntPtr _engineCounter;
    private IntPtr _memoryCounter;
    private bool   _pdhReady;

    public string CpuName { get; }
    public int LogicalProcessors { get; } = Environment.ProcessorCount;
    public IReadOnlyList<AcceleratorInfo> Adapters { get; }

    public ResourceSampler()
    {
        CpuName  = ReadCpuName();
        Adapters = EnumerateAdapters();
        GetSystemTimes(out _prevIdle, out _prevKernel, out _prevUser);
        _prevProcCpu = _process.TotalProcessorTime;
        _prevStamp   = Stopwatch.GetTimestamp();
        InitPdh();
    }

    public ResourceSnapshot Sample()
    {
        // CPU (whole machine)
        double cpu = 0;
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            long dIdle  = idle - _prevIdle;
            long dTotal = (kernel - _prevKernel) + (user - _prevUser);   // kernel time includes idle
            cpu = dTotal > 0 ? System.Math.Clamp(100.0 * (dTotal - dIdle) / dTotal, 0, 100) : 0;
            (_prevIdle, _prevKernel, _prevUser) = (idle, kernel, user);
        }

        // CPU (this process), normalised to all logical processors like Task Manager
        _process.Refresh();
        var  procCpu = _process.TotalProcessorTime;
        long stamp   = Stopwatch.GetTimestamp();
        double wall  = Stopwatch.GetElapsedTime(_prevStamp, stamp).TotalSeconds;
        double procPct = wall > 0
            ? System.Math.Clamp(100.0 * (procCpu - _prevProcCpu).TotalSeconds / (wall * LogicalProcessors), 0, 100)
            : 0;
        _prevProcCpu = procCpu;
        _prevStamp   = stamp;

        // Memory
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);

        return new ResourceSnapshot(
            cpu, procPct,
            mem.ullTotalPhys, mem.ullTotalPhys - mem.ullAvailPhys,
            _process.WorkingSet64,
            SampleAccelerators());
    }

    // ── accelerators ────────────────────────────────────────────────────────

    [GeneratedRegex(@"luid_(0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)_phys_\d+_eng_(\d+)")]
    private static partial Regex EngineInstance();

    [GeneratedRegex(@"luid_(0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)_phys_\d+")]
    private static partial Regex AdapterInstance();

    private IReadOnlyList<AcceleratorSample> SampleAccelerators()
    {
        if (Adapters.Count == 0) return [];

        var load = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var used = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

        if (_pdhReady && PdhCollectQueryData(_query) == 0)
        {
            // per (luid, engine) sum over processes
            var engines = new Dictionary<(string, string), double>();
            foreach (var (name, value) in ReadArray(_engineCounter))
            {
                var m = EngineInstance().Match(name);
                if (!m.Success) continue;
                var key = (m.Groups[1].Value, m.Groups[2].Value);
                engines[key] = engines.GetValueOrDefault(key) + value;
            }
            foreach (var ((luid, _), v) in engines)
                load[luid] = System.Math.Max(load.GetValueOrDefault(luid), v);

            foreach (var (name, value) in ReadArray(_memoryCounter))
            {
                var m = AdapterInstance().Match(name);
                if (m.Success) used[m.Groups[1].Value] = used.GetValueOrDefault(m.Groups[1].Value) + (ulong)System.Math.Max(0, value);
            }
        }

        return Adapters
            .Select(a => new AcceleratorSample(a,
                System.Math.Clamp(load.GetValueOrDefault(a.Luid), 0, 100),
                used.GetValueOrDefault(a.Luid)))
            .ToList();
    }

    private void InitPdh()
    {
        if (Adapters.Count == 0) return;
        try
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
            if (PdhAddEnglishCounter(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _engineCounter) != 0) return;
            PdhAddEnglishCounter(_query, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out _memoryCounter);
            PdhCollectQueryData(_query);   // rate counters need a first sample
            _pdhReady = true;
        }
        catch (DllNotFoundException) { _pdhReady = false; }
    }

    private static List<(string Name, double Value)> ReadArray(IntPtr counter)
    {
        var list = new List<(string, double)>();
        if (counter == IntPtr.Zero) return list;

        uint size = 0;
        uint status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out _, IntPtr.Zero);
        if (status != PDH_MORE_DATA || size == 0) return list;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out uint count, buffer) != 0)
                return list;
            int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(buffer + i * itemSize);
                if (item.Value.CStatus is not (0 or 1)) continue;   // PDH_CSTATUS_VALID_DATA / NEW_DATA
                list.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value.DoubleValue));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return list;
    }

    private static IReadOnlyList<AcceleratorInfo> EnumerateAdapters()
    {
        var result = new List<AcceleratorInfo>();
        try
        {
            var e = new D3DKMT_ENUMADAPTERS2();
            if (D3DKMTEnumAdapters2(ref e) != 0 || e.NumAdapters == 0) return result;

            int size = Marshal.SizeOf<D3DKMT_ADAPTERINFO>();
            e.pAdapters = Marshal.AllocHGlobal(size * (int)e.NumAdapters);
            try
            {
                if (D3DKMTEnumAdapters2(ref e) != 0) return result;
                for (int i = 0; i < e.NumAdapters; i++)
                {
                    var a = Marshal.PtrToStructure<D3DKMT_ADAPTERINFO>(e.pAdapters + i * size);
                    try
                    {
                        uint type = QueryUInt32(a.hAdapter, KMTQAITYPE_ADAPTERTYPE);
                        if ((type & ADAPTERTYPE_SOFTWARE) != 0) continue;

                        AcceleratorKind? kind =
                            (type & ADAPTERTYPE_COMPUTE_ONLY) != 0 ? AcceleratorKind.Npu :
                            (type & ADAPTERTYPE_RENDER) != 0       ? AcceleratorKind.Gpu : null;
                        if (kind is null) continue;

                        string name = QueryString(a.hAdapter, KMTQAITYPE_DRIVER_DESCRIPTION, 4096) ?? "Unknown adapter";
                        ulong dedicated = QuerySegmentSize(a.hAdapter);
                        string luid = string.Create(CultureInfo.InvariantCulture,
                            $"0x{a.LuidHighPart:X8}_0x{a.LuidLowPart:X8}");
                        result.Add(new AcceleratorInfo(luid, name, kind.Value, dedicated));
                    }
                    finally
                    {
                        var close = new D3DKMT_CLOSEADAPTER { hAdapter = a.hAdapter };
                        D3DKMTCloseAdapter(ref close);
                    }
                }
            }
            finally { Marshal.FreeHGlobal(e.pAdapters); }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Pre-WDDM 2.x systems: no accelerator monitoring.
        }
        return result;
    }

    private static uint QueryUInt32(uint adapter, int type)
    {
        IntPtr p = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            var q = new D3DKMT_QUERYADAPTERINFO { hAdapter = adapter, Type = type, pPrivateDriverData = p, PrivateDriverDataSize = sizeof(uint) };
            return D3DKMTQueryAdapterInfo(ref q) == 0 ? (uint)Marshal.ReadInt32(p) : 0;
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    private static string? QueryString(uint adapter, int type, int chars)
    {
        IntPtr p = Marshal.AllocHGlobal(chars * 2);
        try
        {
            var q = new D3DKMT_QUERYADAPTERINFO { hAdapter = adapter, Type = type, pPrivateDriverData = p, PrivateDriverDataSize = (uint)(chars * 2) };
            return D3DKMTQueryAdapterInfo(ref q) == 0 ? Marshal.PtrToStringUni(p) : null;
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    private static ulong QuerySegmentSize(uint adapter)
    {
        int size = Marshal.SizeOf<D3DKMT_SEGMENTSIZEINFO>();
        IntPtr p = Marshal.AllocHGlobal(size);
        try
        {
            var q = new D3DKMT_QUERYADAPTERINFO { hAdapter = adapter, Type = KMTQAITYPE_GETSEGMENTSIZE, pPrivateDriverData = p, PrivateDriverDataSize = (uint)size };
            return D3DKMTQueryAdapterInfo(ref q) == 0 ? Marshal.PtrToStructure<D3DKMT_SEGMENTSIZEINFO>(p).DedicatedVideoMemorySize : 0;
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("ProcessorNameString") is string s)
                return Regex.Replace(s, @"\((R|TM)\)|\s+\d+-Core Processor|\s+Processor|\s+CPU\b.*", "", RegexOptions.IgnoreCase).Trim();
        }
        catch (System.Security.SecurityException) { }
        return "Processor";
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; }
        _process.Dispose();
    }
}
