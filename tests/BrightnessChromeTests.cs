using System;

/// <summary>
/// Layout/label rules for dual brightness: identified displays show even without DDC/WMI control.
/// </summary>
internal static class BrightnessChromeTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        BrightnessMonitor live;
        BrightnessMonitor frost;
        BrightnessChrome.LayoutKind kind;

        kind = BrightnessChrome.Resolve(null, 0, out live, out frost);
        Expect(BrightnessChrome.LayoutKind.None, kind, "null present → None");

        BrightnessMonitor[] oneLive = new BrightnessMonitor[]
        {
            Mon("a", "Laptop", "1920×1200", 55, true)
        };
        kind = BrightnessChrome.Resolve(oneLive, 1, out live, out frost);
        Expect(BrightnessChrome.LayoutKind.Single, kind, "one controllable → Single");
        ExpectTrue(live != null && live.Id == "a", "single picks the live monitor");
        ExpectTrue(frost == null, "single has no second row");

        BrightnessMonitor[] oneFrost = new BrightnessMonitor[]
        {
            Mon("x", "Dell", "2560×1440", 0, false)
        };
        kind = BrightnessChrome.Resolve(oneFrost, 1, out live, out frost);
        Expect(BrightnessChrome.LayoutKind.None, kind, "one non-controllable alone → omit strip");

        BrightnessMonitor[] mixed = new BrightnessMonitor[]
        {
            Mon("lap", "Built-in display", "1920×1200", 70, true),
            Mon("ext", "DELL S2721QS", "2560×1440", 0, false)
        };
        kind = BrightnessChrome.Resolve(mixed, 2, out live, out frost);
        Expect(BrightnessChrome.LayoutKind.Dual, kind, "live + frost → Dual");
        ExpectTrue(live != null && live.Controllable, "dual row A controllable");
        ExpectTrue(frost != null && !frost.Controllable, "dual row B frosted");
        Expect("70", BrightnessChrome.DualRightLabel(live), "live right label is percent");
        Expect("2560×1440", BrightnessChrome.DualRightLabel(frost), "frost right label is resolution");

        BrightnessMonitor[] bothFrost = new BrightnessMonitor[]
        {
            Mon("a", "LG", "3840×2160", 0, false),
            Mon("b", "ASUS", "1920×1080", 0, false)
        };
        kind = BrightnessChrome.Resolve(bothFrost, 2, out live, out frost);
        Expect(BrightnessChrome.LayoutKind.Dual, kind, "two identified non-live → still Dual");
        Expect("3840×2160", BrightnessChrome.DualRightLabel(live), "frost A resolution");
        Expect("1920×1080", BrightnessChrome.DualRightLabel(frost), "frost B resolution");

        Expect("1920×1080", BrightnessChrome.FormatResolution(1920, 1080), "format resolution");
        Expect("", BrightnessChrome.FormatResolution(0, 1080), "bad resolution empty");

        ExpectTrue(BrightnessChrome.IsGenericMonitorName("Generic PnP Monitor"), "generic PnP");
        ExpectTrue(BrightnessChrome.IsGenericMonitorName(""), "empty is generic");
        ExpectTrue(!BrightnessChrome.IsGenericMonitorName("DELL U2720Q"), "Dell is specific");
        ExpectTrue(!BrightnessChrome.IsGenericMonitorName("Built-in display"), "built-in kept");

        Expect(
            "DELL U2720Q",
            BrightnessChrome.PreferDisplayName("Generic PnP Monitor", "Generic PnP Monitor", "DELL U2720Q"),
            "prefer EDID friendly name");
        Expect(
            "Display",
            BrightnessChrome.PreferDisplayName("", "", ""),
            "all empty → Display");

        BrightnessMonitor noRes = Mon("z", "Panel", "", 0, false);
        Expect("—", BrightnessChrome.DualRightLabel(noRes), "frost without resolution → em dash");

        Console.WriteLine();
        Console.WriteLine("BrightnessChrome tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static BrightnessMonitor Mon(string id, string name, string res, int pct, bool live)
    {
        BrightnessMonitor m = new BrightnessMonitor();
        m.Id = id;
        m.Name = name;
        m.Resolution = res;
        m.Percent = pct;
        m.Controllable = live;
        return m;
    }

    private static void Expect(BrightnessChrome.LayoutKind want, BrightnessChrome.LayoutKind got, string name)
    {
        if (want == got)
        {
            _passed++;
            Console.WriteLine("  OK  " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  " + name + " (want " + want + ", got " + got + ")");
        }
    }

    private static void Expect(string want, string got, string name)
    {
        if (string.Equals(want, got, StringComparison.Ordinal))
        {
            _passed++;
            Console.WriteLine("  OK  " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  " + name + " (want '" + want + "', got '" + got + "')");
        }
    }

    private static void ExpectTrue(bool cond, string name)
    {
        if (cond)
        {
            _passed++;
            Console.WriteLine("  OK  " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  " + name);
        }
    }
}
