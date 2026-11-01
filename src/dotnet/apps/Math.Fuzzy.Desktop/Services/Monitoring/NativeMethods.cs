using System.Runtime.InteropServices;

namespace MathFuzzy.Desktop.Services.Monitoring;

/// <summary>
/// Win32 entry points used by the resource monitor: system CPU times and memory status
/// (kernel32), Performance Data Helper counters (pdh), and the D3D kernel-mode thunks
/// (gdi32) that enumerate graphics and compute-only (NPU) adapters the same way
/// Task Manager does.
/// </summary>
internal static class NativeMethods
{
    // ── kernel32 ────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    // ── pdh ─────────────────────────────────────────────────────────────────

    public const uint PDH_FMT_DOUBLE   = 0x00000200;
    public const uint PDH_FMT_NOCAP100 = 0x00008000;
    public const uint PDH_MORE_DATA    = 0x800007D2;

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE
    {
        public uint   CStatus;
        public double DoubleValue;   // union; only the double member is requested
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr               Name;    // LPWSTR into the same buffer
        public PDH_FMT_COUNTERVALUE Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    public static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    public static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    public static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    public static extern uint PdhGetFormattedCounterArray(
        IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    public static extern uint PdhCloseQuery(IntPtr query);

    // ── gdi32 / D3DKMT ──────────────────────────────────────────────────────

    public const int KMTQAITYPE_GETSEGMENTSIZE      = 3;
    public const int KMTQAITYPE_ADAPTERTYPE         = 15;
    public const int KMTQAITYPE_DRIVER_DESCRIPTION  = 65;

    // D3DKMT_ADAPTERTYPE bit field
    public const uint ADAPTERTYPE_RENDER       = 1u << 0;
    public const uint ADAPTERTYPE_DISPLAY      = 1u << 1;
    public const uint ADAPTERTYPE_SOFTWARE     = 1u << 2;
    public const uint ADAPTERTYPE_COMPUTE_ONLY = 1u << 11;

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ADAPTERINFO
    {
        public uint hAdapter;
        public uint LuidLowPart;
        public int  LuidHighPart;
        public uint NumOfSources;
        public int  bPrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ENUMADAPTERS2
    {
        public uint   NumAdapters;
        public IntPtr pAdapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_QUERYADAPTERINFO
    {
        public uint   hAdapter;
        public int    Type;
        public IntPtr pPrivateDriverData;
        public uint   PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_SEGMENTSIZEINFO
    {
        public ulong DedicatedVideoMemorySize;
        public ulong DedicatedSystemMemorySize;
        public ulong SharedSystemMemorySize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_CLOSEADAPTER
    {
        public uint hAdapter;
    }

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTEnumAdapters2(ref D3DKMT_ENUMADAPTERS2 enumAdapters);

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTQueryAdapterInfo(ref D3DKMT_QUERYADAPTERINFO query);

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER close);
}
