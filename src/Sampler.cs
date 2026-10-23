using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

internal sealed class Sampler : IDisposable
{
    private const int History = 60;

    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new object();
    private int _busy;

    private PerformanceCounter _cpu;
    private PerformanceCounter _clock;
    private CoreSlot[] _cores = new CoreSlot[0];
    private PerformanceCounter[] _gpuUtils;
    private PerformanceCounter[] _gpuMems;
    private PerformanceCounter[] _gpuShared;
    private PerformanceCounter _committed;
    private PerformanceCounter _commitLimit;
    private PerformanceCounter _modified;
    private PerformanceCounter _standbyNormal;
    private PerformanceCounter _standbyReserve;
    private PerformanceCounter _standbyCore;
    private PerformanceCounter _paged;
    private PerformanceCounter _nonPaged;

    private bool _countersReady;
    private bool _cpuPrimed;
    private bool _gpuPrimed;
    private bool _factsReady;
    private TempZone[] _temps = new TempZone[0];
    private bool _speedKnown;
    private int _speedMHz;
    private int _moduleCount;
    private long _installedBytes;
    private string _cpuName;
    private int _cpuCores;
    private int _cpuThreads;
    private int _cpuMaxMHz;
    private int _cpuL3Kb;
    private PerformanceCounter[] _netIn;
    private PerformanceCounter[] _netOut;
    private bool _netPrimed;
    private int _netRefresh;
    private int _gpuRefresh;
    private PerformanceCounter[] _therm;
    private string[] _thermNames = new string[0];
    private bool _thermPrimed;
    private int _wmiCpu = -1;
    private int _wmiGpu = -1;
    private int _wmiMem = -1;
    private PerformanceCounter[] _diskRead;
    private PerformanceCounter[] _diskWrite;
    private PerformanceCounter[] _diskIdle;
    private PerformanceCounter[] _diskTime;
    private PerformanceCounter[] _diskFree;
    private bool _diskPrimed;
    private long _volumeBytes;
    private DiskDrive[] _drives = new DiskDrive[0];

    private readonly Dictionary<int, TimeSpan> _lastCpu = new Dictionary<int, TimeSpan>();
    private DateTime _lastWall = DateTime.MinValue;
    private readonly float[] _cpuHist = new float[History];
    private readonly float[] _memHist = new float[History];
    private readonly float[] _gpuHist = new float[History];
    private readonly float[] _diskHist = new float[History];
    private int _histPos;
    private int _histCount;

    public event Action<Snapshot> Sampled;

    public Sampler()
    {
        _timer = new System.Threading.Timer(OnTick, null, 400, 1000);
    }

    public void Dispose()
    {
        _timer.Dispose();
        lock (_gate)
        {
            DropCounters();
        }
    }

    private void OnTick(object state)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;
        try
        {
            Snapshot snap = Collect();
            Action<Snapshot> handler = Sampled;
            if (handler != null)
                handler(snap);
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private Snapshot Collect()
    {
        lock (_gate)
        {
            EnsureCounters();
            Snapshot s = Snapshot.Empty();
            bool historyReady = _cpuPrimed;
            ReadCpu(s);
            ReadClocks(s);
            ReadMemory(s);
            ReadMemoryFacts(s);
            ReadGpu(s);
            ReadThermal(s);
            ReadDisk(s);
            ReadNet(s);
            ReadBattery(s);
            ReadProcesses(s);
            s.Temps = _temps;
            s.SpeedKnown = _speedKnown;
            s.SpeedMHz = _speedMHz;
            s.ModuleCount = _moduleCount;
            s.InstalledBytes = _installedBytes;
            s.CpuName = _cpuName;
            s.CpuCores = _cpuCores;
            s.CpuThreads = _cpuThreads;
            s.CpuMaxMHz = _cpuMaxMHz;
            s.CpuL3Kb = _cpuL3Kb;
            if (_installedBytes > s.MemoryTotal + (50L * 1024L * 1024L))
            {
                s.ReservedKnown = true;
                s.HardwareReserved = _installedBytes - s.MemoryTotal;
            }
            if (historyReady)
                PushHistory(s);
            return s;
        }
    }

    private void EnsureCounters()
    {
        if (_countersReady)
            return;
        _cpu = TryCounter("Processor Information", "% Processor Time", "_Total");
        if (_cpu == null)
            _cpu = TryCounter("Processor", "% Processor Time", "_Total");
        _clock = TryCounter("Processor Information", "Actual Frequency", "_Total");
        _cores = LoadCores();
        _committed = TryCounter("Memory", "Committed Bytes", null);
        _commitLimit = TryCounter("Memory", "Commit Limit", null);
        _modified = TryCounter("Memory", "Modified Page List Bytes", null);
        _standbyNormal = TryCounter("Memory", "Standby Cache Normal Priority Bytes", null);
        _standbyReserve = TryCounter("Memory", "Standby Cache Reserve Bytes", null);
        _standbyCore = TryCounter("Memory", "Standby Cache Core Bytes", null);
        _paged = TryCounter("Memory", "Pool Paged Bytes", null);
        _nonPaged = TryCounter("Memory", "Pool Nonpaged Bytes", null);
        EnsureGpu();
        EnsureDisk();
        LoadThermal();
        ReadStaticFacts();
        _countersReady = true;
    }

    private static PerformanceCounter TryCounter(string category, string name, string instance)
    {
        try
        {
            if (instance == null)
                return new PerformanceCounter(category, name, true);
            return new PerformanceCounter(category, name, instance, true);
        }
        catch
        {
            return null;
        }
    }

    private static CoreSlot[] LoadCores()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("Processor Information"))
                return new CoreSlot[0];
            string[] names = new PerformanceCounterCategory("Processor Information").GetInstanceNames();
            List<CoreSlot> list = new List<CoreSlot>();
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "_Total", StringComparison.OrdinalIgnoreCase))
                    continue;
                PerformanceCounter counter = TryCounter("Processor Information", "Actual Frequency", names[i]);
                if (counter == null)
                    continue;
                CoreSlot slot = new CoreSlot();
                slot.Index = CoreIndex(names[i]);
                slot.Counter = counter;
                list.Add(slot);
            }
            list.Sort(delegate(CoreSlot a, CoreSlot b) { return a.Index.CompareTo(b.Index); });
            return list.ToArray();
        }
        catch
        {
            return new CoreSlot[0];
        }
    }

    private static int CoreIndex(string name)
    {
        int comma = name.LastIndexOf(',');
        if (comma < 0 || comma >= name.Length - 1)
            return 0;
        int n;
        if (!int.TryParse(name.Substring(comma + 1), out n))
            return 0;
        return n;
    }

    private void ReadStaticFacts()
    {
        if (_factsReady)
            return;
        _factsReady = true;
        ReadProcessor();
        ReadModules();
        ReadTemperature();
        ReadDiskHardware();
    }

    private void ReadProcessor()
    {
        ManagementObjectSearcher searcher = null;
        try
        {
            searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L3CacheSize FROM Win32_Processor");
            searcher.Options.Timeout = new TimeSpan(0, 0, 2);
            foreach (ManagementObject obj in searcher.Get())
            {
                object name = obj["Name"];
                if (name != null)
                    _cpuName = ShortCpu(name.ToString());
                _cpuCores = ReadInt(obj, "NumberOfCores");
                _cpuThreads = ReadInt(obj, "NumberOfLogicalProcessors");
                _cpuMaxMHz = ReadInt(obj, "MaxClockSpeed");
                _cpuL3Kb = ReadInt(obj, "L3CacheSize");
                obj.Dispose();
                break;
            }
        }
        catch
        {
        }
        finally
        {
            if (searcher != null)
                searcher.Dispose();
        }
    }

    private static string ShortCpu(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        string text = raw.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "");
        while (text.IndexOf("  ") >= 0)
            text = text.Replace("  ", " ");
        return text.Trim();
    }

    private static int ReadInt(ManagementObject obj, string key)
    {
        try
        {
            object value = obj[key];
            if (value == null)
                return 0;
            return Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private void ReadModules()
    {
        ManagementObjectSearcher searcher = null;
        try
        {
            searcher = new ManagementObjectSearcher("SELECT Capacity, Speed FROM Win32_PhysicalMemory");
            searcher.Options.Timeout = new TimeSpan(0, 0, 2);
            int count = 0;
            long installed = 0;
            int speed = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                count++;
                object cap = obj["Capacity"];
                if (cap != null)
                    installed += Convert.ToInt64(cap);
                object spd = obj["Speed"];
                if (spd != null && speed == 0)
                    speed = Convert.ToInt32(spd);
                obj.Dispose();
            }
            if (count > 0 && installed > 0)
            {
                _moduleCount = count;
                _installedBytes = installed;
            }
            if (speed > 0)
            {
                _speedKnown = true;
                _speedMHz = speed;
            }
        }
        catch
        {
        }
        finally
        {
            if (searcher != null)
                searcher.Dispose();
        }
    }

    private void ReadTemperature()
    {
        ManagementObjectSearcher searcher = null;
        try
        {
            searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            searcher.Options.Timeout = new TimeSpan(0, 0, 2);
            List<TempZone> zones = new List<TempZone>();
            foreach (ManagementObject obj in searcher.Get())
            {
                object raw = obj["CurrentTemperature"];
                if (raw != null)
                {
                    int tenth = Convert.ToInt32(raw);
                    int c = (int)Math.Round((tenth / 10.0) - 273.15);
                    if (c >= 20 && c <= 115)
                    {
                        TempZone zone = new TempZone();
                        object name = obj["InstanceName"];
                        zone.Name = ShortZone(name == null ? "" : name.ToString());
                        zone.Celsius = c;
                        zones.Add(zone);
                    }
                }
                obj.Dispose();
            }
            _temps = zones.ToArray();
            AssignZones(zones, ref _wmiCpu, ref _wmiGpu, ref _wmiMem);
        }
        catch
        {
        }
        finally
        {
            if (searcher != null)
                searcher.Dispose();
        }
    }

    private void LoadThermal()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("Thermal Zone Information"))
                return;
            string[] names = new PerformanceCounterCategory("Thermal Zone Information").GetInstanceNames();
            List<PerformanceCounter> counters = new List<PerformanceCounter>();
            List<string> labels = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                PerformanceCounter counter = TryCounter("Thermal Zone Information", "Temperature", names[i]);
                if (counter == null)
                    continue;
                counters.Add(counter);
                labels.Add(names[i]);
            }
            _therm = counters.ToArray();
            _thermNames = labels.ToArray();
        }
        catch
        {
            _therm = null;
            _thermNames = new string[0];
        }
    }

    private void ReadThermal(Snapshot s)
    {
        if (_therm != null && _therm.Length > 0)
        {
            List<TempZone> live = new List<TempZone>();
            for (int i = 0; i < _therm.Length; i++)
            {
                try
                {
                    int c = ToCelsius(_therm[i].NextValue());
                    if (c < 0)
                        continue;
                    TempZone zone = new TempZone();
                    zone.Name = _thermNames[i];
                    zone.Celsius = c;
                    live.Add(zone);
                }
                catch
                {
                }
            }
            if (!_thermPrimed)
            {
                _thermPrimed = true;
                return;
            }
            int cpu = -1;
            int gpu = -1;
            int mem = -1;
            AssignZones(live, ref cpu, ref gpu, ref mem);
            ApplyTemp(s, cpu, gpu, mem);
            return;
        }
        ApplyTemp(s, _wmiCpu, _wmiGpu, _wmiMem);
    }

    private static void ApplyTemp(Snapshot s, int cpu, int gpu, int mem)
    {
        if (cpu >= 0)
        {
            s.CpuTempKnown = true;
            s.CpuCelsius = cpu;
        }
        if (gpu >= 0)
        {
            s.GpuTempKnown = true;
            s.GpuCelsius = gpu;
        }
        if (mem >= 0)
        {
            s.MemTempKnown = true;
            s.MemCelsius = mem;
        }
    }

    private static void AssignZones(List<TempZone> zones, ref int cpu, ref int gpu, ref int mem)
    {
        int unknown = -1;
        int unknownCount = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            int kind = ZoneKind(zones[i].Name);
            int c = zones[i].Celsius;
            if (kind == 1)
                cpu = c;
            else if (kind == 2)
                gpu = c;
            else if (kind == 3)
                mem = c;
            else
            {
                unknown = c;
                unknownCount++;
            }
        }
        if (cpu < 0 && unknownCount == 1)
            cpu = unknown;
    }

    private static int ZoneKind(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;
        string n = name.ToLowerInvariant();
        if (n.IndexOf("gpu") >= 0 || n.IndexOf("graphics") >= 0 || n.IndexOf("video") >= 0)
            return 2;
        if (n.IndexOf("memory") >= 0 || n.IndexOf("dimm") >= 0 || n.IndexOf("dram") >= 0)
            return 3;
        if (n.IndexOf("cpu") >= 0 || n.IndexOf("processor") >= 0 || n.IndexOf("package") >= 0 || n.IndexOf("core") >= 0)
            return 1;
        return 0;
    }

    private static int ToCelsius(float raw)
    {
        double v = raw;
        if (v > 200d)
            v = v - 273.15d;
        int c = (int)Math.Round(v);
        if (c < 1 || c > 125)
            return -1;
        return c;
    }

    private void EnsureDisk()
    {
        DisposeArray(_diskRead);
        DisposeArray(_diskWrite);
        DisposeArray(_diskIdle);
        DisposeArray(_diskTime);
        DisposeArray(_diskFree);
        string[] names = LogicalDisks();
        _diskRead = CountersFor("LogicalDisk", "Disk Read Bytes/sec", names);
        _diskWrite = CountersFor("LogicalDisk", "Disk Write Bytes/sec", names);
        _diskIdle = CountersFor("LogicalDisk", "% Idle Time", names);
        _diskTime = CountersFor("LogicalDisk", "% Disk Time", names);
        _diskFree = CountersFor("LogicalDisk", "Free Megabytes", names);
        _diskPrimed = false;
    }

    private static string[] LogicalDisks()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("LogicalDisk"))
                return new string[0];
            string[] names = new PerformanceCounterCategory("LogicalDisk").GetInstanceNames();
            List<string> list = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "_Total", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (names[i].IndexOf(':') < 0)
                    continue;
                list.Add(names[i]);
            }
            return list.ToArray();
        }
        catch
        {
            return new string[0];
        }
    }

    private static PerformanceCounter[] CountersFor(string category, string counter, string[] names)
    {
        List<PerformanceCounter> list = new List<PerformanceCounter>();
        for (int i = 0; i < names.Length; i++)
        {
            PerformanceCounter item = TryCounter(category, counter, names[i]);
            if (item != null)
                list.Add(item);
        }
        return list.ToArray();
    }

    private void ReadDisk(Snapshot s)
    {
        s.Drives = _drives;
        bool any = (_diskRead != null && _diskRead.Length > 0)
            || (_diskIdle != null && _diskIdle.Length > 0)
            || (_diskTime != null && _diskTime.Length > 0);
        if (!any && _volumeBytes <= 0)
            return;
        float read = SumCounters(_diskRead);
        float write = SumCounters(_diskWrite);
        float idle = AverageCounters(_diskIdle);
        float time = AverageCounters(_diskTime);
        float freeMb = SumCounters(_diskFree);
        if (!_diskPrimed)
        {
            _diskPrimed = true;
            return;
        }
        if (_diskIdle != null && _diskIdle.Length > 0)
        {
            float busy = 100f - idle;
            if (busy < 0f) busy = 0f;
            if (busy > 100f) busy = 100f;
            s.DiskKnown = true;
            s.DiskBusy = busy;
        }
        else if (_diskTime != null && _diskTime.Length > 0)
        {
            if (time < 0f) time = 0f;
            if (time > 100f) time = 100f;
            s.DiskKnown = true;
            s.DiskBusy = time;
        }
        s.DiskReadMBps = read / (1024f * 1024f);
        s.DiskWriteMBps = write / (1024f * 1024f);
        if (_volumeBytes > 0 && freeMb >= 0f && _diskFree != null && _diskFree.Length > 0)
        {
            long free = (long)freeMb * 1024L * 1024L;
            long used = _volumeBytes - free;
            if (used < 0) used = 0;
            s.DiskSpaceKnown = true;
            s.DiskTotal = _volumeBytes;
            s.DiskUsed = used;
        }
    }

    private static float SumCounters(PerformanceCounter[] counters)
    {
        if (counters == null)
            return 0f;
        float sum = 0f;
        for (int i = 0; i < counters.Length; i++)
        {
            try
            {
                sum += counters[i].NextValue();
            }
            catch
            {
            }
        }
        return sum;
    }

    private static float AverageCounters(PerformanceCounter[] counters)
    {
        if (counters == null || counters.Length == 0)
            return 0f;
        return SumCounters(counters) / counters.Length;
    }

    private void ReadDiskHardware()
    {
        _drives = ReadPhysicalDisks();
        _volumeBytes = ReadVolumeBytes();
    }

    private static DiskDrive[] ReadPhysicalDisks()
    {
        ManagementObjectSearcher searcher = null;
        List<DiskDrive> list = new List<DiskDrive>();
        try
        {
            searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive");
            searcher.Options.Timeout = new TimeSpan(0, 0, 2);
            foreach (ManagementObject obj in searcher.Get())
            {
                DiskDrive drive = new DiskDrive();
                object model = obj["Model"];
                drive.Name = model == null ? "Disk" : model.ToString().Trim();
                object size = obj["Size"];
                if (size != null)
                    drive.Bytes = Convert.ToInt64(size);
                object media = obj["MediaType"];
                drive.Media = media == null ? "" : media.ToString().Trim();
                list.Add(drive);
                obj.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            if (searcher != null)
                searcher.Dispose();
        }
        return list.ToArray();
    }

    private static long ReadVolumeBytes()
    {
        ManagementObjectSearcher searcher = null;
        long total = 0;
        try
        {
            searcher = new ManagementObjectSearcher("SELECT Size, DriveType FROM Win32_LogicalDisk");
            searcher.Options.Timeout = new TimeSpan(0, 0, 2);
            foreach (ManagementObject obj in searcher.Get())
            {
                object kind = obj["DriveType"];
                object size = obj["Size"];
                if (kind != null && Convert.ToInt32(kind) == 3 && size != null)
                    total += Convert.ToInt64(size);
                obj.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            if (searcher != null)
                searcher.Dispose();
        }
        return total;
    }

    private void ReadCpu(Snapshot s)
    {
        if (_cpu == null)
        {
            _cpuPrimed = true;
            return;
        }
        try
        {
            float v = _cpu.NextValue();
            if (!_cpuPrimed)
            {
                _cpuPrimed = true;
                s.CpuKnown = false;
                return;
            }
            if (v < 0f) v = 0f;
            if (v > 100f) v = 100f;
            s.CpuKnown = true;
            s.CpuPercent = v;
        }
        catch
        {
            s.CpuKnown = false;
        }
    }

    private void ReadClocks(Snapshot s)
    {
        if (_clock != null)
        {
            try
            {
                float mhz = _clock.NextValue();
                if (mhz > 0f)
                {
                    s.ClockKnown = true;
                    s.ClockMHz = (int)mhz;
                }
            }
            catch
            {
                s.ClockKnown = false;
            }
        }
        if (_cores.Length == 0)
            return;
        int[] mhzs = new int[_cores.Length];
        int n = 0;
        for (int i = 0; i < _cores.Length; i++)
        {
            try
            {
                float v = _cores[i].Counter.NextValue();
                if (v > 0f)
                {
                    mhzs[n] = (int)v;
                    n++;
                }
            }
            catch
            {
            }
        }
        if (n == 0)
            return;
        if (n == mhzs.Length)
        {
            s.CoreMHz = mhzs;
            return;
        }
        int[] trimmed = new int[n];
        for (int i = 0; i < n; i++)
            trimmed[i] = mhzs[i];
        s.CoreMHz = trimmed;
    }

    private static void ReadMemory(Snapshot s)
    {
        MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
        mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        if (!GlobalMemoryStatusEx(ref mem))
            return;
        s.MemoryTotal = (long)mem.ullTotalPhys;
        long avail = (long)mem.ullAvailPhys;
        s.MemoryUsed = s.MemoryTotal - avail;
        if (s.MemoryUsed < 0)
            s.MemoryUsed = 0;
    }

    private void ReadMemoryFacts(Snapshot s)
    {
        long committed = ReadLong(_committed);
        long limit = ReadLong(_commitLimit);
        if (committed > 0 && limit > 0)
        {
            s.CommitKnown = true;
            s.Committed = committed;
            s.CommitLimit = limit;
        }
        long cached = 0;
        bool anyCache = false;
        anyCache |= Add(ref cached, _modified);
        anyCache |= Add(ref cached, _standbyNormal);
        anyCache |= Add(ref cached, _standbyReserve);
        anyCache |= Add(ref cached, _standbyCore);
        if (anyCache)
        {
            s.CachedKnown = true;
            s.Cached = cached;
        }
        long paged = ReadLong(_paged);
        long nonPaged = ReadLong(_nonPaged);
        if (paged > 0 || nonPaged > 0)
        {
            s.PoolKnown = true;
            s.PagedPool = paged;
            s.NonPagedPool = nonPaged;
        }
    }

    private static bool Add(ref long sum, PerformanceCounter counter)
    {
        long v = ReadLong(counter);
        if (v <= 0)
            return false;
        sum += v;
        return true;
    }

    private static long ReadLong(PerformanceCounter counter)
    {
        if (counter == null)
            return 0;
        try
        {
            float v = counter.NextValue();
            if (v <= 0f)
                return 0;
            return (long)v;
        }
        catch
        {
            return 0;
        }
    }

    private void ReadGpu(Snapshot s)
    {
        if (_gpuUtils == null || _gpuUtils.Length == 0)
        {
            _gpuRefresh++;
            if (_gpuUtils == null || _gpuRefresh >= 10)
            {
                _gpuRefresh = 0;
                EnsureGpu();
            }
        }
        if (_gpuUtils == null || _gpuUtils.Length == 0)
        {
            s.GpuKnown = false;
            return;
        }
        float max = 0f;
        bool any = false;
        for (int i = 0; i < _gpuUtils.Length; i++)
        {
            try
            {
                float v = _gpuUtils[i].NextValue();
                if (v < 0f) v = 0f;
                if (v > max) max = v;
                any = true;
            }
            catch
            {
            }
        }
        if (!any)
        {
            s.GpuKnown = false;
            return;
        }
        if (!_gpuPrimed)
        {
            _gpuPrimed = true;
            s.GpuKnown = false;
            return;
        }
        if (max > 100f) max = 100f;
        s.GpuKnown = true;
        s.GpuPercent = max;
        ReadGpuMemory(s, _gpuMems, true);
        ReadGpuMemory(s, _gpuShared, false);
    }

    private string _ssidCache = "";
    private int _ssidTick;

    private void ReadNet(Snapshot s)
    {
        s.WifiUp = WirelessUp();
        if (s.WifiUp)
        {
            _ssidTick++;
            if (_ssidTick >= 3 || string.IsNullOrEmpty(_ssidCache))
            {
                _ssidCache = WlanSsid.TryRead();
                _ssidTick = 0;
            }
            s.WifiSsid = _ssidCache ?? "";
        }
        else
        {
            _ssidCache = "";
            _ssidTick = 0;
            s.WifiSsid = "";
        }
        if (_netIn == null || _netIn.Length == 0)
        {
            _netRefresh++;
            if (_netIn == null || _netRefresh >= 10)
            {
                _netRefresh = 0;
                LoadNet();
                _netPrimed = false;
            }
        }
        if (_netIn == null || _netIn.Length == 0)
        {
            s.NetKnown = false;
            return;
        }
        float down = SumRates(_netIn);
        float up = SumRates(_netOut);
        if (!_netPrimed)
        {
            _netPrimed = true;
            s.NetKnown = false;
            return;
        }
        s.NetKnown = true;
        s.DownMBps = down / 1048576f;
        s.UpMBps = up / 1048576f;
    }

    private static float SumRates(PerformanceCounter[] counters)
    {
        if (counters == null)
            return 0f;
        float sum = 0f;
        for (int i = 0; i < counters.Length; i++)
        {
            try
            {
                float v = counters[i].NextValue();
                if (v > 0f)
                    sum += v;
            }
            catch
            {
            }
        }
        return sum;
    }

    private void LoadNet()
    {
        DisposeArray(_netIn);
        DisposeArray(_netOut);
        List<string> names = UpAdapterKeys();
        List<PerformanceCounter> inn = new List<PerformanceCounter>();
        List<PerformanceCounter> outn = new List<PerformanceCounter>();
        try
        {
            if (!PerformanceCounterCategory.Exists("Network Interface"))
            {
                _netIn = new PerformanceCounter[0];
                _netOut = new PerformanceCounter[0];
                return;
            }
            string[] instances = new PerformanceCounterCategory("Network Interface").GetInstanceNames();
            for (int i = 0; i < instances.Length; i++)
            {
                if (SkippedAdapter(instances[i]))
                    continue;
                if (!MatchesUp(instances[i], names))
                    continue;
                PerformanceCounter recv = TryCounter("Network Interface", "Bytes Received/sec", instances[i]);
                PerformanceCounter sent = TryCounter("Network Interface", "Bytes Sent/sec", instances[i]);
                if (recv != null)
                    inn.Add(recv);
                if (sent != null)
                    outn.Add(sent);
            }
        }
        catch
        {
        }
        _netIn = inn.ToArray();
        _netOut = outn.ToArray();
    }

    private static bool WirelessUp()
    {
        try
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < nics.Length; i++)
            {
                if (nics[i].NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    && nics[i].OperationalStatus == OperationalStatus.Up)
                    return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static List<string> UpAdapterKeys()
    {
        List<string> keys = new List<string>();
        try
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < nics.Length; i++)
            {
                NetworkInterface nic = nics[i];
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;
                string blob = nic.Name + " " + nic.Description;
                if (SkippedAdapter(blob))
                    continue;
                keys.Add(Compact(nic.Description));
                keys.Add(Compact(nic.Name));
            }
        }
        catch
        {
        }
        return keys;
    }

    private static bool MatchesUp(string instance, List<string> keys)
    {
        string compact = Compact(instance);
        if (compact.Length < 4)
            return false;
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (key.Length < 4)
                continue;
            if (compact.IndexOf(key, StringComparison.Ordinal) >= 0)
                return true;
            if (key.IndexOf(compact, StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static bool SkippedAdapter(string name)
    {
        if (string.IsNullOrEmpty(name))
            return true;
        string lower = name.ToLowerInvariant();
        return lower.IndexOf("loopback") >= 0
            || lower.IndexOf("isatap") >= 0
            || lower.IndexOf("teredo") >= 0
            || lower.IndexOf("pseudo") >= 0
            || lower.IndexOf("virtual") >= 0
            || lower.IndexOf("hyper-v") >= 0
            || lower.IndexOf("vethernet") >= 0
            || lower.IndexOf("bluetooth") >= 0
            || lower.IndexOf("wan miniport") >= 0;
    }

    private static string Compact(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static void ReadGpuMemory(Snapshot s, PerformanceCounter[] counters, bool dedicated)
    {
        if (counters == null || counters.Length == 0)
            return;
        long sum = 0;
        bool any = false;
        for (int i = 0; i < counters.Length; i++)
        {
            try
            {
                float v = counters[i].NextValue();
                if (v > 0f)
                    sum += (long)v;
                any = true;
            }
            catch
            {
            }
        }
        if (!any)
            return;
        if (dedicated)
        {
            s.GpuMemoryKnown = true;
            s.GpuMemoryUsed = sum;
        }
        else
        {
            s.GpuSharedKnown = true;
            s.GpuShared = sum;
        }
    }

    private void EnsureGpu()
    {
        DisposeArray(_gpuUtils);
        DisposeArray(_gpuMems);
        DisposeArray(_gpuShared);
        _gpuUtils = LoadGpu("GPU Engine", "Utilization Percentage", "engtype_3D", 512);
        _gpuMems = LoadGpu("GPU Adapter Memory", "Dedicated Usage", null, 8);
        _gpuShared = LoadGpu("GPU Adapter Memory", "Shared Usage", null, 8);
        _gpuPrimed = false;
    }

    private static PerformanceCounter[] LoadGpu(string category, string counter, string mustContain, int max)
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(category))
                return new PerformanceCounter[0];
            string[] names = new PerformanceCounterCategory(category).GetInstanceNames();
            List<PerformanceCounter> list = new List<PerformanceCounter>();
            for (int i = 0; i < names.Length && list.Count < max; i++)
            {
                if (mustContain != null && names[i].IndexOf(mustContain, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                PerformanceCounter c = TryCounter(category, counter, names[i]);
                if (c != null)
                    list.Add(c);
            }
            return list.ToArray();
        }
        catch
        {
            return new PerformanceCounter[0];
        }
    }

    private static void ReadBattery(Snapshot s)
    {
        SYSTEM_POWER_STATUS status;
        if (!GetSystemPowerStatus(out status))
            return;
        if ((status.BatteryFlag & 128) != 0)
            return;
        if (status.BatteryLifePercent == 255)
            return;
        s.BatteryKnown = true;
        s.BatteryPercent = status.BatteryLifePercent;
        s.Charging = (status.BatteryFlag & 8) != 0;
        if (status.ACLineStatus != 255)
        {
            s.AcKnown = true;
            s.PluggedIn = status.ACLineStatus == 1;
        }
    }

    private static string ShortZone(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "Zone";
        int slash = raw.LastIndexOf('\\');
        string name = slash >= 0 && slash < raw.Length - 1 ? raw.Substring(slash + 1) : raw;
        if (name.Length == 0)
            return "Zone";
        return name;
    }

    private void ReadProcesses(Snapshot s)
    {
        DateTime now = DateTime.UtcNow;
        double wall = _lastWall == DateTime.MinValue ? 0 : (now - _lastWall).TotalSeconds;
        _lastWall = now;
        int cores = Environment.ProcessorCount;
        if (cores < 1) cores = 1;

        Dictionary<string, Acc> map = ReadPrivateSets();
        Dictionary<int, TimeSpan> nextCpu = new Dictionary<int, TimeSpan>();
        Process[] all = null;
        try
        {
            all = Process.GetProcesses();
        }
        catch
        {
            all = null;
        }
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    string name = p.ProcessName;
                    if (IsSkipped(name))
                        continue;
                    TimeSpan cpu = p.TotalProcessorTime;
                    nextCpu[p.Id] = cpu;
                    float cpuPct = 0f;
                    TimeSpan prev;
                    if (wall > 0.2 && _lastCpu.TryGetValue(p.Id, out prev))
                    {
                        double used = (cpu - prev).TotalSeconds;
                        cpuPct = (float)(used / wall / cores * 100.0);
                        if (cpuPct < 0f) cpuPct = 0f;
                        if (cpuPct > 100f) cpuPct = 100f;
                    }
                    Acc acc;
                    if (!map.TryGetValue(name, out acc))
                    {
                        acc = new Acc();
                        acc.Name = name;
                        map[name] = acc;
                        acc.Count++;
                    }
                    else if (!acc.Counted)
                        acc.Count++;
                    acc.Cpu += cpuPct;
                }
                catch
                {
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        _lastCpu.Clear();
        foreach (KeyValuePair<int, TimeSpan> pair in nextCpu)
            _lastCpu[pair.Key] = pair.Value;

        List<AppUse> rows = new List<AppUse>(map.Count);
        foreach (Acc acc in map.Values)
        {
            if (acc.Memory <= 0 && acc.Cpu <= 0f)
                continue;
            AppUse row = new AppUse();
            row.Name = acc.Name;
            row.Count = acc.Count < 1 ? 1 : acc.Count;
            row.MemoryBytes = acc.Memory;
            row.CpuPercent = acc.Cpu;
            rows.Add(row);
        }
        s.ByMemory = Top(rows, true, rows.Count);
        s.ByCpu = Top(rows, false, rows.Count);
    }

    private static Dictionary<string, Acc> ReadPrivateSets()
    {
        Dictionary<string, Acc> map = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);
        try
        {
            PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");
            InstanceDataCollectionCollection data = cat.ReadCategory();
            InstanceDataCollection col = FindCounter(data, "Working Set - Private");
            if (col == null)
                return map;
            foreach (DictionaryEntry entry in col)
            {
                string inst = (string)entry.Key;
                if (string.IsNullOrEmpty(inst))
                    continue;
                if (string.Equals(inst, "_Total", StringComparison.OrdinalIgnoreCase))
                    continue;
                int hash = inst.IndexOf('#');
                string raw = hash > 0 ? inst.Substring(0, hash) : inst;
                if (IsSkipped(raw))
                    continue;
                InstanceData sample = (InstanceData)entry.Value;
                long bytes = sample.RawValue;
                if (bytes < 0)
                    bytes = 0;
                Acc acc;
                if (!map.TryGetValue(raw, out acc))
                {
                    acc = new Acc();
                    acc.Name = raw;
                    acc.Counted = true;
                    map[raw] = acc;
                }
                acc.Count++;
                acc.Memory += bytes;
            }
        }
        catch
        {
        }
        return map;
    }

    private static InstanceDataCollection FindCounter(InstanceDataCollectionCollection data, string name)
    {
        foreach (DictionaryEntry entry in data)
        {
            string key = (string)entry.Key;
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return (InstanceDataCollection)entry.Value;
        }
        return null;
    }

    private static bool IsSkipped(string name)
    {
        return string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "System", StringComparison.OrdinalIgnoreCase);
    }

    private static AppUse[] Top(List<AppUse> rows, bool byMemory, int count)
    {
        rows.Sort(delegate(AppUse a, AppUse b)
        {
            if (byMemory)
                return b.MemoryBytes.CompareTo(a.MemoryBytes);
            return b.CpuPercent.CompareTo(a.CpuPercent);
        });
        if (rows.Count < count)
            count = rows.Count;
        AppUse[] top = new AppUse[count];
        for (int i = 0; i < count; i++)
            top[i] = rows[i];
        return top;
    }

    private void PushHistory(Snapshot s)
    {
        _cpuHist[_histPos] = s.CpuKnown ? s.CpuPercent : 0f;
        float memPct = 0f;
        if (s.MemoryTotal > 0)
            memPct = (float)(s.MemoryUsed * 100.0 / s.MemoryTotal);
        _memHist[_histPos] = memPct;
        _gpuHist[_histPos] = s.GpuKnown ? s.GpuPercent : 0f;
        _diskHist[_histPos] = s.DiskKnown ? s.DiskBusy : 0f;
        _histPos++;
        if (_histPos >= History)
            _histPos = 0;
        if (_histCount < History)
            _histCount++;
        s.CpuHistory = s.CpuKnown ? CopyRing(_cpuHist, _histPos, _histCount) : new float[0];
        s.MemoryHistory = CopyRing(_memHist, _histPos, _histCount);
        s.GpuHistory = s.GpuKnown ? CopyRing(_gpuHist, _histPos, _histCount) : new float[0];
        s.DiskHistory = s.DiskKnown ? CopyRing(_diskHist, _histPos, _histCount) : new float[0];
    }

    private static float[] CopyRing(float[] ring, int pos, int count)
    {
        float[] copy = new float[count];
        int start = pos - count;
        if (start < 0)
            start += ring.Length;
        for (int i = 0; i < count; i++)
        {
            int idx = start + i;
            if (idx >= ring.Length)
                idx -= ring.Length;
            copy[i] = ring[idx];
        }
        return copy;
    }

    private void DropCounters()
    {
        DisposeCounter(_cpu);
        DisposeCounter(_clock);
        DisposeCounter(_committed);
        DisposeCounter(_commitLimit);
        DisposeCounter(_modified);
        DisposeCounter(_standbyNormal);
        DisposeCounter(_standbyReserve);
        DisposeCounter(_standbyCore);
        DisposeCounter(_paged);
        DisposeCounter(_nonPaged);
        _cpu = null;
        _clock = null;
        _committed = null;
        _commitLimit = null;
        _modified = null;
        _standbyNormal = null;
        _standbyReserve = null;
        _standbyCore = null;
        _paged = null;
        _nonPaged = null;
        if (_cores != null)
        {
            for (int i = 0; i < _cores.Length; i++)
                DisposeCounter(_cores[i].Counter);
        }
        _cores = new CoreSlot[0];
        DisposeArray(_gpuUtils);
        DisposeArray(_gpuMems);
        DisposeArray(_gpuShared);
        DisposeArray(_netIn);
        DisposeArray(_netOut);
        DisposeArray(_therm);
        DisposeArray(_diskRead);
        DisposeArray(_diskWrite);
        DisposeArray(_diskIdle);
        DisposeArray(_diskTime);
        DisposeArray(_diskFree);
        _gpuUtils = null;
        _gpuMems = null;
        _gpuShared = null;
        _netIn = null;
        _netOut = null;
    }

    private static void DisposeCounter(PerformanceCounter counter)
    {
        if (counter != null)
            counter.Dispose();
    }

    private static void DisposeArray(PerformanceCounter[] items)
    {
        if (items == null)
            return;
        for (int i = 0; i < items.Length; i++)
            DisposeCounter(items[i]);
    }

    private sealed class Acc
    {
        public string Name;
        public int Count;
        public bool Counted;
        public long Memory;
        public float Cpu;
    }

    private sealed class CoreSlot
    {
        public int Index;
        public PerformanceCounter Counter;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
