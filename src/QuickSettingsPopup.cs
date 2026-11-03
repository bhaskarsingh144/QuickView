using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

internal sealed class QuickSettingsPopup : Form
{
    private static readonly Color Fill = UiTheme.Bg;
    private static readonly Color Line = UiTheme.Border;
    private static readonly Color Track = UiTheme.Raised;
    private static readonly Color TextMain = UiTheme.Text;
    private static readonly Color TextMuted = UiTheme.Muted;

    private readonly AudioControl _audio;
    private readonly QuickActions _quick = new QuickActions();
    private readonly Font _fontLabel;
    private readonly Font _fontValue;
    private readonly Font _fontTitle;
    private readonly System.Windows.Forms.Timer _poll;
    private float _dpi;
    private float _fit;
    private float _textFit;
    private QuickHit _hover = QuickHit.None;
    private DateTime _lastPoll = DateTime.MinValue;

    private const int TileCount = 10;
    private const int TileCols = 3;

    public QuickSettingsPopup(AudioControl audio, float dpi, float fit, float textFit)
    {
        _audio = audio;
        _dpi = dpi < 96f ? 96f : dpi;
        _fit = fit < 0.62f ? 0.62f : fit;
        _textFit = textFit < 0.8f ? 0.8f : textFit;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Fill;
        // Fully opaque — layered Opacity below 1 softens ClearType on the drawer.
        Opacity = OpacityFade.Normal;
        DoubleBuffered = true;
        Text = "Quick settings";
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        float text = FontEm();
        _fontLabel = ChromeFonts.CreateLabel(10.5f, text);
        _fontValue = ChromeFonts.CreateUi(11f, text);
        _fontTitle = ChromeFonts.CreateUi(11.5f, text);
        _poll = new System.Windows.Forms.Timer();
        _poll.Interval = 120;
        _poll.Tick += PollTick;
        KeyPreview = true;
        ApplyLayoutSize();
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
            cp.ExStyle &= ~0x00000008;
            cp.ExStyle &= ~0x00040000;
            return cp;
        }
    }

    public bool ContainsScreenPoint(Point screenPt)
    {
        return Visible && Bounds.Contains(screenPt);
    }

    public void ShowBesidePanel(Rectangle panelScreen)
    {
        _audio.Refresh();
        RefreshState(true);
        ApplyLayoutSize();
        PlaceBesidePanel(panelScreen);
        if (!Visible)
        {
            // Binary show — no opacity ramp (intermediate Opacity blurs drawer text).
            Opacity = OpacityFade.Normal;
            Show();
            _poll.Start();
        }
        else
            Opacity = OpacityFade.Normal;
        RaiseAbove();
        Invalidate();
    }

    public void SyncBesidePanel(Rectangle panelScreen)
    {
        if (!Visible)
            return;
        PlaceBesidePanel(panelScreen);
        RaiseAbove();
    }

    public void RaiseAbove()
    {
        if (!IsHandleCreated || !Visible)
            return;
        ShellChrome.ZOrder(Handle, true);
    }

    private void PlaceBesidePanel(Rectangle panelScreen)
    {
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        int gap = Px(8);
        int left = panelScreen.Left - Width - gap;
        // Same top level as the main panel (not vertically centered mid-window).
        int top = panelScreen.Top;
        if (left < work.Left + Px(8))
            left = work.Left + Px(8);
        if (left + Width > panelScreen.Left - gap)
            left = Math.Max(work.Left + Px(8), panelScreen.Left - Width - gap);
        if (left + Width > work.Right - Px(8))
            left = work.Right - Width - Px(8);
        if (top < work.Top + Px(8))
            top = work.Top + Px(8);
        if (top + Height > work.Bottom - Px(8))
            top = Math.Max(work.Top + Px(8), work.Bottom - Height - Px(8));
        Location = new Point(left, top);
    }

    public void HidePopup()
    {
        _poll.Stop();
        Opacity = OpacityFade.Normal;
        _quick.Armed = QuickArm.None;
        if (Visible)
            Hide();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _poll.Stop();
        _fontLabel.Dispose();
        _fontValue.Dispose();
        _fontTitle.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            HidePopup();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void PollTick(object sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastPoll).TotalMilliseconds < 450)
            return;
        RefreshState(false);
        Invalidate();
    }

    private void RefreshState(bool force)
    {
        _lastPoll = DateTime.UtcNow;
        float volUnused;
        bool muted;
        if (_audio.TryGetVolume(out volUnused, out muted))
        {
            _quick.Muted = muted;
            if (force)
                _quick.StatusLine = "";
        }
        else if (force)
            _quick.StatusLine = _audio.Status;
        _quick.ClearArmIfExpired();
    }

    private float FontEm()
    {
        float t = _fit * _textFit;
        if (t < 0.55f)
            t = 0.55f;
        return t;
    }

    private int Px(int dip)
    {
        return (int)Math.Round(dip * _dpi / 96f * _fit);
    }

    private static int TileRows()
    {
        return (TileCount + TileCols - 1) / TileCols;
    }

    private void ApplyLayoutSize()
    {
        int pad = Px(16);
        int w = Px(320);
        int y = pad;
        y += SectionHeadH() + Px(8);
        int gap = Px(8);
        int innerW = w - pad * 2;
        int cell = Math.Min(Px(92), (innerW - gap * 2) / TileCols);
        int rows = TileRows();
        y += rows * cell + (rows - 1) * gap + pad;
        if (!string.IsNullOrEmpty(_quick.StatusLine))
            y += Px(18);
        Size = new Size(w, y);
        ApplyRegion();
    }

    private int SectionHeadH()
    {
        return Px(14) + Px(8);
    }

    private void ApplyRegion()
    {
        if (Width < 4 || Height < 4)
            return;
        using (GraphicsPath path = Round(new Rectangle(0, 0, Width, Height), Px(14)))
        {
            Region old = Region;
            Region = new Region(path);
            if (old != null)
                old.Dispose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Match panel/network path — ClearType looks soft on layered owner-draw chrome.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Fill);
        using (GraphicsPath path = Round(new Rectangle(0, 0, Width - 1, Height - 1), Px(14)))
        using (Pen pen = new Pen(Line, 1f))
            g.DrawPath(pen, path);
        LayoutAndDraw(g);
    }

    private void LayoutAndDraw(Graphics g)
    {
        _quick.ClearHits();
        _quick.ClearArmIfExpired();
        int pad = Px(16);
        int x = pad;
        int inner = Width - pad * 2;
        int y = pad;
        int gap = Px(8);

        y = DrawSectionHead(g, "Actions", x, y, inner);
        int cell = Math.Min(Px(92), (inner - gap * 2) / TileCols);
        int gridW = TileCols * cell + (TileCols - 1) * gap;
        int gridX = x + (inner - gridW) / 2;
        string[] labels = new string[]
        {
            "Explorer", "Lock", "Sleep",
            _quick.Armed == QuickArm.Restart ? "Confirm" : "Restart",
            _quick.Armed == QuickArm.Shutdown ? "Confirm" : "Shut down",
            "Display",
            "Sound",
            _quick.Muted ? "Unmute" : "Mute",
            "Relaunch",
            "Text"
        };
        QuickHit[] hits = new QuickHit[]
        {
            QuickHit.TileExplorer, QuickHit.TileLock, QuickHit.TileSleep,
            QuickHit.TileRestart, QuickHit.TileShutdown, QuickHit.TileDisplaySettings,
            QuickHit.TileSoundSettings, QuickHit.TileMute, QuickHit.TileAppRestart,
            QuickHit.TileTextSettings
        };
        Action<Graphics, Rectangle, Color>[] glyphs = new Action<Graphics, Rectangle, Color>[]
        {
            QuickActions.GlyphExplorer, QuickActions.GlyphLock, QuickActions.GlyphSleep,
            QuickActions.GlyphRestart, QuickActions.GlyphShutdown, QuickActions.GlyphSettings,
            QuickActions.GlyphSpeakerOpen, QuickActions.GlyphSpeakerOpen, QuickActions.GlyphAppRestart,
            QuickActions.GlyphTextAa
        };
        for (int i = 0; i < TileCount; i++)
        {
            int col = i % TileCols;
            int row = i / TileCols;
            Rectangle cellRect = new Rectangle(gridX + col * (cell + gap), y + row * (cell + gap), cell, cell);
            AssignTile(hits[i], cellRect);
            bool hover = _hover == hits[i];
            bool armed = (hits[i] == QuickHit.TileRestart && _quick.Armed == QuickArm.Restart)
                || (hits[i] == QuickHit.TileShutdown && _quick.Armed == QuickArm.Shutdown);
            if (hits[i] == QuickHit.TileMute)
                DrawMuteGridTile(g, cellRect, labels[i], hover);
            else
                QuickActions.DrawGridTile(g, cellRect, labels[i], _fontValue, hover, armed, Track, Line, TextMain, TextMuted, glyphs[i]);
        }
        y += TileRows() * (cell + gap);
        if (!string.IsNullOrEmpty(_quick.StatusLine))
        {
            using (SolidBrush brush = new SolidBrush(TextMuted))
                g.DrawString(_quick.StatusLine, _fontLabel, brush, x, y);
        }
    }

    private int DrawSectionHead(Graphics g, string title, int x, int y, int inner)
    {
        QuickActions.DrawSectionTitle(g, _fontTitle, title, x, y, inner, TextMuted);
        return y + SectionHeadH();
    }

    private void DrawMuteGridTile(Graphics g, Rectangle rect, string label, bool hover)
    {
        QuickActions.DrawGridTile(g, rect, label, _fontValue, hover, false, Track, Line, TextMain, TextMuted,
            delegate(Graphics gg, Rectangle r, Color ink)
            {
                QuickActions.GlyphSpeaker(gg, r, ink, _quick.Muted);
            });
    }

    private void AssignTile(QuickHit hit, Rectangle r)
    {
        if (hit == QuickHit.TileExplorer) _quick.TileExplorer = r;
        else if (hit == QuickHit.TileLock) _quick.TileLock = r;
        else if (hit == QuickHit.TileSleep) _quick.TileSleep = r;
        else if (hit == QuickHit.TileRestart) _quick.TileRestart = r;
        else if (hit == QuickHit.TileShutdown) _quick.TileShutdown = r;
        else if (hit == QuickHit.TileDisplaySettings) _quick.TileDisplaySettings = r;
        else if (hit == QuickHit.TileSoundSettings) _quick.TileSoundSettings = r;
        else if (hit == QuickHit.TileTextSettings) _quick.TileTextSettings = r;
        else if (hit == QuickHit.TileMute) _quick.TileMute = r;
        else if (hit == QuickHit.TileAppRestart) _quick.TileAppRestart = r;
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

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;
        HandleClick(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        QuickHit prev = _hover;
        _hover = _quick.HitTest(e.Location);
        Cursor = _hover != QuickHit.None ? Cursors.Hand : Cursors.Default;
        if (prev != _hover)
            Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != QuickHit.None)
        {
            _hover = QuickHit.None;
            Invalidate();
        }
    }

    private void HandleClick(Point p)
    {
        QuickHit hit = _quick.HitTest(p);
        _quick.ClearArmIfExpired();
        if (hit == QuickHit.TileMute)
        {
            bool next = !_quick.Muted;
            if (_audio.TrySetMute(next))
            {
                _quick.Muted = next;
                _quick.StatusLine = "";
            }
            else
                _quick.StatusLine = _audio.Status;
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileExplorer)
        {
            _quick.StatusLine = SystemActions.RestartExplorer() ? "" : SystemActions.LastError;
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileLock)
        {
            if (!SystemActions.LockWorkstation())
                _quick.StatusLine = SystemActions.LastError;
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileSleep)
        {
            if (!SystemActions.Sleep())
                _quick.StatusLine = SystemActions.LastError;
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileRestart)
        {
            if (_quick.Armed == QuickArm.Restart)
            {
                _quick.Armed = QuickArm.None;
                SystemActions.RestartPc(false);
            }
            else
                _quick.Arm(QuickArm.Restart);
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileShutdown)
        {
            if (_quick.Armed == QuickArm.Shutdown)
            {
                _quick.Armed = QuickArm.None;
                SystemActions.ShutdownPc(false);
            }
            else
                _quick.Arm(QuickArm.Shutdown);
            Invalidate();
            return;
        }
        if (hit == QuickHit.TileDisplaySettings)
        {
            SystemActions.OpenSettings("ms-settings:display");
            return;
        }
        if (hit == QuickHit.TileSoundSettings)
        {
            SystemActions.OpenSettings("ms-settings:sound");
            return;
        }
        if (hit == QuickHit.TileTextSettings)
        {
            // Accessibility → Text size (“Make text bigger”)
            SystemActions.OpenSettings("ms-settings:easeofaccess-display");
            return;
        }
        if (hit == QuickHit.TileAppRestart)
        {
            if (!SystemActions.RestartQuickView())
            {
                _quick.StatusLine = SystemActions.LastError;
                Invalidate();
            }
        }
    }
}
