using System;

/// <summary>
/// Pure layout math for panel chrome introduced with the compact header / volume column.
/// Keeps DIP→px and metric font floors consistent across Fit × TextFit × DPI — no raw
/// device-pixel mins that break on 16″ FHD@125% or 4K@150%.
/// </summary>
internal static class ChromeLayout
{
    public const string NetworkRateSample = "99.99 MB/s";

    public const int PanelWidthDip = 380;
    public const int ContentPadDip = 32;
    public const int HeaderRowDip = 48;
    public const int VolWidthDip = 28;
    public const int VolIconDip = 10;
    public const int VolIconMinDip = 8;
    public const int VolIconPadDip = 2;
    public const int VolWidthInsetDip = 12;
    public const int VolFillMinDip = 28;
    public const int VolBadgeFloorDip = 14;
    public const int VolMainGapDip = 8;
    public const int VolMinHeightDip = 40;
    public const int MainMinWidthDip = 120;
    public const int BrightBlockDip = 34;
    public const int BrightGapBelowDip = 10;
    /// <summary>Minimum width reserved for the drawer hit left of the network cluster.</summary>
    public const int ChevronReserveMinDip = 44;
    public const int HeaderGapDip = 6;
    public const int HeaderMidGapDip = 4;
    public const int BatteryIconDip = 12;
    public const int BatteryIconGapDip = 3;
    public const int BatterySlotPadDip = 4;

    public const float RateFontPt = 11.5f;
    public const float LevelFontPt = 11f;
    public const float VolLevelFontPt = 8.5f;
    public const float BatteryFontPt = 11f;
    /// <summary>Readable floor for metric Point sizes after Fit×TextFit.</summary>
    public const float MetricMinPt = 8f;

    public static float FontEm(float fit, float textFit)
    {
        float t = fit * textFit;
        if (t < 0.55f)
            return 0.55f;
        return t;
    }

    public static int Px(int dip, float dpi, float fit)
    {
        if (dpi < 96f)
            dpi = 96f;
        if (fit < 0.62f)
            fit = 0.62f;
        return (int)Math.Round(dip * dpi / 96f * fit);
    }

    /// <summary>
    /// Half-point grid + MetricMinPt floor — avoids blurry fractional sizes on high-DPI.
    /// </summary>
    public static float SnapPt(float pt)
    {
        if (pt < MetricMinPt)
            pt = MetricMinPt;
        return (float)(Math.Round(pt * 2.0) / 2.0);
    }

    public static float MetricPointSize(float basePt, float fontEm)
    {
        return SnapPt(basePt * fontEm);
    }

    public struct VolumeColumnGeom
    {
        public int BadgeHeight;
        public int FillHeight;
        public int IconSize;
    }

    /// <summary>
    /// Speaker sits in a top badge; fill track is the remainder. All sizes are already in px.
    /// </summary>
    public static VolumeColumnGeom LayoutVolumeColumn(
        int columnW,
        int columnH,
        int iconPx,
        int iconMinPx,
        int iconPadPx,
        int widthInsetPx,
        int fillMinPx,
        int badgeFloorPx)
    {
        VolumeColumnGeom g = new VolumeColumnGeom();
        if (columnH < 1)
        {
            g.BadgeHeight = 0;
            g.FillHeight = 0;
            g.IconSize = 0;
            return g;
        }

        int icon = iconPx;
        if (icon < iconMinPx)
            icon = iconMinPx;
        int byWidth = columnW - widthInsetPx;
        if (byWidth < iconMinPx)
            byWidth = iconMinPx;
        if (icon > byWidth)
            icon = byWidth;

        int badgeH = icon + iconPadPx * 2;
        int fillMin = fillMinPx;
        if (fillMin < 1)
            fillMin = 1;
        if (badgeH > columnH - fillMin)
            badgeH = Math.Max(badgeFloorPx, columnH - fillMin);
        if (badgeH < 0)
            badgeH = 0;
        if (badgeH > columnH)
            badgeH = columnH;

        if (icon > badgeH - iconPadPx)
            icon = Math.Max(iconMinPx / 2, badgeH - iconPadPx);
        if (icon < 0)
            icon = 0;

        g.IconSize = icon;
        g.BadgeHeight = badgeH;
        g.FillHeight = columnH - badgeH;
        return g;
    }

    public static VolumeColumnGeom LayoutVolumeColumnForDisplay(int columnW, int columnH, float dpi, float fit)
    {
        return LayoutVolumeColumn(
            columnW,
            columnH,
            Px(VolIconDip, dpi, fit),
            Px(VolIconMinDip, dpi, fit),
            Px(VolIconPadDip, dpi, fit),
            Px(VolWidthInsetDip, dpi, fit),
            Px(VolFillMinDip, dpi, fit),
            Px(VolBadgeFloorDip, dpi, fit));
    }

    /// <summary>
    /// Approximate network cluster width without GDI — used to assert header budget.
    /// Wi-Fi square tracks the rate stack height (capped by the header row).
    /// </summary>
    public static int EstimateNetworkWidthPx(float dpi, float fit, float textFit)
    {
        int row = Px(HeaderRowDip, dpi, fit);
        float em = FontEm(fit, textFit);
        float ratePt = MetricPointSize(RateFontPt, em);
        // Point → pixel (same unit WinForms uses for GraphicsUnit.Point).
        float pxPerPt = dpi / 72f;
        int lineH = Math.Max(Px(12, dpi, fit), (int)Math.Ceiling(ratePt * pxPerPt));
        int rowGap = Math.Max(1, Px(2, dpi, fit));
        int ratesH = lineH * 2 + rowGap;
        int maxSide = row - Px(6, dpi, fit);
        if (ratesH > maxSide)
        {
            lineH = Math.Max(Px(10, dpi, fit), (maxSide - rowGap) / 2);
            ratesH = lineH * 2 + rowGap;
        }
        int wifi = ratesH;
        // Fixed sample width (~0.55em average advance for geometric digits + unit).
        int rateTextW = (int)Math.Ceiling(NetworkRateSample.Length * ratePt * pxPerPt * 0.55f)
            + Px(2, dpi, fit);
        int arrowColW = Math.Max(Px(10, dpi, fit), (int)Math.Ceiling(ratePt * pxPerPt)) + Px(4, dpi, fit);
        int clusterGap = Px(6, dpi, fit);
        return wifi + clusterGap + arrowColW + rateTextW;
    }

    /// <summary>Fixed battery slot: "100%" + two icon cells.</summary>
    public static int EstimateBatterySlotPx(float dpi, float fit, float textFit)
    {
        float em = FontEm(fit, textFit);
        float battPt = MetricPointSize(BatteryFontPt, em);
        float pxPerPt = dpi / 72f;
        // "100%" ≈ 4 glyphs.
        int textW = (int)Math.Ceiling(4f * battPt * pxPerPt * 0.62f);
        int icon = Px(BatteryIconDip, dpi, fit);
        int gap = Px(BatteryIconGapDip, dpi, fit);
        return textW + 2 * (icon + gap) + Px(BatterySlotPadDip, dpi, fit);
    }

    public static int ContentInnerPx(float dpi, float fit, int gutterPx)
    {
        int inner = Px(PanelWidthDip, dpi, fit) - Px(ContentPadDip, dpi, fit) - gutterPx;
        if (inner < 1)
            return 1;
        return inner;
    }

    /// <summary>
    /// True when chevron reserve + network + battery (+ gaps) fit the content inner width.
    /// </summary>
    public static bool HeaderClusterFits(float dpi, float fit, float textFit, int gutterPx)
    {
        int inner = ContentInnerPx(dpi, fit, gutterPx);
        int volW = Px(VolWidthDip, dpi, fit);
        int gap = Px(VolMainGapDip, dpi, fit);
        // Header status uses mainW = inner - vol - gap (volume column sits beside header).
        int mainW = inner - volW - gap;
        if (mainW < Px(MainMinWidthDip, dpi, fit))
            mainW = Px(MainMinWidthDip, dpi, fit);

        int netW = EstimateNetworkWidthPx(dpi, fit, textFit);
        int battW = EstimateBatterySlotPx(dpi, fit, textFit);
        int row = Px(HeaderRowDip, dpi, fit);
        int chev = Math.Max(Px(ChevronReserveMinDip, dpi, fit), row);
        int hGap = Px(HeaderGapDip, dpi, fit);
        int midGap = Px(HeaderMidGapDip, dpi, fit);
        int need = chev + hGap + netW + hGap + midGap + battW;
        return need <= mainW;
    }
}
