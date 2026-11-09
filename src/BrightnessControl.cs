using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class BrightnessControl : IDisposable
{
    private readonly List<BrightnessMonitor> _items = new List<BrightnessMonitor>();
    private readonly List<IntPtr> _owned = new List<IntPtr>();
    private List<string> _edidNames;

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

    /// <summary>
    /// Identified (plugged) displays in enumeration order, capped at <paramref name="max"/>.
    /// Controllable is orthogonal — dual chrome may include non-controllable rows.
    /// </summary>
    public void CopyPresent(BrightnessMonitor[] dest, int max, out int count)
    {
        count = 0;
        if (dest == null || max < 1)
            return;
        if (max > dest.Length)
            max = dest.Length;
        for (int i = 0; i < _items.Count && count < max; i++)
            dest[count++] = _items[i];
    }

    /// <summary>Backward-compatible alias: controllable-only copy (tests / callers that need live rows).</summary>
    public void CopyUsable(BrightnessMonitor[] dest, int max, out int count)
    {
        count = 0;
        if (dest == null || max < 1)
            return;
        if (max > dest.Length)
            max = dest.Length;
        for (int i = 0; i < _items.Count && count < max; i++)
        {
            if (!_items[i].Controllable)
                continue;
            dest[count++] = _items[i];
        }
    }

    public void Refresh()
    {
        DisposeOwned();
        _items.Clear();
        _edidNames = null;
        Status = "";
        try
        {
            PrefetchEdidNames();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);
            TryApplyWmiFallback();
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
            string deviceName;
            bool primary;
            int resW, resH;
            ReadMonitorGeometry(hMonitor, out deviceName, out primary, out resW, out resH);
            string resolution = BrightnessChrome.FormatResolution(resW, resH);
            string deviceString = TryMonitorDeviceString(deviceName);

            uint count = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref count) || count == 0)
            {
                // Still identify the HMONITOR so dual chrome can show brand/resolution.
                AddIdentifiedMonitor(
                    "mon:" + hMonitor.ToInt64().ToString("X"),
                    BrightnessChrome.PreferDisplayName("", deviceString, TakeEdidName()),
                    resolution,
                    primary,
                    false,
                    false,
                    IntPtr.Zero,
                    "Brightness not available on this display");
                return true;
            }
            PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, arr))
            {
                AddIdentifiedMonitor(
                    "mon:" + hMonitor.ToInt64().ToString("X"),
                    BrightnessChrome.PreferDisplayName("", deviceString, TakeEdidName()),
                    resolution,
                    primary,
                    false,
                    false,
                    IntPtr.Zero,
                    "Brightness not available on this display");
                return true;
            }
            for (int i = 0; i < arr.Length; i++)
            {
                IntPtr phys = arr[i].hPhysicalMonitor;
                _owned.Add(phys);
                uint min = 0, cur = 0, max = 0;
                bool ok = GetMonitorBrightness(phys, ref min, ref cur, ref max) && max > min;
                string physical = arr[i].szPhysicalMonitorDescription;
                string name = BrightnessChrome.PreferDisplayName(physical, deviceString, TakeEdidName());
                BrightnessMonitor m = new BrightnessMonitor();
                m.Id = "ddc:" + hMonitor.ToInt64().ToString("X") + ":" + i.ToString();
                m.Name = Truncate(name, 28);
                m.Resolution = resolution;
                m.Physical = phys;
                m.ViaWmi = false;
                m.IsPrimary = primary && i == 0;
                m.Controllable = ok;
                if (ok)
                {
                    m.Percent = (int)Math.Round(100.0 * (cur - min) / (max - min));
                    m.Status = "";
                }
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

    private void AddIdentifiedMonitor(
        string id,
        string name,
        string resolution,
        bool primary,
        bool controllable,
        bool viaWmi,
        IntPtr physical,
        string status)
    {
        BrightnessMonitor m = new BrightnessMonitor();
        m.Id = id;
        m.Name = Truncate(name, 28);
        m.Resolution = resolution ?? "";
        m.IsPrimary = primary;
        m.Controllable = controllable;
        m.ViaWmi = viaWmi;
        m.Physical = physical;
        m.Percent = 0;
        m.Status = status ?? "";
        _items.Add(m);
    }

    /// <summary>
    /// When no DDC row is live, attach WMI brightness to the primary identified display
    /// instead of appending a duplicate "Built-in" row (keeps dual chrome accurate).
    /// </summary>
    private void TryApplyWmiFallback()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Controllable)
                return;
        }
        try
        {
            int pct;
            if (!TryGetWmiBrightness(out pct))
                return;
            BrightnessMonitor target = null;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsPrimary)
                {
                    target = _items[i];
                    break;
                }
            }
            if (target == null && _items.Count > 0)
                target = _items[0];
            if (target != null)
            {
                target.Controllable = true;
                target.ViaWmi = true;
                target.Percent = pct;
                target.Status = "";
                if (BrightnessChrome.IsGenericMonitorName(target.Name))
                    target.Name = "Built-in display";
                return;
            }
            BrightnessMonitor m = new BrightnessMonitor();
            m.Id = "wmi:internal";
            m.Name = "Built-in display";
            m.Resolution = "";
            m.Percent = pct;
            m.Controllable = true;
            m.ViaWmi = true;
            m.IsPrimary = true;
            m.Physical = IntPtr.Zero;
            _items.Add(m);
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
    }

    private void PrefetchEdidNames()
    {
        _edidNames = new List<string>();
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT UserFriendlyName, Active FROM WmiMonitorID"))
            using (ManagementObjectCollection list = searcher.Get())
            {
                foreach (ManagementObject row in list)
                {
                    using (row)
                    {
                        object active = row["Active"];
                        if (active is bool && !(bool)active)
                            continue;
                        string name = DecodeUshortString(row["UserFriendlyName"] as ushort[]);
                        if (string.IsNullOrEmpty(name))
                            continue;
                        _edidNames.Add(name);
                    }
                }
            }
        }
        catch
        {
            // EDID names are best-effort; geometry + device string still work.
        }
    }

    private string TakeEdidName()
    {
        if (_edidNames == null || _edidNames.Count == 0)
            return "";
        string name = _edidNames[0];
        _edidNames.RemoveAt(0);
        return name;
    }

    private static string DecodeUshortString(ushort[] data)
    {
        if (data == null || data.Length == 0)
            return "";
        StringBuilder sb = new StringBuilder(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0)
                break;
            sb.Append((char)data[i]);
        }
        return sb.ToString().Trim();
    }

    private static void ReadMonitorGeometry(
        IntPtr hMonitor,
        out string deviceName,
        out bool primary,
        out int width,
        out int height)
    {
        deviceName = "";
        primary = false;
        width = 0;
        height = 0;
        MONITORINFOEX info = new MONITORINFOEX();
        info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
        if (!GetMonitorInfo(hMonitor, ref info))
            return;
        deviceName = info.szDevice ?? "";
        primary = (info.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
        width = info.rcMonitor.Right - info.rcMonitor.Left;
        height = info.rcMonitor.Bottom - info.rcMonitor.Top;
        DEVMODE mode = new DEVMODE();
        mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        if (!string.IsNullOrEmpty(deviceName)
            && EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref mode)
            && mode.dmPelsWidth > 0
            && mode.dmPelsHeight > 0)
        {
            width = mode.dmPelsWidth;
            height = mode.dmPelsHeight;
        }
    }

    private static string TryMonitorDeviceString(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
            return "";
        try
        {
            DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            // DeviceName is the adapter (\\.\DISPLAYn); index 0 is the attached monitor.
            if (EnumDisplayDevices(deviceName, 0, ref dd, 0))
                return dd.DeviceString ?? "";
        }
        catch
        {
        }
        return "";
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

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }
}
