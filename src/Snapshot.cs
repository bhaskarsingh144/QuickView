using System;

internal sealed class TempZone
{
    public string Name;
    public int Celsius;
}

internal sealed class DiskDrive
{
    public string Name;
    public long Bytes;
    public string Media;
}

internal sealed class AppUse
{
    public string Name;
    public int Count;
    public float CpuPercent;
    public long MemoryBytes;
}

internal sealed class Snapshot
{
    public bool CpuKnown;
    public float CpuPercent;
    public int ClockMHz;
    public bool ClockKnown;
    public string CpuName;
    public int CpuCores;
    public int CpuThreads;
    public int CpuMaxMHz;
    public int CpuL3Kb;
    public int[] CoreMHz;
    public TempZone[] Temps;
    public bool CpuTempKnown;
    public int CpuCelsius;
    public bool MemTempKnown;
    public int MemCelsius;
    public bool GpuTempKnown;
    public int GpuCelsius;

    public long MemoryUsed;
    public long MemoryTotal;
    public bool CommitKnown;
    public long Committed;
    public long CommitLimit;
    public bool CachedKnown;
    public long Cached;
    public bool PoolKnown;
    public long PagedPool;
    public long NonPagedPool;
    public bool SpeedKnown;
    public int SpeedMHz;
    public int ModuleCount;
    public long InstalledBytes;
    public bool ReservedKnown;
    public long HardwareReserved;

    public bool GpuKnown;
    public float GpuPercent;
    public bool GpuMemoryKnown;
    public long GpuMemoryUsed;
    public bool GpuSharedKnown;
    public long GpuShared;

    public bool BatteryKnown;
    public int BatteryPercent;
    public bool AcKnown;
    public bool PluggedIn;
    public bool Charging;

    public bool WifiUp;
    public string WifiSsid;
    public bool NetKnown;
    public float DownMBps;
    public float UpMBps;

    public AppUse[] ByMemory;
    public AppUse[] ByCpu;
    public float[] CpuHistory;
    public float[] MemoryHistory;
    public float[] GpuHistory;

    public bool DiskKnown;
    public float DiskBusy;
    public bool DiskSpaceKnown;
    public long DiskUsed;
    public long DiskTotal;
    public float DiskReadMBps;
    public float DiskWriteMBps;
    public DiskDrive[] Drives;
    public float[] DiskHistory;

    public static Snapshot Empty()
    {
        Snapshot s = new Snapshot();
        s.CoreMHz = new int[0];
        s.Temps = new TempZone[0];
        s.ByMemory = new AppUse[0];
        s.ByCpu = new AppUse[0];
        s.CpuHistory = new float[0];
        s.MemoryHistory = new float[0];
        s.GpuHistory = new float[0];
        s.Drives = new DiskDrive[0];
        s.DiskHistory = new float[0];
        s.WifiSsid = "";
        return s;
    }
}
