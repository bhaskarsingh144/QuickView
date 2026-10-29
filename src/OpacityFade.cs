using System;

/// <summary>
/// Opacity helpers. Resting chrome stays fully opaque (ClearType / AA stay sharp).
/// Only the brightness-wheel preview soft-dims the main panel, then eases back after idle.
/// Drawer show/hide is binary (0 or 1) — no intermediate fade that blurs text.
/// </summary>
internal static class OpacityFade
{
    /// <summary>Quick-settings drawer resting opacity (fully opaque).</summary>
    public const double Normal = 1.0;
    /// <summary>Main panel resting opacity (fully opaque).</summary>
    public const double PanelNormal = 1.0;
    /// <summary>
    /// Opacity floor while brightness-wheel preview is active on the main panel.
    /// Soft peek (~0.64) so chrome stays readable while desktop brightness is visible.
    /// </summary>
    public const double Preview = 0.637875;
    public const int IdleMs = 650;
    public const int FadeOutMs = 160;
    public const int FadeInMs = 280;

    /// <summary>Drawer opens at full opacity; kept for tests / callers that still reference timing.</summary>
    public const int DrawerShowMs = 0;
    public const double DrawerShowFrom = 1.0;

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
