using System;
using System.Drawing;
using System.Management;
using System.Windows.Forms;

// Layout / text scale for the primary screen. Remeasured on first start, restart,
// DPI change, and display change.
//
// Model (PerMonitorV2):
//   - Layout DIPs:   px = dip * (Dpi/96) * Fit
//   - Font points:   pt = basePt * Fit * TextFit   (GDI then scales points by Dpi)
// Fit follows logical height (physical * 96 / Dpi). Laptop/panel floors used to
// push Fit back up on 15–16" FHD panels even when Windows DPI was already ≥125%,
// which made Point text feel oversized and cramped in fixed chrome (drawer tiles).
// TextFit further tightens fonts only on those laptop panels without shrinking
// the window chrome.

internal struct DisplayProfile
{
    public int PhysW;
    public int PhysH;
    public float Dpi;
    public float LogicW;
    public float LogicH;
    public float DiagonalIn;
    public float Fit;
    public float TextFit;
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
        p.TextFit = ComputeTextFit(p);
        return p;
    }

    public static float ComputeFit(DisplayProfile p)
    {
        float fit = FitFromLogicalHeight(p.LogicH);
        float scale = p.Dpi / 96f;

        float floor = LaptopFloor(p.DiagonalIn, p.SixteenTen);
        float panel = StandardPanelFloor(p.PhysW, p.PhysH, p.DiagonalIn, p.SixteenTen);
        float want = Math.Max(floor, panel);

        if (want > 0f && fit < want)
        {
            // When Windows scale is already elevated, logical height dropped on purpose.
            // Do not yank Fit all the way back to the old laptop floor — that is what
            // made 16" FHD/FHD+ text feel huge while chrome stayed "fine".
            if (scale >= 1.2f)
                fit = Math.Min(want, fit + 0.04f);
            else
                fit = want;
        }

        if (p.Ultrawide && fit > 0.94f && p.LogicH < 1200f)
            fit = 0.94f;

        if (fit > 1f)
            fit = 1f;
        if (fit < 0.62f)
            fit = 0.62f;
        return fit;
    }

    /// <summary>
    /// Extra multiplier for Point fonts only. Keeps window/chrome size, shrinks type
    /// on mid-size laptop panels where Segoe/Bahnschrift read large in the drawer.
    /// </summary>
    public static float ComputeTextFit(DisplayProfile p)
    {
        // 15–17" class with FHD / FHD+ native panels (user's 16" ~1080p / 16:10 case).
        bool compactLaptop = p.DiagonalIn > 0f && p.DiagonalIn <= 17.5f
            && p.PhysH > 0 && p.PhysH <= 1200;
        if (!compactLaptop && p.DiagonalIn <= 0f && p.PhysH >= 1000 && p.PhysH <= 1200
            && (Near(p.PhysW, 1920) || (p.SixteenTen && p.PhysH >= 1100)))
            compactLaptop = true; // unknown EDID: still treat FHD-class as laptop

        if (!compactLaptop)
            return 1f;

        float scale = p.Dpi / 96f;
        if (scale >= 1.45f)
            return 0.88f; // 150%+ on a small panel
        if (scale >= 1.2f)
            return 0.90f; // 125%
        return 0.92f;     // 100% on 16" — still slightly tight vs desktop
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
        // Softer than the previous 0.88/0.84 floors — those fought Windows DPI.
        if (diagonalIn >= 15.0f)
            return sixteenTen ? 0.80f : 0.76f; // 15–17" class
        if (diagonalIn >= 13.0f)
            return sixteenTen ? 0.76f : 0.72f; // 13–14" class
        return sixteenTen ? 0.72f : 0.68f;     // compact
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
                return 0.76f;
            return Laptopish(diagonalIn) ? 0.80f : 0f;
        }
        if (Near(w, 1920) && Near(h, 1080))
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.72f;
            return Laptopish(diagonalIn) ? 0.76f : 0f;
        }
        if (Near(w, 2240) && Near(h, 1400))
            return 0.80f; // 14" 16:10
        if (Near(w, 2560) && Near(h, 1600))
            return Laptopish(diagonalIn) ? 0.88f : 0f;
        if (Near(w, 2560) && Near(h, 1440))
            return Laptopish(diagonalIn) ? 0.86f : 0f;
        if (Near(w, 2880) && Near(h, 1800))
            return Laptopish(diagonalIn) ? 0.88f : 0f; // 2.8K 16:10
        if (Near(w, 3072) && Near(h, 1920))
            return Laptopish(diagonalIn) ? 0.88f : 0f; // 3K 16:10
        if (Near(w, 3840) && Near(h, 2400))
            return Laptopish(diagonalIn) ? 0.90f : 0f; // 4K 16:10 laptop
        if (Near(w, 3840) && Near(h, 2160))
            return 0f; // 4K 16:9 — logical curve only (desktop or laptop)
        if (Near(w, 3440) && Near(h, 1440))
            return 0f;
        if (Near(w, 1366) && Near(h, 768))
            return 0.62f;
        if (Near(w, 1600) && Near(h, 900))
            return Laptopish(diagonalIn) ? 0.68f : 0f;
        if (sixteenTen && Laptopish(diagonalIn) && h >= 1100 && h <= 1300)
        {
            if (diagonalIn > 0f && diagonalIn < 15.0f)
                return 0.76f;
            return 0.80f;
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
        // Approximate only — Windows DPI is a scale factor, not true panel PPI.
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
