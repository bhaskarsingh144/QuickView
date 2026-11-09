using System;

internal sealed class BrightnessMonitor
{
    public string Id;
    public string Name;
    /// <summary>Current mode label like 2560×1440; empty when unknown.</summary>
    public string Resolution;
    public int Percent;
    public bool Controllable;
    public bool ViaWmi;
    public bool IsPrimary;
    public IntPtr Physical;
    public string Status;
}

/// <summary>
/// Pure layout/label rules for main-panel brightness chrome.
/// Controllable gates interaction; present/identified displays gate dual rows.
/// </summary>
internal static class BrightnessChrome
{
    public enum LayoutKind
    {
        None,
        Single,
        Dual
    }

    /// <summary>
    /// Dual when two identified displays exist (even if one/both lack software brightness).
    /// Single only when exactly one display is present and controllable (classic unnamed strip).
    /// </summary>
    public static LayoutKind Resolve(
        BrightnessMonitor[] present,
        int count,
        out BrightnessMonitor a,
        out BrightnessMonitor b)
    {
        a = null;
        b = null;
        if (present == null || count <= 0)
            return LayoutKind.None;
        if (count >= 2)
        {
            a = present[0];
            b = present[1];
            return LayoutKind.Dual;
        }
        if (present[0] != null && present[0].Controllable)
        {
            a = present[0];
            return LayoutKind.Single;
        }
        return LayoutKind.None;
    }

    /// <summary>Right-side dual label: percent when live, resolution when software brightness is unavailable.</summary>
    public static string DualRightLabel(BrightnessMonitor mon)
    {
        if (mon == null)
            return "";
        if (mon.Controllable)
            return mon.Percent.ToString();
        if (!string.IsNullOrEmpty(mon.Resolution))
            return mon.Resolution;
        return "—";
    }

    public static string FormatResolution(int width, int height)
    {
        if (width < 1 || height < 1)
            return "";
        return width.ToString() + "×" + height.ToString();
    }

    public static bool IsGenericMonitorName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return true;
        string n = name.Trim();
        if (n.Length == 0)
            return true;
        if (string.Equals(n, "Display", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(n, "Built-in display", StringComparison.OrdinalIgnoreCase))
            return false;
        if (n.IndexOf("Generic", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (n.IndexOf("PnP Monitor", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (n.StartsWith("Default_", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Pick the best human label: EDID/friendly → device string → physical → Display.</summary>
    public static string PreferDisplayName(string physical, string deviceString, string edidFriendly)
    {
        if (!IsGenericMonitorName(edidFriendly))
            return edidFriendly.Trim();
        if (!IsGenericMonitorName(deviceString))
            return deviceString.Trim();
        if (!IsGenericMonitorName(physical))
            return physical.Trim();
        if (!string.IsNullOrEmpty(edidFriendly) && edidFriendly.Trim().Length > 0)
            return edidFriendly.Trim();
        if (!string.IsNullOrEmpty(deviceString) && deviceString.Trim().Length > 0)
            return deviceString.Trim();
        if (!string.IsNullOrEmpty(physical) && physical.Trim().Length > 0)
            return physical.Trim();
        return "Display";
    }
}
