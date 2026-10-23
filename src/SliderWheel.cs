internal static class SliderWheel
{
    /// <summary>
    /// One notch of mouse wheel → +1 or −1 (ignore magnitude beyond sign).
    /// </summary>
    public static int StepFromDelta(int wheelDelta)
    {
        if (wheelDelta == 0)
            return 0;
        return wheelDelta > 0 ? 1 : -1;
    }

    /// <summary>
    /// Brightness percent 0–100, adjusted by one point per wheel step.
    /// </summary>
    public static int AdjustBrightness(int percent, int wheelDelta)
    {
        int next = percent + StepFromDelta(wheelDelta);
        if (next < 0) next = 0;
        if (next > 100) next = 100;
        return next;
    }

    /// <summary>
    /// Volume 0–1, adjusted by 0.01 (1%) per wheel step.
    /// </summary>
    public static float AdjustVolume01(float volume01, int wheelDelta)
    {
        float next = volume01 + StepFromDelta(wheelDelta) * 0.01f;
        if (next < 0f) next = 0f;
        if (next > 1f) next = 1f;
        return next;
    }

    /// <summary>
    /// Map a Y position on a vertical track to volume 0–1 (bottom = 0, top = 1).
    /// </summary>
    public static float VolumeFromVerticalY(int y, int trackTop, int trackHeight)
    {
        if (trackHeight < 1)
            return 0f;
        float t = (trackTop + trackHeight - y) / (float)trackHeight;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return t;
    }
}
