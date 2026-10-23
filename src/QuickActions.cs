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
    Mute,
    Volume,
    Brightness,
    TileExplorer,
    TileLock,
    TileSleep,
    TileRestart,
    TileShutdown,
    TileDisplaySettings,
    TileSoundSettings,
    TileMute
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
    public Rectangle MuteRect;
    public Rectangle VolumeTrack;
    public Rectangle VolumeThumb;
    public readonly List<Rectangle> BrightTracks = new List<Rectangle>();
    public readonly List<Rectangle> BrightThumbs = new List<Rectangle>();
    public readonly List<string> BrightIds = new List<string>();
    public Rectangle TileExplorer;
    public Rectangle TileLock;
    public Rectangle TileSleep;
    public Rectangle TileRestart;
    public Rectangle TileShutdown;
    public Rectangle TileDisplaySettings;
    public Rectangle TileSoundSettings;
    public Rectangle TileMute;

    public float Volume01;
    public bool Muted;
    public int TextPercent = 100;
    public int TextAppliedPercent = 100;
    public bool TextDirty;
    public string StatusLine = "";
    public QuickArm Armed = QuickArm.None;
    public DateTime ArmedUntil = DateTime.MinValue;
    public int DragBrightIndex = -1;
    public bool DragVolume;
    public bool DragText;

    public void ClearHits()
    {
        IconRect = Rectangle.Empty;
        TextMinusRect = Rectangle.Empty;
        TextPlusRect = Rectangle.Empty;
        TextTrack = Rectangle.Empty;
        TextThumb = Rectangle.Empty;
        TextApplyRect = Rectangle.Empty;
        MuteRect = Rectangle.Empty;
        VolumeTrack = Rectangle.Empty;
        VolumeThumb = Rectangle.Empty;
        BrightTracks.Clear();
        BrightThumbs.Clear();
        BrightIds.Clear();
        TileExplorer = Rectangle.Empty;
        TileLock = Rectangle.Empty;
        TileSleep = Rectangle.Empty;
        TileRestart = Rectangle.Empty;
        TileShutdown = Rectangle.Empty;
        TileDisplaySettings = Rectangle.Empty;
        TileSoundSettings = Rectangle.Empty;
        TileMute = Rectangle.Empty;
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
        if (MuteRect.Contains(p)) return QuickHit.Mute;
        if (VolumeTrack.Contains(p) || VolumeThumb.Contains(p)) return QuickHit.Volume;
        for (int i = 0; i < BrightTracks.Count; i++)
        {
            if (BrightTracks[i].Contains(p) || (i < BrightThumbs.Count && BrightThumbs[i].Contains(p)))
                return QuickHit.Brightness;
        }
        if (TileExplorer.Contains(p)) return QuickHit.TileExplorer;
        if (TileLock.Contains(p)) return QuickHit.TileLock;
        if (TileSleep.Contains(p)) return QuickHit.TileSleep;
        if (TileRestart.Contains(p)) return QuickHit.TileRestart;
        if (TileShutdown.Contains(p)) return QuickHit.TileShutdown;
        if (TileDisplaySettings.Contains(p)) return QuickHit.TileDisplaySettings;
        if (TileSoundSettings.Contains(p)) return QuickHit.TileSoundSettings;
        if (TileMute.Contains(p)) return QuickHit.TileMute;
        return QuickHit.None;
    }

    public int BrightnessIndexAt(Point p)
    {
        for (int i = 0; i < BrightTracks.Count; i++)
        {
            if (BrightTracks[i].Contains(p) || (i < BrightThumbs.Count && BrightThumbs[i].Contains(p)))
                return i;
        }
        return -1;
    }

    public static void DrawQuickGlyph(Graphics g, Rectangle r, Color ink)
    {
        int inset = Math.Max(6, r.Width / 5);
        int w = r.Width - 2 * inset;
        int gap = Math.Max(4, (r.Height - 2 * inset) / 4);
        int totalH = gap * 2;
        int x = r.X + inset;
        int y0 = r.Y + (r.Height - totalH) / 2;
        using (Pen pen = new Pen(ink, 1.6f))
        using (SolidBrush brush = new SolidBrush(ink))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            for (int i = 0; i < 3; i++)
            {
                int yy = y0 + i * gap;
                g.DrawLine(pen, x, yy, x + w, yy);
                int knob = x + (i == 0 ? w / 3 : (i == 1 ? (2 * w) / 3 : w / 2));
                g.FillEllipse(brush, knob - 2, yy - 2, 4, 4);
            }
        }
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
        int icon = Math.Max(26, Math.Min(34, rect.Height * 2 / 5));
        int iconX = rect.X + (rect.Width - icon) / 2;
        int iconY = rect.Y + Math.Max(10, (rect.Height / 2 - icon) / 2 + 2);
        glyph(g, new Rectangle(iconX, iconY, icon, icon), ink);
        if (!string.IsNullOrEmpty(label) && font != null)
        {
            int textTop = iconY + icon + 4;
            Rectangle textRect = new Rectangle(rect.X + 3, textTop, rect.Width - 6, rect.Bottom - textTop - 4);
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
        using (Font f = new Font("Segoe UI", Math.Max(7f, r.Height * 0.45f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush brush = new SolidBrush(ink))
        using (StringFormat fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Center;
            fmt.LineAlignment = StringAlignment.Center;
            g.DrawString("Aa", f, brush, r, fmt);
        }
    }

    public static void DrawSlider(Graphics g, Rectangle track, float value01, Color trackColor, Color fillColor, out Rectangle thumb)
    {
        thumb = Rectangle.Empty;
        if (g == null)
        {
            int tw = Math.Max(8, track.Height + 4);
            int tx = track.X + (int)((track.Width - tw) * value01);
            thumb = new Rectangle(tx, track.Y - 2, tw, track.Height + 4);
            return;
        }
        using (GraphicsPath path = Round(track, track.Height / 2))
        using (SolidBrush brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        int fillW = (int)Math.Round(track.Width * value01);
        if (fillW > 0)
        {
            Rectangle fill = new Rectangle(track.X, track.Y, Math.Max(fillW, track.Height), track.Height);
            if (fill.Width > track.Width) fill.Width = track.Width;
            using (GraphicsPath path = Round(fill, track.Height / 2))
            using (SolidBrush brush = new SolidBrush(fillColor))
                g.FillPath(brush, path);
        }
        int thumbW = Math.Max(10, track.Height + 2);
        int thumbX = track.X + (int)Math.Round((track.Width - thumbW) * value01);
        thumb = new Rectangle(thumbX, track.Y - 3, thumbW, track.Height + 6);
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 226, 230, 236)))
            g.FillEllipse(brush, thumb);
    }

    /// <summary>
    /// HyperOS-style brightness block: rounded capsule, left fill by value, no thumb.
    /// </summary>
    public static void DrawFillBlock(Graphics g, Rectangle block, float value01, Color trackColor, Color fillColor)
    {
        if (value01 < 0f) value01 = 0f;
        if (value01 > 1f) value01 = 1f;
        int radius = Math.Max(8, block.Height / 2);
        if (g == null)
            return;
        using (GraphicsPath path = Round(block, radius))
        using (SolidBrush brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);
        int fillW = (int)Math.Round(block.Width * value01);
        if (fillW <= 0)
            return;
        // Keep a pill end so low values still read as a rounded fill.
        int minFill = Math.Min(block.Width, Math.Max(radius, block.Height));
        if (fillW < minFill)
            fillW = minFill;
        if (fillW > block.Width)
            fillW = block.Width;
        Rectangle fill = new Rectangle(block.X, block.Y, fillW, block.Height);
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
