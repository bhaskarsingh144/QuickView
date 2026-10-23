using System;

internal static class OpacityFadeTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        ExpectDouble(0.0, OpacityFade.Clamp01(-1), 0.0001, "clamp low");
        ExpectDouble(1.0, OpacityFade.Clamp01(2), 0.0001, "clamp high");
        ExpectDouble(0.5, OpacityFade.Clamp01(0.5), 0.0001, "clamp mid");

        ExpectDouble(0.0, OpacityFade.Ease(0), 0.0001, "ease 0");
        ExpectDouble(1.0, OpacityFade.Ease(1), 0.0001, "ease 1");
        ExpectDouble(0.5, OpacityFade.Ease(0.5), 0.0001, "ease 0.5");
        // Smoothstep is below linear in first half
        if (OpacityFade.Ease(0.25) < 0.25)
        {
            _passed++;
            Console.WriteLine("  PASS  ease 0.25 below linear");
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  ease 0.25 below linear");
        }

        ExpectDouble(OpacityFade.Normal, OpacityFade.OpacityAt(OpacityFade.Normal, OpacityFade.Preview, 0, OpacityFade.FadeOutMs), 0.0001, "fade start = from");
        ExpectDouble(OpacityFade.Preview, OpacityFade.OpacityAt(OpacityFade.Normal, OpacityFade.Preview, OpacityFade.FadeOutMs, OpacityFade.FadeOutMs), 0.0001, "fade end = to");
        double mid = OpacityFade.OpacityAt(OpacityFade.Normal, OpacityFade.Preview, OpacityFade.FadeOutMs / 2, OpacityFade.FadeOutMs);
        if (mid < OpacityFade.Normal && mid > OpacityFade.Preview)
        {
            _passed++;
            Console.WriteLine("  PASS  fade mid between ends");
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  fade mid between ends: {0}", mid);
        }

        if (!OpacityFade.FadeComplete(0, 160)) { Pass("not complete at start"); } else { Fail("not complete at start"); }
        if (OpacityFade.FadeComplete(160, 160)) { Pass("complete at duration"); } else { Fail("complete at duration"); }
        if (OpacityFade.FadeComplete(200, 160)) { Pass("complete after duration"); } else { Fail("complete after duration"); }

        DateTime t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        DateTime deadline = OpacityFade.IdleDeadline(t0);
        Expect(OpacityFade.IdleMs, (int)(deadline - t0).TotalMilliseconds, "idle deadline ms");
        if (!OpacityFade.IdleElapsed(t0, deadline)) { Pass("idle not yet"); } else { Fail("idle not yet"); }
        if (OpacityFade.IdleElapsed(deadline, deadline)) { Pass("idle at deadline"); } else { Fail("idle at deadline"); }
        if (OpacityFade.IdleElapsed(deadline.AddMilliseconds(1), deadline)) { Pass("idle after deadline"); } else { Fail("idle after deadline"); }

        if (OpacityFade.Preview < 0.2) { Pass("preview very transparent"); } else { Fail("preview very transparent"); }
        if (OpacityFade.Normal > 0.9) { Pass("normal near opaque"); } else { Fail("normal near opaque"); }
        if (OpacityFade.PanelNormal > 0.9 && OpacityFade.PanelNormal < OpacityFade.Normal)
            Pass("panel normal slightly below drawer normal");
        else
            Fail("panel normal slightly below drawer normal");
        ExpectDouble(OpacityFade.PanelNormal, OpacityFade.OpacityAt(OpacityFade.Preview, OpacityFade.PanelNormal, OpacityFade.FadeInMs, OpacityFade.FadeInMs), 0.0001, "fade in ends at PanelNormal");

        Console.WriteLine();
        Console.WriteLine("OpacityFade tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Pass(string name)
    {
        _passed++;
        Console.WriteLine("  PASS  {0}", name);
    }

    private static void Fail(string name)
    {
        _failed++;
        Console.WriteLine("  FAIL  {0}", name);
    }

    private static void Expect(int expected, int actual, string name)
    {
        if (expected == actual) Pass(name);
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual);
        }
    }

    private static void ExpectDouble(double expected, double actual, double eps, string name)
    {
        if (Math.Abs(expected - actual) <= eps) Pass(name);
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}: expected {1}, got {2}", name, expected, actual);
        }
    }
}
