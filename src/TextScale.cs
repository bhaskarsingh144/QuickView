internal static class TextScale
{
    public const int MinPercent = 100;
    public const int MaxPercent = 225;
    public const int StepPercent = 5;
    public const int RangePercent = MaxPercent - MinPercent; // 125

    /// <summary>
    /// Clamp to 100–225 and snap to the nearest 5% (Accessibility Text size).
    /// </summary>
    public static int ClampAndSnap(int percent)
    {
        if (percent < MinPercent) percent = MinPercent;
        if (percent > MaxPercent) percent = MaxPercent;
        percent = ((percent + 2) / StepPercent) * StepPercent;
        if (percent < MinPercent) percent = MinPercent;
        if (percent > MaxPercent) percent = MaxPercent;
        return percent;
    }

    /// <summary>
    /// Map a slider fraction 0–1 to a percent in 100–225 (before snap).
    /// </summary>
    public static int PercentFromSlider(float t)
    {
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return (int)System.Math.Round(MinPercent + t * RangePercent);
    }

    /// <summary>
    /// Map a percent to a slider fraction 0–1 for drawing the thumb.
    /// </summary>
    public static float SliderFromPercent(int percent)
    {
        float t = (percent - MinPercent) / (float)RangePercent;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return t;
    }

    /// <summary>
    /// Next value for A+ / Text tile (+5). Tile wrap: above max returns min.
    /// </summary>
    public static int StepUp(int percent, bool wrapAtMax)
    {
        int n = percent + StepPercent;
        if (wrapAtMax && n > MaxPercent)
            return MinPercent;
        return ClampAndSnap(n);
    }

    /// <summary>
    /// Next value for A− (−5), clamped.
    /// </summary>
    public static int StepDown(int percent)
    {
        return ClampAndSnap(percent - StepPercent);
    }
}
