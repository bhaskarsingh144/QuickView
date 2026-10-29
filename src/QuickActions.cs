using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

internal enum QuickHit
{
    None,
    QuickIcon,
    TextMinus,
    TextPlus,
    TextScale,
    TextApply,
    TileExplorer,
    TileLock,
    TileSleep,
    TileRestart,
    TileShutdown,
    TileDisplaySettings,
    TileSoundSettings,
    TileMute,
    TileAppRestart
}

internal enum QuickArm
{
    None,
    Restart,
    Shutdown
}

internal sealed class QuickActions
{
    public Rectangle IconRect;
    public Rectangle TextMinusRect;
    public Rectangle TextPlusRect;
    public Rectangle TextTrack;
    public Rectangle TextThumb;
    public Rectangle TextApplyRect;
    public Rectangle TileExplorer;
    public Rectangle TileLock;
    public Rectangle TileSleep;
    public Rectangle TileRestart;
    public Rectangle TileShutdown;
    public Rectangle TileDisplaySettings;
    public Rectangle TileSoundSettings;
    public Rectangle TileMute;
    public Rectangle TileAppRestart;

    public bool Muted;
    public int TextPercent = 100;
    public int TextAppliedPercent = 100;
    public bool TextDirty;
    public string StatusLine = "";
    public QuickArm Armed = QuickArm.None;
    public DateTime ArmedUntil = DateTime.MinValue;

    public void ClearHits()
    {
        IconRect = Rectangle.Empty;
        TextMinusRect = Rectangle.Empty;
        TextPlusRect = Rectangle.Empty;
        TextTrack = Rectangle.Empty;
        TextThumb = Rectangle.Empty;
        TextApplyRect = Rectangle.Empty;
        TileExplorer = Rectangle.Empty;
        TileLock = Rectangle.Empty;
        TileSleep = Rectangle.Empty;
        TileRestart = Rectangle.Empty;
        TileShutdown = Rectangle.Empty;
        TileDisplaySettings = Rectangle.Empty;
        TileSoundSettings = Rectangle.Empty;
        TileMute = Rectangle.Empty;
        TileAppRestart = Rectangle.Empty;
    }

    public void ClearArmIfExpired()
    {
        if (Armed != QuickArm.None && DateTime.UtcNow > ArmedUntil)
            Armed = QuickArm.None;
    }

    public void Arm(QuickArm arm)
    {
        Armed = arm;
        ArmedUntil = DateTime.UtcNow.AddSeconds(2);
    }

    public QuickHit HitTest(Point p)
    {
        ClearArmIfExpired();
        if (IconRect.Contains(p)) return QuickHit.QuickIcon;
        if (TextMinusRect.Contains(p)) return QuickHit.TextMinus;
        if (TextPlusRect.Contains(p)) return QuickHit.TextPlus;
        if (TextTrack.Contains(p) || TextThumb.Contains(p)) return QuickHit.TextScale;
        if (TextDirty && TextApplyRect.Contains(p)) return QuickHit.TextApply;
        if (TileExplorer.Contains(p)) return QuickHit.TileExplorer;
        if (TileLock.Contains(p)) return QuickHit.TileLock;
        if (TileSleep.Contains(p)) return QuickHit.TileSleep;
        if (TileRestart.Contains(p)) return QuickHit.TileRestart;
        if (TileShutdown.Contains(p)) return QuickHit.TileShutdown;
        if (TileDisplaySettings.Contains(p)) return QuickHit.TileDisplaySettings;
        if (TileSoundSettings.Contains(p)) return QuickHit.TileSoundSettings;
        if (TileMute.Contains(p)) return QuickHit.TileMute;
        if (TileAppRestart.Contains(p)) return QuickHit.TileAppRestart;
        return QuickHit.None;
    }

    public static void DrawSectionTitle(Graphics g, Font font, string title, int x, int y, int inner, Color muted)
    {
        if (g == null)
            return;
        using (SolidBrush brush = new SolidBrush(muted))
            g.DrawString(title, font, brush, x, y);
    }

    public static void DrawGridTile(Graphics g, Rectangle rect, string label, Font font, bool hover, bool armed, Color track, Color line, Color textMain, Color textMuted, Action<Graphics, Rectangle, Color> glyph)
    {
        if (g == null)
            return;
        Color back = armed ? Color.FromArgb(255, 72, 56, 56) : (hover ? Color.FromArgb(255, 54, 58, 66) : track);
        int rad = Math.Max(5, rect.Height / 10);
        using (GraphicsPath path = Round(rect, rad))
        using (SolidBrush brush = new SolidBrush(back))
            g.FillPath(brush, path);
        if (armed || hover)
        {
            using (Pen pen = new Pen(armed ? Color.FromArgb(255, 220, 100, 100) : line, 1.3f))
            using (GraphicsPath path = Round(rect, rad))
                g.DrawPath(pen, path);
        }
        Color ink = (hover || armed) ? textMain : Color.FromArgb(255, 210, 214, 222);
        // Reserve a label band first so icons never collide with "Shut down" etc. on compact Fit.
        int pad = Math.Max(3, rect.Height / 24);
        int labelH = 0;
        if (!string.IsNullOrEmpty(label) && font != null)
        {
            labelH = (int)Math.Ceiling(font.GetHeight()) + 2;
            if (labelH < rect.Height / 5)
                labelH = rect.Height / 5;
            if (labelH > rect.Height / 3)
                labelH = rect.Height / 3;
        }
        int iconMax = rect.Height - labelH - pad * 3;
        if (iconMax < 12)
            iconMax = 12;
        int icon = Math.Min(iconMax, Math.Max(14, rect.Height * 2 / 5));
        if (icon > rect.Width - pad * 2)
            icon = Math.Max(12, rect.Width - pad * 2);
        int iconX = rect.X + (rect.Width - icon) / 2;
        int contentTop = rect.Y + pad;
        int contentH = rect.Height - labelH - pad * 2;
        int iconY = contentTop + Math.Max(0, (contentH - icon) / 2);
        glyph(g, new Rectangle(iconX, iconY, icon, icon), ink);
        if (!string.IsNullOrEmpty(label) && font != null)
        {
            Rectangle textRect = new Rectangle(rect.X + pad, rect.Bottom - labelH - pad, rect.Width - pad * 2, labelH);
            using (SolidBrush brush = new SolidBrush(ink))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(label, font, brush, textRect, fmt);
            }
        }
    }

    public static void GlyphTextAa(Graphics g, Rectangle r, Color ink)
    {
        using (Font f = ChromeFonts.CreatePixel(Math.Max(7f, r.Height * 0.45f)))
        using (SolidBrush brush = new SolidBrush(ink))
        using (StringFormat fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Center;
            fmt.LineAlignment = StringAlignment.Center;
            g.DrawString("Aa", f, brush, r, fmt);
        }
    }

    /// <summary>
    /// HyperOS-style brightness block: rounded capsule, left fill by value, no thumb.
    /// Radius and min-fill follow the block size (no raw device-pixel floors).
    /// </summary>
    public static void DrawFillBlock(Graphics g, Rectangle block, float value01, Color trackColor, Color fillColor)
    {
        if (value01 < 0f) value01 = 0f;
        if (value01 > 1f) value01 = 1f;
        int radius = Math.Max(1, Math.Min(block.Width, block.Height) / 2);
        if (g == null)
            return;
        using (GraphicsPath path = Round(block, radius))
        using (SolidBrush brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        int fillW = (int)Math.Round(block.Width * value01);
        if (fillW <= 0)
            return;
        // Pill end for low values, but never more than half the track.
        int minFill = Math.Min(block.Width / 2, Math.Max(radius, block.Height));
        if (minFill < 1)
            minFill = 1;
        if (fillW < minFill)
            fillW = minFill;
        if (fillW > block.Width)
            fillW = block.Width;
        Rectangle fill = new Rectangle(block.X, block.Y, fillW, block.Height);
        using (GraphicsPath path = Round(fill, radius))
        using (SolidBrush brush = new SolidBrush(fillColor))
            g.FillPath(brush, path);
    }

    /// <summary>
    /// Vertical HyperOS-style volume block: fill rises from the bottom.
    /// </summary>
    public static void DrawFillBlockVertical(Graphics g, Rectangle block, float value01, Color trackColor, Color fillColor)
    {
        if (value01 < 0f) value01 = 0f;
        if (value01 > 1f) value01 = 1f;
        int radius = Math.Max(1, Math.Min(block.Width, block.Height) / 2);
        if (g == null)
            return;
        using (GraphicsPath path = Round(block, radius))
        using (SolidBrush brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        int fillH = (int)Math.Round(block.Height * value01);
        if (fillH <= 0)
            return;
        // Cap min fill so a short track does not look "stuck" loud at low volume.
        int minFill = Math.Min(block.Height / 2, Math.Max(radius, block.Width / 2));
        if (minFill < 1)
            minFill = 1;
        if (fillH < minFill)
            fillH = minFill;
        if (fillH > block.Height)
            fillH = block.Height;
        Rectangle fill = new Rectangle(block.X, block.Bottom - fillH, block.Width, fillH);
        using (GraphicsPath path = Round(fill, radius))
        using (SolidBrush brush = new SolidBrush(fillColor))
            g.FillPath(brush, path);
    }

    private static GraphicsPath Round(Rectangle r, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        if (d > r.Width) d = Math.Max(1, r.Width);
        if (d > r.Height) d = Math.Max(1, r.Height);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void GlyphExplorer(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        {
            g.DrawRectangle(pen, r.X + 1, r.Y + r.Height / 3, r.Width - 2, r.Height - r.Height / 3 - 1);
            g.DrawLine(pen, r.X + 1, r.Y + r.Height / 3, r.X + r.Width / 3, r.Y + 2);
            g.DrawLine(pen, r.X + r.Width / 3, r.Y + 2, r.X + r.Width / 2, r.Y + r.Height / 3);
        }
    }

    public static void GlyphLock(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        {
            int bodyTop = r.Y + r.Height / 3;
            g.DrawRectangle(pen, r.X + 2, bodyTop, r.Width - 4, r.Height - r.Height / 3 - 1);
            g.DrawArc(pen, r.X + 4, r.Y + 1, r.Width - 8, r.Height / 2, 200, 140);
        }
    }

    public static void GlyphSleep(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
            g.DrawArc(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 40, 280);
    }

    public static void GlyphRestart(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        {
            g.DrawArc(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 40, 280);
            int cx = r.Right - 3;
            int cy = r.Y + r.Height / 3;
            g.DrawLine(pen, cx - 4, cy, cx, cy);
            g.DrawLine(pen, cx, cy, cx, cy + 4);
        }
    }

    /// <summary>
    /// App relaunch: rounded panel outline with a refresh arc (distinct from PC Restart).
    /// </summary>
    public static void GlyphAppRestart(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int inset = Math.Max(1, r.Width / 10);
            g.DrawRectangle(pen, r.X + inset, r.Y + inset, r.Width - inset * 2, r.Height - inset * 2);
            int arc = Math.Max(8, r.Width / 2);
            int ax = r.X + (r.Width - arc) / 2;
            int ay = r.Y + (r.Height - arc) / 2;
            g.DrawArc(pen, ax, ay, arc, arc, 40, 260);
            int tipX = ax + arc - 1;
            int tipY = ay + arc / 3;
            g.DrawLine(pen, tipX - 3, tipY, tipX, tipY);
            g.DrawLine(pen, tipX, tipY, tipX, tipY + 3);
        }
    }

    public static void GlyphShutdown(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.6f))
        {
            int mid = r.X + r.Width / 2;
            g.DrawLine(pen, mid, r.Y + 1, mid, r.Y + r.Height / 2);
            g.DrawArc(pen, r.X + 1, r.Y + 3, r.Width - 2, r.Height - 4, 40, 280);
        }
    }

    public static void GlyphSettings(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        {
            g.DrawEllipse(pen, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
            g.DrawEllipse(pen, r.X + r.Width / 3, r.Y + r.Height / 3, r.Width / 3, r.Height / 3);
        }
    }

    public static void GlyphSpeakerOpen(Graphics g, Rectangle r, Color ink)
    {
        GlyphSpeaker(g, r, ink, false);
    }

    public static void GlyphSpeaker(Graphics g, Rectangle r, Color ink, bool muted)
    {
        using (Pen pen = new Pen(ink, 1.5f))
        using (SolidBrush brush = new SolidBrush(ink))
        {
            Point[] cone = new Point[]
            {
                new Point(r.X + 1, r.Y + r.Height / 3),
                new Point(r.X + r.Width / 3, r.Y + r.Height / 3),
                new Point(r.X + (2 * r.Width) / 3, r.Y + 1),
                new Point(r.X + (2 * r.Width) / 3, r.Bottom - 1),
                new Point(r.X + r.Width / 3, r.Bottom - r.Height / 3),
                new Point(r.X + 1, r.Bottom - r.Height / 3)
            };
            g.FillPolygon(brush, cone);
            if (muted)
                g.DrawLine(pen, r.X + 1, r.Y + 1, r.Right - 1, r.Bottom - 1);
            else
            {
                g.DrawArc(pen, r.X + r.Width / 2, r.Y + 2, r.Width / 2 - 1, r.Height - 4, -50, 100);
            }
        }
    }

    public static void GlyphSun(Graphics g, Rectangle r, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.4f))
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            int rad = Math.Max(2, r.Width / 5);
            g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4.0;
                int x1 = cx + (int)(Math.Cos(a) * (rad + 1));
                int y1 = cy + (int)(Math.Sin(a) * (rad + 1));
                int x2 = cx + (int)(Math.Cos(a) * (rad + 4));
                int y2 = cy + (int)(Math.Sin(a) * (rad + 4));
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }
    }
}
