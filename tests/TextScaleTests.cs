using System;

/// <summary>
/// Dedicated cases for Accessibility Text size math used by the quick-settings
/// A− / A+ / slider / Text tile. Run via scripts\test.ps1 (no NuGet / NUnit).
/// </summary>
internal static class TextScaleTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        // ClampAndSnap — range and 5% grid
        Expect(100, TextScale.ClampAndSnap(100), "snap keeps 100");
        Expect(225, TextScale.ClampAndSnap(225), "snap keeps 225");
        Expect(100, TextScale.ClampAndSnap(99), "snap clamps below min");
        Expect(225, TextScale.ClampAndSnap(226), "snap clamps above max");
        Expect(135, TextScale.ClampAndSnap(136), "snap 136 → 135");
        Expect(140, TextScale.ClampAndSnap(138), "snap 138 → 140");
        Expect(140, TextScale.ClampAndSnap(142), "snap 142 → 140");
        Expect(105, TextScale.ClampAndSnap(103), "snap 103 → 105");
        Expect(105, TextScale.ClampAndSnap(107), "snap 107 → 105");
        Expect(110, TextScale.ClampAndSnap(108), "snap 108 → 110");

        // Every multiple of 5 in range is stable
        for (int p = TextScale.MinPercent; p <= TextScale.MaxPercent; p += TextScale.StepPercent)
            Expect(p, TextScale.ClampAndSnap(p), "snap stable at " + p);

        // Slider → percent → snap (ApplyTextScaleAt path)
        Expect(100, TextScale.ClampAndSnap(TextScale.PercentFromSlider(0f)), "slider 0 → 100");
        Expect(225, TextScale.ClampAndSnap(TextScale.PercentFromSlider(1f)), "slider 1 → 225");
        Expect(160, TextScale.ClampAndSnap(TextScale.PercentFromSlider(0.5f)), "slider 0.5 → 160");
        Expect(100, TextScale.ClampAndSnap(TextScale.PercentFromSlider(-0.2f)), "slider below 0 → 100");
        Expect(225, TextScale.ClampAndSnap(TextScale.PercentFromSlider(1.2f)), "slider above 1 → 225");

        // Percent → slider fraction (draw thumb)
        ExpectFloat(0f, TextScale.SliderFromPercent(100), 0.001f, "thumb at 100");
        ExpectFloat(1f, TextScale.SliderFromPercent(225), 0.001f, "thumb at 225");
        ExpectFloat(0.48f, TextScale.SliderFromPercent(160), 0.001f, "thumb at 160");
        ExpectFloat(0f, TextScale.SliderFromPercent(50), 0.001f, "thumb clamps low");
        ExpectFloat(1f, TextScale.SliderFromPercent(300), 0.001f, "thumb clamps high");

        // A− / A+
        Expect(100, TextScale.StepDown(100), "A- at min stays 100");
        Expect(120, TextScale.StepDown(125), "A- 125 → 120");
        Expect(220, TextScale.StepDown(225), "A- 225 → 220");
        Expect(105, TextScale.StepUp(100, false), "A+ 100 → 105");
        Expect(225, TextScale.StepUp(225, false), "A+ at max stays 225");
        Expect(225, TextScale.StepUp(220, false), "A+ 220 → 225");

        // Actions Text tile: +5 with wrap
        Expect(105, TextScale.StepUp(100, true), "tile 100 → 105");
        Expect(225, TextScale.StepUp(220, true), "tile 220 → 225");
        Expect(100, TextScale.StepUp(225, true), "tile wrap 225 → 100");
        Expect(100, TextScale.StepUp(221, true), "tile wrap after snap path");

        // Round-trip: snap then slider fraction stays in range
        for (int p = 100; p <= 225; p += 5)
        {
            float t = TextScale.SliderFromPercent(p);
            int back = TextScale.ClampAndSnap(TextScale.PercentFromSlider(t));
            Expect(p, back, "round-trip " + p);
        }

        Console.WriteLine();
        Console.WriteLine("TextScale tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Expect(int expected, int actual, string name)
    {
        if (expected == actual)
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
