using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class BrightnessMonitor
{
    public string Id;
    public string Name;
    public int Percent;
    public bool Controllable;
    public bool ViaWmi;
    public IntPtr Physical;
    public string Status;
}

internal sealed class BrightnessControl : IDisposable
{
    private readonly List<BrightnessMonitor> _items = new List<BrightnessMonitor>();
    private readonly List<IntPtr> _owned = new List<IntPtr>();

    public IList<BrightnessMonitor> Items
    {
        get { return _items; }
    }

    public string Status { get; private set; }

    public BrightnessControl()
    {
        Status = "";
        Refresh();
    }

    public void Refresh()
    {
        DisposeOwned();
        _items.Clear();
        Status = "";
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);
            TryAddWmiFallback();
            if (_items.Count == 0)
                Status = "Brightness not available on this display";
        }
        catch (Exception ex)
        {
            Status = "Brightness unavailable";
            Program.LogCrash(ex);
        }
    }

    public bool TrySet(string id, int percent)
    {
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;
        for (int i = 0; i < _items.Count; i++)
        {
            BrightnessMonitor m = _items[i];
            if (!string.Equals(m.Id, id, StringComparison.Ordinal))
                continue;
            if (!m.Controllable)
                return false;
            try
            {
                if (m.ViaWmi)
                {
                    if (!SetWmiBrightness(percent))
                    {
                        m.Status = "Brightness set failed";
                        return false;
                    }
                }
                else
                {
                    uint min = 0, cur = 0, max = 0;
                    if (!GetMonitorBrightness(m.Physical, ref min, ref cur, ref max) || max <= min)
                    {
                        m.Status = "Brightness set failed";
                        return false;
                    }
                    uint value = min + (uint)((max - min) * (percent / 100.0));
                    if (!SetMonitorBrightness(m.Physical, value))
                    {
                        m.Status = "Brightness set failed";
                        return false;
                    }
                }
                m.Percent = percent;
                m.Status = "";
                return true;
            }
            catch (Exception ex)
            {
                m.Status = "Brightness set failed";
                Program.LogCrash(ex);
                return false;
            }
        }
        return false;
    }

    private bool MonitorEnum(IntPtr hMonitor, IntPtr hdc, IntPtr lprc, IntPtr dwData)
    {
        try
        {
            uint count = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref count) || count == 0)
                return true;
            PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, arr))
                return true;
            for (int i = 0; i < arr.Length; i++)
            {
                IntPtr phys = arr[i].hPhysicalMonitor;
                _owned.Add(phys);
                uint min = 0, cur = 0, max = 0;
                bool ok = GetMonitorBrightness(phys, ref min, ref cur, ref max) && max > min;
                string name = arr[i].szPhysicalMonitorDescription;
                if (string.IsNullOrEmpty(name))
                    name = "Display";
                BrightnessMonitor m = new BrightnessMonitor();
                m.Id = "ddc:" + hMonitor.ToInt64().ToString("X") + ":" + i.ToString();
                m.Name = Truncate(name, 28);
                m.Physical = phys;
                m.ViaWmi = false;
                m.Controllable = ok;
                if (ok)
                    m.Percent = (int)Math.Round(100.0 * (cur - min) / (max - min));
                else
                {
                    m.Percent = 0;
                    m.Status = "Brightness not available on this display";
                }
                _items.Add(m);
            }
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
        return true;
    }

    private void TryAddWmiFallback()
    {
        bool anyControllable = false;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Controllable)
                anyControllable = true;
        }
        if (anyControllable)
            return;
        try
        {
            int pct;
            if (!TryGetWmiBrightness(out pct))
                return;
            BrightnessMonitor m = new BrightnessMonitor();
            m.Id = "wmi:internal";
            m.Name = "Built-in display";
            m.Percent = pct;
            m.Controllable = true;
            m.ViaWmi = true;
            m.Physical = IntPtr.Zero;
            _items.Add(m);
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
    }

    private static bool TryGetWmiBrightness(out int percent)
    {
        percent = 0;
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
            "root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
        using (ManagementObjectCollection list = searcher.Get())
        {
            foreach (ManagementObject row in list)
            {
                using (row)
                {
                    object v = row["CurrentBrightness"];
                    if (v == null)
                        continue;
                    percent = Convert.ToInt32(v);
                    return true;
                }
            }
        }
        return false;
    }

    private static bool SetWmiBrightness(int percent)
    {
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
            "root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
        using (ManagementObjectCollection list = searcher.Get())
        {
            foreach (ManagementObject row in list)
            {
                using (row)
                {
                    row.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)percent });
                    return true;
                }
            }
        }
        return false;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s ?? "";
        return s.Substring(0, max - 1) + "…";
    }

    public void Dispose()
    {
        DisposeOwned();
        _items.Clear();
    }

    private void DisposeOwned()
    {
        if (_owned.Count == 0)
            return;
        try
        {
            PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[_owned.Count];
            for (int i = 0; i < _owned.Count; i++)
            {
                arr[i].hPhysicalMonitor = _owned[i];
                arr[i].szPhysicalMonitorDescription = "";
            }
            DestroyPhysicalMonitors((uint)_owned.Count, arr);
        }
        catch
        {
        }
        _owned.Clear();
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hPhysicalMonitor, ref uint pdwMinimumBrightness, ref uint pdwCurrentBrightness, ref uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hPhysicalMonitor, uint dwNewBrightness);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }
}
