using System;
using System.Drawing;

/// <summary>
/// Typography helpers for sharp owner-drawn chrome: Semibold faces, half-point grid,
/// readable floors after Fit × TextFit. Ink colors stay with the caller (UiTheme).
/// </summary>
internal static class ChromeFonts
{
    public static Font CreateUi(float basePt, float fontEm)
    {
        return CreateFace(ChromeLayout.SnapPt(basePt * fontEm), preferSemibold: true);
    }

    public static Font CreateLabel(float basePt, float fontEm)
    {
        return CreateFace(ChromeLayout.SnapPt(basePt * fontEm), preferSemibold: true);
    }

    /// <summary>
    /// Prefer Bahnschrift SemiBold, then Segoe UI Semibold — same quality as network rates.
    /// </summary>
    public static Font CreateMetric(float basePt, float fontEm)
    {
        float em = ChromeLayout.MetricPointSize(basePt, fontEm);
        string[] names = new string[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Segoe UI" };
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            FontStyle style = (name == "Bahnschrift" || name == "Segoe UI") ? FontStyle.Bold : FontStyle.Regular;
            try
            {
                Font font = new Font(name, em, style, GraphicsUnit.Point);
                string family = font.FontFamily.Name;
                if (family.IndexOf("Bahnschrift", StringComparison.OrdinalIgnoreCase) >= 0
                    || family.IndexOf("Segoe", StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
                font.Dispose();
            }
            catch
            {
            }
        }
        return new Font("Segoe UI", em, FontStyle.Bold, GraphicsUnit.Point);
    }

    /// <summary>Pixel-sized glyph caption (e.g. tile "Aa") — Semibold when available.</summary>
    public static Font CreatePixel(float pixelSize)
    {
        if (pixelSize < 7f)
            pixelSize = 7f;
        try
        {
            return new Font("Segoe UI Semibold", pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch
        {
        }
        return new Font("Segoe UI", pixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static Font CreateFace(float emSize, bool preferSemibold)
    {
        if (preferSemibold)
        {
            try
            {
                return new Font("Segoe UI Semibold", emSize, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
            }
            try
            {
                return new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Point);
            }
            catch
            {
            }
        }
        return new Font("Segoe UI", emSize, FontStyle.Regular, GraphicsUnit.Point);
    }
}
