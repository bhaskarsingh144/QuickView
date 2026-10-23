using System;
using System.Drawing;
using System.Management;
using System.Windows.Forms;

// Layout scale for the primary screen. Remeasured on first start, restart,
// DPI change, and display change.
//
// Standard panels covered (physical pixels + common inch classes):
//   16:10 — 1920x1200 (FHD+ / "1080" 16:10), 2240x1400, 2560x1600,
//           2880x1800 (2.8K), 3072x1920 (3K), 3840x2400 (4K 16:10)
//   16:9  — 1366x768, 1600x900, 1920x1080, 2560x1440, 3840x2160 (4K)
//   Other — 3440x1440 ultrawide, 5120x2160 / 5120x1440 superwide
// Inch classes: ~13–14" and ~15–16–17" laptops; ~24", ~27–28", ~32" desktops.
// Logical height still drives the desktop curve so 4K @150% stays full size.

internal struct DisplayProfile
{
    public int PhysW;
    public int PhysH;
    public float Dpi;
    public float LogicW;
    public float LogicH;
    public float DiagonalIn;
    public float Fit;
    public bool SixteenTen;
    public bool Ultrawide;
}

internal static class DisplayScale
{
    public static DisplayProfile Measure(float dpi)
    {
        if (dpi < 96f)
            dpi = 96f;
        Rectangle bounds = Screen.PrimaryScreen.Bounds;
        DisplayProfile p = new DisplayProfile();
        p.PhysW = bounds.Width;
        p.PhysH = bounds.Height;
        p.Dpi = dpi;
        p.LogicW = bounds.Width * 96f / dpi;
        p.LogicH = bounds.Height * 96f / dpi;
        float ratio = AspectRatio(p.LogicW, p.LogicH);
        p.SixteenTen = IsSixteenTen(ratio);
        p.Ultrawide = IsUltrawide(ratio);
        p.DiagonalIn = ResolveDiagonalInches(bounds, dpi, p.SixteenTen);
        p.Fit = ComputeFit(p);
        return p;
    }

    public static float ComputeFit(DisplayProfile p)
    {
        float fit = FitFromLogicalHeight(p.LogicH);
        float floor = LaptopFloor(p.DiagonalIn, p.SixteenTen);
        if (floor > 0f && fit < floor)
            fit = floor;

        // Known physical panels: nudge when Windows scale leaves logical height low.
        float panel = StandardPanelFloor(p.PhysW, p.PhysH, p.DiagonalIn, p.SixteenTen);
        if (panel > 0f && fit < panel)
            fit = panel;

        if (p.Ultrawide && fit > 0.94f && p.LogicH < 1200f)
            fit = 0.94f;

        if (fit > 1f)
            fit = 1f;
        if (fit < 0.62f)
            fit = 0.62f;
        return fit;
    }

    private static float FitFromLogicalHeight(float h)
    {
        // Logical height = physical height * 96 / Windows DPI.
        // 4K 16:9 @150% ≈ 1440; 1920x1200 @100% = 1200; FHD @100% = 1080.
        if (h >= 1600f)
            return 1f;
        if (h >= 1440f)
            return 1f;
        if (h >= 1280f)
            return 0.97f;
        if (h >= 1200f)
            return 0.94f;
        if (h >= 1080f)
            return 0.88f;
        if (h >= 1024f)
            return 0.82f;
        if (h >= 900f)
            return 0.74f;
        if (h >= 800f)
            return 0.70f;
        if (h >= 768f)
            return 0.66f;
        return 0.62f;
    }

    private static float LaptopFloor(float diagonalIn, bool sixteenTen)
    {
        if (diagonalIn <= 0f || diagonalIn > 18.5f)
            return 0f;
        // Desktops (~24"+) use the logical curve only.
        if (diagonalIn >= 15.0f)
            return sixteenTen ? 0.88f : 0.84f; // 15–17" class (incl. 16" FHD+ 16:10)
        if (diagonalIn >= 13.0f)
            return sixteenTen ? 0.80f : 0.76f; // 13–14" class
        return sixteenTen ? 0.74f : 0.70f;     // compact
    }

    // Floors keyed to common native panels when the inch class is known or likely.
    private static float StandardPanelFloor(int physW, int physH, float diagonalIn, bool sixteenTen)
    {
        int w = physW;
        int h = physH;
        if (Near(w, 1920) && Near(h, 1200))
        {
            // FHD+ 16:10 — common on 14" and 16" laptops.
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.80f;
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        }
        if (Near(w, 1920) && Near(h, 1080))
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.76f;
            return Laptopish(diagonalIn) ? 0.84f : 0f;
        }
        if (Near(w, 2240) && Near(h, 1400))
            return 0.82f; // 14" 16:10
        if (Near(w, 2560) && Near(h, 1600))
            return Laptopish(diagonalIn) ? 0.90f : 0f;
        if (Near(w, 2560) && Near(h, 1440))
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        if (Near(w, 2880) && Near(h, 1800))
            return Laptopish(diagonalIn) ? 0.90f : 0f; // 2.8K 16:10
        if (Near(w, 3072) && Near(h, 1920))
            return Laptopish(diagonalIn) ? 0.90f : 0f; // 3K 16:10
        if (Near(w, 3840) && Near(h, 2400))
            return Laptopish(diagonalIn) ? 0.92f : 0f; // 4K 16:10 laptop
        if (Near(w, 3840) && Near(h, 2160))
            return 0f; // 4K 16:9 — logical curve only (desktop or laptop)
        if (Near(w, 3440) && Near(h, 1440))
            return 0f;
        if (Near(w, 1366) && Near(h, 768))
            return 0.62f;
        if (Near(w, 1600) && Near(h, 900))
            return Laptopish(diagonalIn) ? 0.70f : 0f;
        if (sixteenTen && Laptopish(diagonalIn) && h >= 1100 && h <= 1300)
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.80f;
            return 0.86f;
        }
        return 0f;
    }

    private static bool Laptopish(float diagonalIn)
    {
        if (diagonalIn <= 0f)
            return true; // unknown size: still allow panel floors for laptop-like resolutions
        return diagonalIn <= 18.5f;
    }

    private static bool Near(int value, int target)
    {
        int d = value - target;
        if (d < 0)
            d = -d;
        return d <= 16;
    }

    private static float AspectRatio(float w, float h)
    {
        if (h < 1f)
            return 0f;
        return w / h;
    }

    private static bool IsSixteenTen(float ratio)
    {
        // EDID cm is often rounded (e.g. 34/22 = 1.545 for a 16:10 panel).
        return ratio >= 1.53f && ratio <= 1.67f;
    }

    private static bool IsUltrawide(float ratio)
    {
        return ratio >= 2.0f && ratio <= 3.6f;
    }

    private static float ResolveDiagonalInches(Rectangle bounds, float dpi, bool sixteenTen)
    {
        float estimated = EstimatedDiagonalInches(bounds, dpi);
        float edid = TryEdidDiagonalInches(bounds, sixteenTen, estimated);
        if (edid >= 10f && edid <= 45f)
            return edid;
        if (estimated >= 10f && estimated <= 45f)
            return estimated;
        return 0f;
    }

    private static float EstimatedDiagonalInches(Rectangle bounds, float dpi)
    {
        float w = bounds.Width / dpi;
        float h = bounds.Height / dpi;
        return (float)Math.Sqrt(w * w + h * h);
    }

    private static float TryEdidDiagonalInches(Rectangle bounds, bool sixteenTen, float estimated)
    {
        try
        {
            float screenAspect = bounds.Height < 1 ? 0f : (float)bounds.Width / bounds.Height;
            float best = 0f;
            float bestScore = float.MaxValue;
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "root\\wmi", "SELECT MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams"))
            using (ManagementObjectCollection list = searcher.Get())
            {
                foreach (ManagementObject row in list)
                {
                    using (row)
                    {
                        object ow = row["MaxHorizontalImageSize"];
                        object oh = row["MaxVerticalImageSize"];
                        if (ow == null || oh == null)
                            continue;
                        float cmW = Convert.ToSingle(ow);
                        float cmH = Convert.ToSingle(oh);
                        if (cmW < 5f || cmH < 5f)
                            continue;
                        float diagIn = (float)Math.Sqrt(cmW * cmW + cmH * cmH) / 2.54f;
                        float aspect = cmW / cmH;
                        bool edidTen = IsSixteenTen(aspect);
                        float aspectDelta = Math.Abs(aspect - screenAspect);
                        float sizeDelta = Math.Abs(diagIn - estimated);
                        float score = aspectDelta * 40f + sizeDelta;
                        if (sixteenTen == edidTen)
                            score -= 2f;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = diagIn;
                        }
                    }
                }
            }
            return best;
        }
        catch
        {
            return 0f;
        }
    }
}
