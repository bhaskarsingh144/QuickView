using System;

/// <summary>
/// Scale / budget cases for header + volume chrome across Fit × TextFit × DPI.
/// Run via scripts\test.ps1.
/// </summary>
internal static class ChromeLayoutTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        // Profiles matching DisplayScale coverage (+ a tight low-Fit case).
        Case[] cases = new Case[]
        {
            new Case("16\" FHD @100%", 1920, 1080, 96f, 16.0f, false),
            new Case("16\" FHD @125%", 1920, 1080, 120f, 16.0f, false),
            new Case("16\" FHD+ @125%", 1920, 1200, 120f, 16.0f, true),
            new Case("27\" 4K @150%", 3840, 2160, 144f, 27.0f, false),
            new Case("14\" 1366x768 @100%", 1366, 768, 96f, 14.0f, false),
        };

        for (int i = 0; i < cases.Length; i++)
            RunProfile(cases[i]);

        // Volume geometry: badge + fill always partition the column; fill keeps usable height.
        ChromeLayout.VolumeColumnGeom tall = ChromeLayout.LayoutVolumeColumnForDisplay(
            ChromeLayout.Px(ChromeLayout.VolWidthDip, 96f, 1f),
            ChromeLayout.Px(120, 96f, 1f),
            96f, 1f);
        ExpectTrue(tall.BadgeHeight + tall.FillHeight == ChromeLayout.Px(120, 96f, 1f), "volume tall partitions height");
        ExpectTrue(tall.FillHeight >= ChromeLayout.Px(ChromeLayout.VolFillMinDip, 96f, 1f), "volume tall fill ≥ min");
        ExpectTrue(tall.IconSize > 0 && tall.IconSize <= tall.BadgeHeight, "volume tall icon fits badge");

        ChromeLayout.VolumeColumnGeom shortCol = ChromeLayout.LayoutVolumeColumnForDisplay(
            ChromeLayout.Px(ChromeLayout.VolWidthDip, 120f, 0.74f),
            ChromeLayout.Px(48, 120f, 0.74f),
            120f, 0.74f);
        ExpectTrue(shortCol.BadgeHeight + shortCol.FillHeight == ChromeLayout.Px(48, 120f, 0.74f), "volume short partitions height");
        ExpectTrue(shortCol.FillHeight >= 1, "volume short still has fill");

        // Metric floors: tiny FontEm must not drop below MetricMinPt.
        float tinyEm = ChromeLayout.FontEm(0.62f, 0.88f);
        ExpectTrue(tinyEm >= 0.55f, "FontEm floor");
        ExpectFloat(ChromeLayout.MetricMinPt,
            ChromeLayout.MetricPointSize(ChromeLayout.VolLevelFontPt, tinyEm),
            0.001f,
            "vol level pt floored at MetricMinPt");
        ExpectTrue(ChromeLayout.MetricPointSize(ChromeLayout.RateFontPt, tinyEm) >= ChromeLayout.MetricMinPt,
            "rate pt ≥ MetricMinPt");

        // Desktop 4K: full em, vol level uses base×em (above floor).
        float deskEm = ChromeLayout.FontEm(1f, 1f);
        ExpectFloat(ChromeLayout.VolLevelFontPt,
            ChromeLayout.MetricPointSize(ChromeLayout.VolLevelFontPt, deskEm),
            0.001f,
            "vol level pt on desktop");

        Console.WriteLine();
        Console.WriteLine("ChromeLayout tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private struct Case
    {
        public string Name;
        public int PhysW;
        public int PhysH;
        public float Dpi;
        public float DiagonalIn;
        public bool SixteenTen;

        public Case(string name, int w, int h, float dpi, float diag, bool ten)
        {
            Name = name;
            PhysW = w;
            PhysH = h;
            Dpi = dpi;
            DiagonalIn = diag;
            SixteenTen = ten;
        }
    }

    private static void RunProfile(Case c)
    {
        DisplayProfile p = new DisplayProfile();
        p.PhysW = c.PhysW;
        p.PhysH = c.PhysH;
        p.Dpi = c.Dpi;
        p.LogicW = c.PhysW * 96f / c.Dpi;
        p.LogicH = c.PhysH * 96f / c.Dpi;
        p.DiagonalIn = c.DiagonalIn;
        p.SixteenTen = c.SixteenTen;
        p.Ultrawide = false;
        p.Fit = DisplayScale.ComputeFit(p);
        p.TextFit = DisplayScale.ComputeTextFit(p);

        float em = ChromeLayout.FontEm(p.Fit, p.TextFit);
        ExpectTrue(em >= 0.55f && em <= 1.01f, c.Name + " FontEm in range");

        float ratePt = ChromeLayout.MetricPointSize(ChromeLayout.RateFontPt, em);
        float volPt = ChromeLayout.MetricPointSize(ChromeLayout.VolLevelFontPt, em);
        ExpectTrue(ratePt >= ChromeLayout.MetricMinPt, c.Name + " rate pt ≥ floor");
        ExpectTrue(volPt >= ChromeLayout.MetricMinPt, c.Name + " vol pt ≥ floor");

        int volW = ChromeLayout.Px(ChromeLayout.VolWidthDip, p.Dpi, p.Fit);
        int volH = ChromeLayout.Px(ChromeLayout.HeaderRowDip, p.Dpi, p.Fit)
            + ChromeLayout.Px(ChromeLayout.BrightBlockDip, p.Dpi, p.Fit);
        ChromeLayout.VolumeColumnGeom vol = ChromeLayout.LayoutVolumeColumnForDisplay(volW, volH, p.Dpi, p.Fit);
        ExpectTrue(vol.BadgeHeight + vol.FillHeight == volH, c.Name + " volume partitions");
        ExpectTrue(vol.FillHeight >= ChromeLayout.Px(ChromeLayout.VolFillMinDip, p.Dpi, p.Fit)
            || volH < ChromeLayout.Px(ChromeLayout.VolFillMinDip, p.Dpi, p.Fit) + ChromeLayout.Px(ChromeLayout.VolBadgeFloorDip, p.Dpi, p.Fit),
            c.Name + " volume fill usable");

        ExpectTrue(ChromeLayout.HeaderClusterFits(p.Dpi, p.Fit, p.TextFit, 0),
            c.Name + " header cluster fits main strip");
    }

    private static void ExpectTrue(bool ok, string name)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}", name);
        }
    }

    private static void ExpectFloat(float expected, float actual, float eps, string name)
    {
        if (Math.Abs(expected - actual) <= eps)
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual);
        }
    }
}
