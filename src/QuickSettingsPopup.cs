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
    private static readonly Color Accent = UiTheme.Accent;

    private readonly AudioControl _audio;
    private readonly QuickActions _quick = new QuickActions();
    private readonly Font _fontLabel;
    private readonly Font _fontValue;
    private readonly Font _fontTitle;
    private readonly System.Windows.Forms.Timer _poll;
    private readonly System.Windows.Forms.Timer _fade;
    private float _dpi;
    private float _fit;
    private float _textFit;
    private QuickHit _hover = QuickHit.None;
    private DateTime _lastPoll = DateTime.MinValue;
    private double _fadeFrom = OpacityFade.Normal;
    private double _fadeTo = OpacityFade.Normal;
    private DateTime _fadeStartedUtc = DateTime.MinValue;
    private int _fadeDurationMs;
    private bool _fadeRunning;

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
        // Near-opaque — translucent layers make thin text look soft/blurry.
        Opacity = OpacityFade.Normal;
        DoubleBuffered = true;
        Text = "Quick settings";
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        float text = FontEm();
        _fontLabel = new Font("Segoe UI", 10f * text, FontStyle.Regular, GraphicsUnit.Point);
        _fontValue = new Font("Segoe UI", 10.5f * text, FontStyle.Regular, GraphicsUnit.Point);
        _fontTitle = new Font("Segoe UI", 11f * text, FontStyle.Regular, GraphicsUnit.Point);
        _poll = new System.Windows.Forms.Timer();
        _poll.Interval = 120;
        _poll.Tick += PollTick;
        _fade = new System.Windows.Forms.Timer();
        _fade.Interval = 16;
        _fade.Tick += FadeTick;
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
            // Instant show + short ease-in — open dwell lives on the panel hover, not here.
            Opacity = OpacityFade.DrawerShowFrom;
            Show();
            _poll.Start();
            StartShowFade();
        }
        else if (!_fadeRunning)
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
        StopShowFade();
        Opacity = OpacityFade.Normal;
        _quick.Armed = QuickArm.None;
        if (Visible)
            Hide();
    }

    private void StartShowFade()
    {
        _fadeFrom = Opacity;
        _fadeTo = OpacityFade.Normal;
        _fadeDurationMs = OpacityFade.DrawerShowMs;
        _fadeStartedUtc = DateTime.UtcNow;
        _fadeRunning = true;
        if (!_fade.Enabled)
            _fade.Start();
    }

    private void StopShowFade()
    {
        _fade.Stop();
        _fadeRunning = false;
    }

    private void FadeTick(object sender, EventArgs e)
    {
        if (!_fadeRunning)
        {
            _fade.Stop();
            return;
        }
        int elapsed = (int)(DateTime.UtcNow - _fadeStartedUtc).TotalMilliseconds;
        Opacity = OpacityFade.OpacityAt(_fadeFrom, _fadeTo, elapsed, _fadeDurationMs);
        if (OpacityFade.FadeComplete(elapsed, _fadeDurationMs))
        {
            Opacity = _fadeTo;
            StopShowFade();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _poll.Stop();
        StopShowFade();
        _fade.Dispose();
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
        float vol;
        bool muted;
        if (_audio.TryGetVolume(out vol, out muted))
        {
            _quick.Volume01 = vol;
            _quick.Muted = muted;
            if (force)
                _quick.StatusLine = "";
        }
        else if (force)
            _quick.StatusLine = _audio.Status;
        int applied = SystemActions.GetTextScalePercent();
        _quick.TextAppliedPercent = applied;
        if (!_quick.TextDirty || force)
        {
            _quick.TextPercent = applied;
            _quick.TextDirty = false;
        }
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

    private void ApplyLayoutSize()
    {
        int pad = Px(16);
        int w = Px(320);
        int y = pad;
        // Text size: label + A± row + Apply
        y += Px(22) + Px(8) + Px(32) + Px(8) + Px(32) + BlockGap();
        y += SectionHeadH() + Px(8);
        int gap = Px(8);
        int innerW = w - pad * 2;
        int cell = Math.Min(Px(92), (innerW - gap * 2) / 3);
        y += 3 * cell + 2 * gap + pad;
        if (!string.IsNullOrEmpty(_quick.StatusLine))
            y += Px(18);
        Size = new Size(w, y);
        ApplyRegion();
    }

    private int SectionHeadH()
    {
        return Px(14) + Px(8);
    }

    private int BlockGap()
    {
        return Px(16);
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
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
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
        int side = Px(32);

        // Text size: A− · A+ · Apply (Settings > Accessibility > Text size)
        string textValue = _quick.TextPercent + "%" + (_quick.TextDirty ? " *" : "");
        y = DrawLabelRow(g, x, y, inner, "Text size", textValue);
        int half = (inner - gap) / 2;
        _quick.TextMinusRect = new Rectangle(x, y, half, side);
        _quick.TextPlusRect = new Rectangle(x + half + gap, y, inner - half - gap, side);
        FillRound(g, _quick.TextMinusRect, Px(8), _hover == QuickHit.TextMinus ? UiTheme.Hover : Track);
        FillRound(g, _quick.TextPlusRect, Px(8), _hover == QuickHit.TextPlus ? UiTheme.Hover : Track);
        using (SolidBrush brush = new SolidBrush(TextMain))
        using (StringFormat fmt = CenterFormat())
        {
            g.DrawString("A−", _fontLabel, brush, _quick.TextMinusRect, fmt);
            g.DrawString("A+", _fontLabel, brush, _quick.TextPlusRect, fmt);
        }
        y += side + gap;
        int applyH = side;
        _quick.TextApplyRect = new Rectangle(x, y, inner, applyH);
        bool applyHot = _hover == QuickHit.TextApply && _quick.TextDirty;
        Color applyBack = !_quick.TextDirty
            ? Color.FromArgb(255, 40, 44, 50)
            : (applyHot ? Accent : Color.FromArgb(255, 70, 110, 180));
        Color applyInk = _quick.TextDirty ? TextMain : TextMuted;
        FillRound(g, _quick.TextApplyRect, Px(8), applyBack);
        using (SolidBrush brush = new SolidBrush(applyInk))
        using (StringFormat fmt = CenterFormat())
            g.DrawString("Apply", _fontLabel, brush, _quick.TextApplyRect, fmt);
        y += applyH + BlockGap();

        y = DrawSectionHead(g, "Actions", x, y, inner);
        int cell = Math.Min(Px(92), (inner - gap * 2) / 3);
        int gridW = 3 * cell + 2 * gap;
        int gridX = x + (inner - gridW) / 2;
        string[] labels = new string[]
        {
            "Explorer", "Lock", "Sleep",
            _quick.Armed == QuickArm.Restart ? "Confirm" : "Restart",
            _quick.Armed == QuickArm.Shutdown ? "Confirm" : "Shut down",
            "Display",
            "Sound",
            _quick.Muted ? "Unmute" : "Mute",
            "Relaunch"
        };
        QuickHit[] hits = new QuickHit[]
        {
            QuickHit.TileExplorer, QuickHit.TileLock, QuickHit.TileSleep,
            QuickHit.TileRestart, QuickHit.TileShutdown, QuickHit.TileDisplaySettings,
            QuickHit.TileSoundSettings, QuickHit.TileMute, QuickHit.TileAppRestart
        };
        Action<Graphics, Rectangle, Color>[] glyphs = new Action<Graphics, Rectangle, Color>[]
        {
            QuickActions.GlyphExplorer, QuickActions.GlyphLock, QuickActions.GlyphSleep,
            QuickActions.GlyphRestart, QuickActions.GlyphShutdown, QuickActions.GlyphSettings,
            QuickActions.GlyphSpeakerOpen, QuickActions.GlyphSpeakerOpen, QuickActions.GlyphAppRestart
        };
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3;
            int row = i / 3;
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
        y += 3 * (cell + gap);
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

    private int DrawLabelRow(Graphics g, int x, int y, int inner, string title, string value)
    {
        int h = Px(22);
        int valueW = Px(64);
        using (SolidBrush brush = new SolidBrush(TextMain))
        using (StringFormat near = new StringFormat())
        using (StringFormat far = new StringFormat())
        {
            near.Alignment = StringAlignment.Near;
            near.LineAlignment = StringAlignment.Center;
            near.Trimming = StringTrimming.EllipsisCharacter;
            near.FormatFlags = StringFormatFlags.NoWrap;
            far.Alignment = StringAlignment.Far;
            far.LineAlignment = StringAlignment.Center;
            far.FormatFlags = StringFormatFlags.NoWrap;
            g.DrawString(title, _fontValue, brush, new Rectangle(x, y, Math.Max(0, inner - valueW - Px(8)), h), near);
            g.DrawString(value, _fontValue, brush, new Rectangle(x + inner - valueW, y, valueW, h), far);
        }
        return y + h + Px(8);
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
        else if (hit == QuickHit.TileMute) _quick.TileMute = r;
        else if (hit == QuickHit.TileAppRestart) _quick.TileAppRestart = r;
    }

    private static StringFormat CenterFormat()
    {
        StringFormat fmt = new StringFormat();
        fmt.Alignment = StringAlignment.Center;
        fmt.LineAlignment = StringAlignment.Center;
        fmt.Trimming = StringTrimming.EllipsisCharacter;
        fmt.FormatFlags = StringFormatFlags.NoWrap;
        return fmt;
    }

    private static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
    {
        using (GraphicsPath path = Round(rect, radius))
        using (SolidBrush brush = new SolidBrush(color))
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

    private void SetTextPending(int percent)
    {
        percent = TextScale.ClampAndSnap(percent);
        _quick.TextPercent = percent;
        _quick.TextDirty = percent != _quick.TextAppliedPercent;
        _quick.StatusLine = "";
        Invalidate();
    }

    private void CommitTextScale()
    {
        if (!_quick.TextDirty)
            return;
        if (SystemActions.SetTextScalePercent(_quick.TextPercent))
        {
            _quick.TextPercent = SystemActions.GetTextScalePercent();
            _quick.TextAppliedPercent = _quick.TextPercent;
            _quick.TextDirty = false;
            _quick.StatusLine = "";
        }
        else
            _quick.StatusLine = SystemActions.LastError;
        Invalidate();
    }

    private void HandleClick(Point p)
    {
        QuickHit hit = _quick.HitTest(p);
        _quick.ClearArmIfExpired();
        if (hit == QuickHit.TextMinus)
        {
            SetTextPending(TextScale.StepDown(_quick.TextPercent));
            return;
        }
        if (hit == QuickHit.TextPlus)
        {
            SetTextPending(TextScale.StepUp(_quick.TextPercent, false));
            return;
        }
        if (hit == QuickHit.TextApply)
        {
            CommitTextScale();
            return;
        }
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
