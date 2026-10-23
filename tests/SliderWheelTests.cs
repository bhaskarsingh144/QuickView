using System;

/// <summary>
/// Cases for mouse-wheel adjustment of brightness (±1%) and volume (±1%).
/// Run via scripts\test.ps1.
/// </summary>
internal static class SliderWheelTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Expect(0, SliderWheel.StepFromDelta(0), "delta 0 → 0");
        Expect(1, SliderWheel.StepFromDelta(120), "delta +120 → +1");
        Expect(1, SliderWheel.StepFromDelta(240), "delta +240 → +1");
        Expect(1, SliderWheel.StepFromDelta(1), "delta +1 → +1");
        Expect(-1, SliderWheel.StepFromDelta(-120), "delta -120 → -1");
        Expect(-1, SliderWheel.StepFromDelta(-1), "delta -1 → -1");

        Expect(51, SliderWheel.AdjustBrightness(50, 120), "brightness 50 +wheel → 51");
        Expect(49, SliderWheel.AdjustBrightness(50, -120), "brightness 50 -wheel → 49");
        Expect(0, SliderWheel.AdjustBrightness(0, -120), "brightness clamps at 0");
        Expect(100, SliderWheel.AdjustBrightness(100, 120), "brightness clamps at 100");
        Expect(1, SliderWheel.AdjustBrightness(0, 120), "brightness 0 +wheel → 1");
        Expect(99, SliderWheel.AdjustBrightness(100, -120), "brightness 100 -wheel → 99");

        ExpectFloat(0.51f, SliderWheel.AdjustVolume01(0.50f, 120), 0.0001f, "volume 50% +wheel → 51%");
        ExpectFloat(0.49f, SliderWheel.AdjustVolume01(0.50f, -120), 0.0001f, "volume 50% -wheel → 49%");
        ExpectFloat(0f, SliderWheel.AdjustVolume01(0f, -120), 0.0001f, "volume clamps at 0");
        ExpectFloat(1f, SliderWheel.AdjustVolume01(1f, 120), 0.0001f, "volume clamps at 1");
        ExpectFloat(0.01f, SliderWheel.AdjustVolume01(0f, 120), 0.0001f, "volume 0 +wheel → 1%");
        ExpectFloat(0.99f, SliderWheel.AdjustVolume01(1f, -120), 0.0001f, "volume 1 -wheel → 99%");

        // Ten notches from 50 → 60 brightness
        int b = 50;
        for (int i = 0; i < 10; i++)
            b = SliderWheel.AdjustBrightness(b, 120);
        Expect(60, b, "10 wheel notches brightness 50→60");

        float v = 0.50f;
        for (int i = 0; i < 10; i++)
            v = SliderWheel.AdjustVolume01(v, -120);
        ExpectFloat(0.40f, v, 0.0001f, "10 wheel notches volume 50%→40%");

        // Vertical track: bottom = 0, top = 1
        ExpectFloat(0f, SliderWheel.VolumeFromVerticalY(100, 0, 100), 0.0001f, "vertical y=bottom → 0");
        ExpectFloat(1f, SliderWheel.VolumeFromVerticalY(0, 0, 100), 0.0001f, "vertical y=top → 1");
        ExpectFloat(0.5f, SliderWheel.VolumeFromVerticalY(50, 0, 100), 0.0001f, "vertical y=mid → 0.5");
        ExpectFloat(0f, SliderWheel.VolumeFromVerticalY(200, 0, 100), 0.0001f, "vertical y below → clamp 0");
        ExpectFloat(1f, SliderWheel.VolumeFromVerticalY(-10, 0, 100), 0.0001f, "vertical y above → clamp 1");
        ExpectFloat(0f, SliderWheel.VolumeFromVerticalY(0, 0, 0), 0.0001f, "vertical zero height → 0");

        Console.WriteLine();
        Console.WriteLine("SliderWheel tests: {0} passed, {1} failed", _passed, _failed);
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
