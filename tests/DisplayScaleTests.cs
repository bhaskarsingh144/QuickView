using System;

/// <summary>
/// Cases for layout Fit / TextFit on common panels (esp. 16" FHD / FHD+).
/// Run via scripts\test.ps1.
/// </summary>
internal static class DisplayScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        // 16" FHD 16:9 @100% — text should tighten; Fit follows the 1080 logical curve.
        DisplayProfile fhd100 = Profile(1920, 1080, 96f, 16.0f, false);
        ExpectFitRange(fhd100, 0.74f, 0.88f, "16\" FHD @100% Fit");
        ExpectFloat(0.92f, DisplayScale.ComputeTextFit(fhd100), 0.001f, "16\" FHD @100% TextFit");

        // Same panel @125% — Windows already scaled; Fit must not yank back to ~0.84.
        DisplayProfile fhd125 = Profile(1920, 1080, 120f, 16.0f, false);
        float fit125 = DisplayScale.ComputeFit(fhd125);
        ExpectTrue(fit125 <= 0.80f, "16\" FHD @125% Fit ≤ 0.80 (was over-floored), got " + fit125.ToString("0.00"));
        ExpectFloat(0.90f, DisplayScale.ComputeTextFit(fhd125), 0.001f, "16\" FHD @125% TextFit");

        // 16" FHD+ 16:10 @125%
        DisplayProfile ten125 = Profile(1920, 1200, 120f, 16.0f, true);
        float fitTen = DisplayScale.ComputeFit(ten125);
        ExpectTrue(fitTen <= 0.82f, "16\" 1920x1200 @125% Fit ≤ 0.82, got " + fitTen.ToString("0.00"));
        ExpectFloat(0.90f, DisplayScale.ComputeTextFit(ten125), 0.001f, "16\" 1920x1200 @125% TextFit");

        // 27" 4K @150% — desktop curve, no text tighten.
        DisplayProfile fourK = Profile(3840, 2160, 144f, 27.0f, false);
        ExpectFloat(1f, DisplayScale.ComputeFit(fourK), 0.001f, "27\" 4K @150% Fit");
        ExpectFloat(1f, DisplayScale.ComputeTextFit(fourK), 0.001f, "27\" 4K @150% TextFit");

        // Logical height curve sanity (desktop diagonal → no laptop floor)
        ExpectFloat(0.88f, CallFitHeight(1080f), 0.001f, "curve 1080 → 0.88");
        ExpectFloat(0.74f, CallFitHeight(900f), 0.001f, "curve 900 → 0.74");
        ExpectFloat(1f, CallFitHeight(1440f), 0.001f, "curve 1440 → 1");

        Console.WriteLine();
        Console.WriteLine("DisplayScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static DisplayProfile Profile(int physW, int physH, float dpi, float diagonalIn, bool sixteenTen)
    {
        DisplayProfile p = new DisplayProfile();
        p.PhysW = physW;
        p.PhysH = physH;
        p.Dpi = dpi;
        p.LogicW = physW * 96f / dpi;
        p.LogicH = physH * 96f / dpi;
        p.DiagonalIn = diagonalIn;
        p.SixteenTen = sixteenTen;
        p.Ultrawide = false;
        p.Fit = DisplayScale.ComputeFit(p);
        p.TextFit = DisplayScale.ComputeTextFit(p);
        return p;
    }

    private static float CallFitHeight(float logicH)
    {
        DisplayProfile p = new DisplayProfile();
        p.LogicH = logicH;
        p.Dpi = 96f;
        p.DiagonalIn = 27f;
        p.PhysW = 0;
        p.PhysH = 0;
        return DisplayScale.ComputeFit(p);
    }

    private static void ExpectFitRange(DisplayProfile p, float min, float max, string name)
    {
        float fit = DisplayScale.ComputeFit(p);
        if (fit >= min && fit <= max)
        {
            _passed++;
            Console.WriteLine("  PASS  {0} ({1:0.00})", name, fit);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: {1:0.00} not in [{2:0.00},{3:0.00}]", name, fit, min, max);
        }
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
