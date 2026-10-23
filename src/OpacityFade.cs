using System;

/// <summary>
/// Opacity fade helpers for brightness preview: dim the drawer while scrolling
/// brightness so the desktop is visible, then ease back after idle.
/// </summary>
internal static class OpacityFade
{
    public const double Normal = 0.98;
    public const double Preview = 0.08;
    public const int IdleMs = 650;
    public const int FadeOutMs = 160;
    public const int FadeInMs = 280;

    public static double Clamp01(double v)
    {
        if (v < 0.0) return 0.0;
        if (v > 1.0) return 1.0;
        return v;
    }

    /// <summary>
    /// Smoothstep ease (0–1) for fade animation.
    /// </summary>
    public static double Ease(double t)
    {
        t = Clamp01(t);
        return t * t * (3.0 - 2.0 * t);
    }

    public static double Lerp(double from, double to, double t)
    {
        return from + (to - from) * Ease(t);
    }

    /// <summary>
    /// Opacity at <paramref name="elapsedMs"/> into a fade of <paramref name="durationMs"/>.
    /// </summary>
    public static double OpacityAt(double from, double to, int elapsedMs, int durationMs)
    {
        if (durationMs <= 0)
            return to;
        double t = elapsedMs / (double)durationMs;
        if (t >= 1.0)
            return to;
        return Lerp(from, to, t);
    }

    public static bool FadeComplete(int elapsedMs, int durationMs)
    {
        return elapsedMs >= durationMs;
    }

    public static DateTime IdleDeadline(DateTime utcNow)
    {
        return utcNow.AddMilliseconds(IdleMs);
    }

    public static bool IdleElapsed(DateTime utcNow, DateTime idleUntil)
    {
        return utcNow >= idleUntil;
    }
}
