using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal enum DetailMode
{
    None,
    Cpu,
    Memory,
    Gpu,
    Disk
}

internal sealed class PanelForm : Form
{
    private static readonly Color Fill = UiTheme.Bg;
    private static readonly Color Line = UiTheme.Border;
    private static readonly Color Track = UiTheme.Raised;
    private static readonly Color TextMain = UiTheme.Text;
    private static readonly Color TextMuted = UiTheme.Muted;
    private static readonly Color Cpu = UiTheme.Cpu;
    private static readonly Color Mem = UiTheme.Mem;
    private static readonly Color Gpu = UiTheme.Gpu;
    private static readonly Color InternalInk = UiTheme.InternalInk;
    private static readonly Color PlatformInk = UiTheme.PlatformInk;
    private static readonly Color InstalledInk = UiTheme.InstalledInk;
    private static readonly Color TempCool = UiTheme.TempCool;
    private static readonly Color TempWarm = UiTheme.TempWarm;
    private static readonly Color TempHot = UiTheme.TempHot;

    private readonly Sampler _sampler;
    private readonly System.Windows.Forms.Timer _mouseTimer;
    private Font _fontLabel;
    private Font _fontValue;
    private Snapshot _snap = Snapshot.Empty();
    private DetailMode _detail = DetailMode.None;
    private bool _shown;
    private bool _syncing;
    private float _dpi = 96f;
    private float _fit = 1f;
    private DisplayProfile _display;
    private int _textH;
    private int _scroll;
    private float _scrollPos;
    private int _scrollTarget;
    private int _headH;
    private int _docH;
    private int _gutter;
    private bool _dragging;
    private int _dragY;
    private int _dragScroll;
    private Rectangle _barRect;
    private Rectangle _thumbRect;
    private readonly WheelFilter _wheel;
    private readonly QuickActions _quick = new QuickActions();
    private readonly AudioControl _audio = new AudioControl();
    private readonly BrightnessControl _brightness = new BrightnessControl();
    private readonly ToolTip _tip = new ToolTip();
    private QuickSettingsPopup _quickPopup;
    private DateTime _quickHoverSince = DateTime.MinValue;
    private DateTime _quickLeaveSince = DateTime.MinValue;
    private bool _chevronHot;
    private bool _wifiHot;
    private Rectangle _chevronHit;
    private QuickHit _hover = QuickHit.None;
    private bool _chipHoverCpu;
    private bool _chipHoverMem;
    private bool _chipHoverGpu;
    private bool _chipHoverDisk;
    private bool _trimHover;
    private string _lastTip = "";

    private Rectangle _cpuChip;
    private Rectangle _memChip;
    private Rectangle _gpuChip;
    private Rectangle _diskChip;
    private Rectangle _trimRect;
    private Rectangle _wifiHit;
    private Rectangle _batteryHit;
    private bool _batteryHot;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    public PanelForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = Fill;
        Opacity = 0.94;
        DoubleBuffered = true;
        Text = "QuickView";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        AutoScaleMode = AutoScaleMode.None;
        RemeasureDisplay();
        RebuildFonts();
        _tip.ShowAlways = false;
        _tip.AutoPopDelay = 2500;
        _tip.InitialDelay = 400;
        _tip.ReshowDelay = 200;
        _sampler = new Sampler();
        _sampler.Sampled += OnSampled;
        _mouseTimer = new System.Windows.Forms.Timer();
        _mouseTimer.Interval = 16;
        _mouseTimer.Tick += MouseTick;
        _mouseTimer.Start();
        _wheel = new WheelFilter(this);
        Application.AddMessageFilter(_wheel);
        ApplySize();
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
            ShellChrome.Prepare(cp);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RemeasureDisplay();
        RebuildFonts();
        ApplySize();
        Place(false, false);
        ShellChrome.Pin(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _mouseTimer.Stop();
        _sampler.Sampled -= OnSampled;
        _sampler.Dispose();
        CloseQuickPopup();
        if (_quickPopup != null)
        {
            _quickPopup.Dispose();
            _quickPopup = null;
        }
        _audio.Dispose();
        _brightness.Dispose();
        _tip.Dispose();
        _fontLabel.Dispose();
        _fontValue.Dispose();
        Application.RemoveMessageFilter(_wheel);
        base.OnFormClosed(e);
    }

    private void OnSampled(Snapshot snap)
    {
        if (snap == null)
            return;
        if (!IsHandleCreated)
        {
            _snap = snap;
            return;
        }
        try
        {
            BeginInvoke(new Action(delegate
            {
                _snap = snap;
                ApplySize();
                Invalidate();
            }));
        }
        catch
        {
        }
    }

    private void RefreshDpi()
    {
        try
        {
            if (IsHandleCreated)
            {
                uint dpi = GetDpiForWindow(Handle);
                if (dpi >= 96)
                {
                    _dpi = dpi;
                    return;
                }
            }
        }
        catch
        {
        }
        _dpi = 96f;
    }

    // First start, restart, DPI change, and monitor plug/unplug.
    private void RemeasureDisplay()
    {
        RefreshDpi();
        _display = DisplayScale.Measure(_dpi);
        _dpi = _display.Dpi;
        _fit = _display.Fit;
    }

    private void RebuildFonts()
    {
        float fit = Fit();
        Font oldLabel = _fontLabel;
        Font oldValue = _fontValue;
        _fontLabel = new Font("Segoe UI", 11f * fit, FontStyle.Regular, GraphicsUnit.Point);
        _fontValue = new Font("Segoe UI", 12f * fit, FontStyle.Regular, GraphicsUnit.Point);
        _textH = (int)Math.Ceiling(_fontValue.GetHeight()) + 1;
        if (oldLabel != null)
            oldLabel.Dispose();
        if (oldValue != null)
            oldValue.Dispose();
    }

    private float Fit()
    {
        return _fit < 0.62f ? 0.62f : _fit;
    }

    private int Px(int dip)
    {
        return (int)Math.Round(dip * _dpi / 96f * Fit());
    }

    private int MaxHeight()
    {
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        int room = area.Height - Px(96);
        int prefer = Px(820);
        if (room > prefer)
            room = prefer;
        if (room < Px(240))
            room = Px(240);
        return room;
    }

    private int ContentInner()
    {
        return Px(380) - Px(32) - _gutter;
    }

    private void ApplySize()
    {
        int w = Px(380);
        _gutter = 0;
        _headH = Chrome(null);
        _docH = Body(null, _headH);
        int maxH = MaxHeight();
        if (_docH > maxH)
        {
            _gutter = Px(12);
            _headH = Chrome(null);
            _docH = Body(null, _headH);
        }
        int h = _docH < maxH ? _docH : maxH;
        ClampScroll(h);
        if (Width != w || Height != h)
        {
            bool keepTop = _shown && Bounds.Contains(Control.MousePosition);
            int heldTop = Top;
            Size = new Size(w, h);
            if (keepTop)
                Top = heldTop;
            ApplyRegion();
            if (_shown)
                Place(true, true);
        }
        else if (Region == null)
        {
            ApplyRegion();
        }
    }

    private void ClampScroll(int height)
    {
        int max = _docH - height;
        if (max < 0)
            max = 0;
        if (_scroll > max)
            _scroll = max;
        if (_scroll < 0)
            _scroll = 0;
        if (_scrollTarget > max)
            _scrollTarget = max;
        if (_scrollTarget < 0)
            _scrollTarget = 0;
        if (_scrollPos > max)
            _scrollPos = max;
        if (_scrollPos < 0f)
            _scrollPos = 0f;
    }

    private void ApplyRegion()
    {
        if (Width < 4 || Height < 4)
            return;
        using (GraphicsPath path = Round(new Rectangle(0, 0, Width, Height), Px(16)))
        {
            Region old = Region;
            Region = new Region(path);
            if (old != null)
                old.Dispose();
        }
    }

    private void MouseTick(object sender, EventArgs e)
    {
        if (_syncing)
            return;
        _syncing = true;
        try
        {
            TickSmoothScroll();
            TickQuickPopup(Control.MousePosition);
            Point pt = Control.MousePosition;
            bool inside = Bounds.Contains(pt);
            // Panel-only: DPI inset leaves a gap between the 24px edge strip and the
            // panel's right edge. Count that strip as still "on us". Does not change dock.
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            bool edgeBridge = _shown
                && pt.X > Bounds.Right && pt.X <= screen.Right
                && pt.Y >= Bounds.Top && pt.Y <= Bounds.Bottom;
            bool onPopup = _quickPopup != null && _quickPopup.ContainsScreenPoint(pt);
            bool onPanel = inside || edgeBridge || onPopup;
            bool pressing = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left && onPanel;
            bool pointerOnUs = onPanel;
            ShellView view = ShellChrome.Decide(ShellEdge.Right, pointerOnUs, pressing, false);
            if (view == ShellView.Park)
                Place(false, false);
            else
                Place(true, view == ShellView.Reveal);
            if (IsHandleCreated)
                ShellChrome.PinSoon(Handle);
            RaiseQuickPopup();
        }
        catch
        {
        }
        finally
        {
            _syncing = false;
        }
    }

    private void TickSmoothScroll()
    {
        float gap = _scrollTarget - _scrollPos;
        if (Math.Abs(gap) < 0.5f)
        {
            if (_scroll == _scrollTarget)
                return;
            _scrollPos = _scrollTarget;
            _scroll = _scrollTarget;
            ClampScroll(Height);
            Invalidate();
            return;
        }
        // Snappy ease — the 7 ms / 0.18 path felt laggy on 144 Hz.
        _scrollPos += gap * 0.55f;
        _scroll = (int)Math.Round(_scrollPos);
        ClampScroll(Height);
        Invalidate();
    }

    private void TickQuickPopup(Point screenPt)
    {
        bool overChevron = false;
        if (IsHandleCreated && _chevronHit.Width > 0)
            overChevron = _chevronHit.Contains(PointToClient(screenPt));
        bool overPopup = _quickPopup != null && _quickPopup.ContainsScreenPoint(screenPt);
        bool wasHot = _chevronHot;
        _chevronHot = overChevron;
        if (overChevron)
        {
            _quickLeaveSince = DateTime.MinValue;
            if (_quickHoverSince == DateTime.MinValue)
                _quickHoverSince = DateTime.UtcNow;
            else if ((_quickPopup == null || !_quickPopup.Visible)
                && (DateTime.UtcNow - _quickHoverSince).TotalMilliseconds >= 280)
                OpenQuickPopup();
        }
        else
            _quickHoverSince = DateTime.MinValue;

        if (_quickPopup != null && _quickPopup.Visible)
        {
            if (overChevron || overPopup)
                _quickLeaveSince = DateTime.MinValue;
            else
            {
                if (_quickLeaveSince == DateTime.MinValue)
                    _quickLeaveSince = DateTime.UtcNow;
                else if ((DateTime.UtcNow - _quickLeaveSince).TotalMilliseconds >= 200)
                    CloseQuickPopup();
            }
        }
        if (wasHot != _chevronHot)
            Invalidate();
    }

    private void OpenQuickPopup()
    {
        if (!IsHandleCreated)
            return;
        if (_quickPopup == null || _quickPopup.IsDisposed)
            _quickPopup = new QuickSettingsPopup(_audio, _brightness, _dpi, Fit());
        _quickPopup.ShowBesidePanel(Bounds);
        RaiseQuickPopup();
        _quickLeaveSince = DateTime.MinValue;
        Invalidate();
    }

    private void CloseQuickPopup()
    {
        if (_quickPopup != null && !_quickPopup.IsDisposed && _quickPopup.Visible)
            _quickPopup.HidePopup();
        _quickLeaveSince = DateTime.MinValue;
        Invalidate();
    }

    private bool QuickPopupOpen()
    {
        return _quickPopup != null && !_quickPopup.IsDisposed && _quickPopup.Visible;
    }

    private void RaiseQuickPopup()
    {
        if (_quickPopup != null && !_quickPopup.IsDisposed && _quickPopup.Visible)
            _quickPopup.SyncBesidePanel(Bounds);
    }

    private void ToggleQuickPopup()
    {
        if (QuickPopupOpen())
            CloseQuickPopup();
        else
            OpenQuickPopup();
    }

    private void Place(bool show, bool topmost)
    {
        Rectangle screen = Screen.PrimaryScreen.WorkingArea;
        int left = show ? screen.Right - Width - Px(20) : -32000;
        int top = screen.Top + Math.Max(Px(16), (screen.Height - Height) / 2);
        Point cursor = Control.MousePosition;
        bool here = _shown && Bounds.Contains(cursor);
        if (show && here)
            top = Top;
        if (show && !here)
        {
            int limit = screen.Bottom - Height - Px(12);
            if (top > limit)
                top = limit;
            int min = screen.Top + Px(8);
            if (top < min)
                top = min;
        }
        if (show && IsHandleCreated)
            ShellChrome.RestoreIfIconic(Handle);
        if (_shown != show || Left != left || Top != top)
        {
            if (!show && _shown)
                CloseQuickPopup();
            _shown = show;
            Left = left;
            Top = top;
        }
        if (show && IsHandleCreated)
        {
            ShellChrome.ZOrder(Handle, topmost);
            RaiseQuickPopup();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && QuickPopupOpen())
        {
            CloseQuickPopup();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_DPICHANGED = 0x02E0;
        const int WM_DISPLAYCHANGE = 0x007E;
        if (ShellChrome.SwallowMinimize(ref m))
            return;
        if (m.Msg == WM_DPICHANGED || m.Msg == WM_DISPLAYCHANGE)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                int dpi = m.WParam.ToInt32() & 0xFFFF;
                if (dpi >= 96)
                    _dpi = dpi;
            }
            RemeasureDisplay();
            RebuildFonts();
            CloseQuickPopup();
            if (_quickPopup != null)
            {
                _quickPopup.Dispose();
                _quickPopup = null;
            }
            ApplySize();
            if (_shown)
                Place(true, true);
            Invalidate();
            if (m.Msg == WM_DPICHANGED)
                return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // ClearType looks soft on a translucent window; grayscale AA stays sharp.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Fill);
        using (GraphicsPath path = Round(new Rectangle(0, 0, Width - 1, Height - 1), Px(16)))
        using (Pen pen = new Pen(Line, 1f))
        {
            g.DrawPath(pen, path);
        }
        _barRect = Rectangle.Empty;
        _thumbRect = Rectangle.Empty;
        _headH = Chrome(g);
        GraphicsState state = g.Save();
        g.SetClip(new Rectangle(0, _headH, Width, Math.Max(0, Height - _headH)));
        g.TranslateTransform(0, -_scroll);
        _docH = Body(g, _headH);
        g.Restore(state);
        if (_gutter > 0)
            DrawBar(g);
    }

    private int Chrome(Graphics g)
    {
        _trimRect = Rectangle.Empty;
        int x = Px(16);
        int y = Px(14);
        int inner = ContentInner();
        y = Header(g, x, y, inner);
        return Chips(g, x, y, inner);
    }

    private int Body(Graphics g, int y)
    {
        int x = Px(16);
        int inner = ContentInner();
        if (_detail == DetailMode.None)
            y = Resting(g, x, y, inner);
        else
        {
            y = PxGap(y, 4);
            if (g != null)
            {
                using (Pen hair = new Pen(Line, 1f))
                    g.DrawLine(hair, x, y, x + inner, y);
            }
            y = PxGap(y, 10);
            if (_detail == DetailMode.Cpu)
                y = CpuDetail(g, x, y, inner);
            else if (_detail == DetailMode.Memory)
                y = MemoryDetail(g, x, y, inner);
            else if (_detail == DetailMode.Gpu)
                y = GpuDetail(g, x, y, inner);
            else if (_detail == DetailMode.Disk)
                y = DiskDetail(g, x, y, inner);
        }
        return y + Px(14);
    }

    private int Header(Graphics g, int x, int y, int inner)
    {
        int row = Px(52);
        if (g != null)
        {
            DrawStatus(g, x, y, inner, row);
            int lineY = y + row + Px(6);
            using (Pen hair = new Pen(Line, 1f))
                g.DrawLine(hair, x, lineY, x + inner, lineY);
        }
        return y + row + Px(14);
    }

    private void DrawStatus(Graphics g, int x, int y, int inner, int row)
    {
        _wifiHit = Rectangle.Empty;
        _batteryHit = Rectangle.Empty;
        _chevronHit = Rectangle.Empty;
        _quick.IconRect = Rectangle.Empty;
        if (g == null)
            return;

        int gap = Px(8);
        int right = x + inner;
        int padY = Px(6);
        // Large hit target; glyph stays centered inside it.
        int chevHit = Math.Min(row - Px(4), Px(36));
        int chevGlyph = Px(18);
        _chevronHit = new Rectangle(x, y + (row - chevHit) / 2, chevHit, chevHit);
        bool open = QuickPopupOpen();
        if (_chevronHot || open)
            FillRound(g, _chevronHit, Px(8), UiTheme.Hover);
        Rectangle chevGlyphRect = new Rectangle(
            _chevronHit.X + (_chevronHit.Width - chevGlyph) / 2,
            _chevronHit.Y + (_chevronHit.Height - chevGlyph) / 2,
            chevGlyph,
            chevGlyph);
        DrawHeaderChevron(g, chevGlyphRect, open, open || _chevronHot ? TextMain : TextMuted);

        // Reserve battery width first so "100%" never clips.
        int battW = _snap.BatteryKnown ? EstimateBatteryWidth(g) : 0;
        int battLeft = right - battW;
        int wifiLeft = _chevronHit.Right + Px(6);
        int wifiRight = _snap.BatteryKnown ? battLeft - gap - Px(8) : right;
        if (_wifiHot && wifiRight > wifiLeft)
            FillRound(g, new Rectangle(wifiLeft, y, wifiRight - wifiLeft, row), Px(8), UiTheme.Hover);

        int wifi = Px(22);
        int nameH = (row - padY * 2) / 2;
        int nameTop = y + padY;
        int rateTop = nameTop + nameH;
        DrawWifi(g, new Rectangle(wifiLeft, nameTop + Math.Max(0, (nameH - wifi) / 2), wifi, wifi), _snap.WifiUp, true);
        int textX = wifiLeft + wifi + Px(8);
        int nameW = Math.Max(0, wifiRight - textX);
        using (SolidBrush brush = new SolidBrush(_snap.WifiUp ? TextMain : TextMuted))
        using (StringFormat fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Near;
            fmt.LineAlignment = StringAlignment.Center;
            fmt.FormatFlags = StringFormatFlags.NoWrap;
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            g.DrawString(NetworkLabel(), _fontValue, brush, new Rectangle(textX, nameTop, nameW, nameH), fmt);
        }
        DrawInlineRates(g, textX, rateTop, nameW, nameH);
        _wifiHit = new Rectangle(wifiLeft, y, Math.Max(0, wifiRight - wifiLeft), row);

        if (_snap.BatteryKnown)
        {
            int divX = battLeft - Px(8);
            using (Pen pen = new Pen(Line, 1f))
                g.DrawLine(pen, divX, y + padY, divX, y + row - padY);
            _batteryHit = new Rectangle(battLeft - Px(4), y, right - battLeft + Px(4), row);
            if (_batteryHot)
                FillRound(g, _batteryHit, Px(8), UiTheme.Hover);
            DrawHeaderBatteryLeft(g, battLeft, y, row, right);
        }
    }

    private static void DrawHeaderChevron(Graphics g, Rectangle r, bool pointRight, Color ink)
    {
        using (Pen pen = new Pen(ink, 1.7f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            int dx = Math.Max(3, r.Width / 5);
            int dy = Math.Max(4, r.Height / 4);
            if (pointRight)
            {
                g.DrawLine(pen, cx - dx / 2, cy - dy, cx + dx / 2, cy);
                g.DrawLine(pen, cx - dx / 2, cy + dy, cx + dx / 2, cy);
            }
            else
            {
                g.DrawLine(pen, cx + dx / 2, cy - dy, cx - dx / 2, cy);
                g.DrawLine(pen, cx + dx / 2, cy + dy, cx - dx / 2, cy);
            }
        }
    }

    private string NetworkLabel()
    {
        if (_snap.WifiUp)
        {
            if (!string.IsNullOrEmpty(_snap.WifiSsid))
                return _snap.WifiSsid;
            return "Wi-Fi";
        }
        if (_snap.NetKnown)
            return "Ethernet";
        return "Offline";
    }

    private int EstimateBatteryWidth(Graphics g)
    {
        string percent = _snap.BatteryPercent + "%";
        SizeF size = g.MeasureString(percent, _fontValue);
        int icons = 0;
        if ((_snap.AcKnown && _snap.PluggedIn) || (_snap.AcKnown && !_snap.PluggedIn))
            icons++;
        if (_snap.Charging)
            icons++;
        if (icons < 1)
            icons = 1;
        // Extra slack — GDI+ MeasureString under-reports slightly vs DrawString.
        return (int)Math.Ceiling(size.Width) + icons * (Px(14) + Px(4)) + Px(8);
    }

    private void DrawInlineRates(Graphics g, int x, int y, int width, int height)
    {
        int icon = Px(11);
        float down = _snap.NetKnown ? _snap.DownMBps : 0f;
        float up = _snap.NetKnown ? _snap.UpMBps : 0f;
        string dText = down.ToString("0.00") + " MB/s";
        string uText = up.ToString("0.00") + " MB/s";
        SizeF dSize = g.MeasureString(dText, _fontLabel);
        SizeF uSize = g.MeasureString(uText, _fontLabel);
        int dW = (int)Math.Ceiling(dSize.Width);
        int uW = (int)Math.Ceiling(uSize.Width);
        int gap = Px(12);
        int need = icon + Px(3) + dW + gap + icon + Px(3) + uW;
        if (need > width)
        {
            dText = down.ToString("0.00");
            uText = up.ToString("0.00");
            dSize = g.MeasureString(dText, _fontLabel);
            uSize = g.MeasureString(uText, _fontLabel);
            dW = (int)Math.Ceiling(dSize.Width);
            uW = (int)Math.Ceiling(uSize.Width);
            need = icon + Px(3) + dW + gap + icon + Px(3) + uW;
            if (need > width)
                return;
        }
        int gy = y + Math.Max(0, (height - icon) / 2);
        int ty = y + Math.Max(0, (height - _textH) / 2);
        DrawDown(g, new Rectangle(x, gy, icon, icon), UiTheme.Down);
        using (SolidBrush brush = new SolidBrush(UiTheme.Down))
            g.DrawString(dText, _fontLabel, brush, x + icon + Px(3), ty);
        int ux = x + icon + Px(3) + dW + gap;
        DrawUp(g, new Rectangle(ux, gy, icon, icon), UiTheme.Up);
        using (SolidBrush brush = new SolidBrush(UiTheme.Up))
            g.DrawString(uText, _fontLabel, brush, ux + icon + Px(3), ty);
    }

    private void DrawHeaderBatteryLeft(Graphics g, int x, int y, int row, int rightLimit)
    {
        string percent = _snap.BatteryPercent + "%";
        bool plug = _snap.AcKnown && _snap.PluggedIn;
        bool cell = _snap.AcKnown && !_snap.PluggedIn;
        bool bolt = _snap.Charging;
        int s = Px(14);
        int gap = Px(4);
        int cursor = x;
        int gy = y + (row - s) / 2;
        if (plug)
        {
            DrawPlug(g, new Rectangle(cursor, gy, s, s), TextMuted);
            cursor += s + gap;
        }
        else if (cell)
        {
            DrawCell(g, new Rectangle(cursor, gy, s, s), TextMuted);
            cursor += s + gap;
        }
        if (bolt)
        {
            DrawBolt(g, new Rectangle(cursor, gy, s, s), TextMuted);
            cursor += s + gap;
        }
        SizeF size = g.MeasureString(percent, _fontValue);
        int valueW = (int)Math.Ceiling(size.Width) + Px(2);
        if (cursor + valueW > rightLimit)
            valueW = Math.Max(Px(28), rightLimit - cursor);
        using (SolidBrush brush = new SolidBrush(TextMain))
        using (StringFormat fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Near;
            fmt.LineAlignment = StringAlignment.Center;
            fmt.FormatFlags = StringFormatFlags.NoWrap;
            fmt.Trimming = StringTrimming.None;
            g.DrawString(percent, _fontValue, brush, new Rectangle(cursor, y, valueW, row), fmt);
        }
    }

    private static void DrawWifi(Graphics g, Rectangle r, bool up, bool accent)
    {
        Color ink = !up ? UiTheme.Muted : (accent ? UiTheme.Down : UiTheme.Text);
        float stroke = Math.Max(1.8f, r.Width / 10f);
        using (Pen pen = new Pen(ink, stroke))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int cx = r.Left + r.Width / 2;
            int cy = r.Bottom - Math.Max(2, r.Height / 10);
            int a1 = Math.Max(4, r.Width / 4);
            int a2 = Math.Max(8, r.Width / 2);
            int a3 = Math.Max(12, (r.Width * 3) / 4);
            g.DrawArc(pen, cx - a1 / 2, cy - a1 / 2 - 1, a1, a1, 200, 140);
            g.DrawArc(pen, cx - a2 / 2, cy - a2 / 2 - 1, a2, a2, 200, 140);
            g.DrawArc(pen, cx - a3 / 2, cy - a3 / 2 - 1, a3, a3, 200, 140);
            int dot = Math.Max(2, r.Width / 8);
            using (SolidBrush brush = new SolidBrush(ink))
                g.FillEllipse(brush, cx - dot / 2, cy - dot / 2, dot, dot);
            if (!up)
                g.DrawLine(pen, r.Left + 2, r.Top + 2, r.Right - 2, r.Bottom - 2);
        }
    }

    private static void DrawDown(Graphics g, Rectangle r, Color color)
    {
        using (Pen pen = new Pen(color, 1.6f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int mid = r.Left + r.Width / 2;
            g.DrawLine(pen, mid, r.Top + 1, mid, r.Bottom - 3);
            g.DrawLine(pen, mid - 3, r.Bottom - 6, mid, r.Bottom - 2);
            g.DrawLine(pen, mid + 3, r.Bottom - 6, mid, r.Bottom - 2);
        }
    }

    private static void DrawUp(Graphics g, Rectangle r, Color color)
    {
        using (Pen pen = new Pen(color, 1.6f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int mid = r.Left + r.Width / 2;
            g.DrawLine(pen, mid, r.Top + 3, mid, r.Bottom - 1);
            g.DrawLine(pen, mid - 3, r.Top + 6, mid, r.Top + 2);
            g.DrawLine(pen, mid + 3, r.Top + 6, mid, r.Top + 2);
        }
    }

    private int Chips(Graphics g, int x, int y, int inner)
    {
        int gap = Px(8);
        int chipW = (inner - gap * 3) / 4;
        int h = Px(28);
        _cpuChip = new Rectangle(x, y, chipW, h);
        _memChip = new Rectangle(x + (chipW + gap), y, chipW, h);
        _gpuChip = new Rectangle(x + (chipW + gap) * 2, y, chipW, h);
        _diskChip = new Rectangle(x + (chipW + gap) * 3, y, inner - (chipW + gap) * 3, h);
        if (g != null)
        {
            DrawChip(g, _cpuChip, "CPU", _detail == DetailMode.Cpu, Cpu, _chipHoverCpu);
            DrawChip(g, _memChip, "Memory", _detail == DetailMode.Memory, Mem, _chipHoverMem);
            DrawChip(g, _gpuChip, "GPU", _detail == DetailMode.Gpu, Gpu, _chipHoverGpu);
            DrawChip(g, _diskChip, "Disk", _detail == DetailMode.Disk, TextMain, _chipHoverDisk);
        }
        return y + h + Px(14);
    }

    private int Resting(Graphics g, int x, int y, int inner)
    {
        y = CpuSection(g, x, y, inner);
        y = MemorySection(g, x, y, inner);
        y = DiskSection(g, x, y, inner);
        return GpuSection(g, x, y, inner);
    }

    private int CpuSection(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "CPU", false, _snap.CpuTempKnown, _snap.CpuCelsius);
        y = Graph(g, x, y, inner, _snap.CpuHistory, Cpu, false);
        y = Fact(g, x, y, inner, "Usage", _snap.CpuKnown ? Percent(_snap.CpuPercent) : "--");
        y = ProcessorFacts(g, x, y, inner);
        if (_snap.ClockKnown)
            y = Fact(g, x, y, inner, "Clock", FormatGHz(_snap.ClockMHz));
        if (_snap.CoreMHz != null && _snap.CoreMHz.Length > 0)
        {
            y = Caption(g, x, y, "Cores");
            y = CoreGrid(g, x, y, inner, _snap.CoreMHz);
        }
        return y;
    }

    private int MemorySection(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "Memory", true, _snap.MemTempKnown, _snap.MemCelsius);
        y = Graph(g, x, y, inner, _snap.MemoryHistory, Mem, true);
        if (_snap.MemoryTotal > 0)
            y = Fact(g, x, y, inner, "In use", FormatBytes(_snap.MemoryUsed) + " of " + FormatBytes(_snap.MemoryTotal));
        if (_snap.CommitKnown)
            y = Fact(g, x, y, inner, "Committed", FormatBytes(_snap.Committed) + " / " + FormatBytes(_snap.CommitLimit));
        if (_snap.CachedKnown)
            y = Fact(g, x, y, inner, "Cached", FormatBytes(_snap.Cached));
        if (_snap.PoolKnown)
        {
            y = Fact(g, x, y, inner, "Paged pool", FormatBytes(_snap.PagedPool));
            y = Fact(g, x, y, inner, "Nonpaged pool", FormatBytes(_snap.NonPagedPool));
        }
        return ModuleFacts(g, x, y, inner);
    }

    private int GpuSection(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "GPU", true, _snap.GpuTempKnown, _snap.GpuCelsius);
        if (!_snap.GpuKnown)
            return Fact(g, x, y, inner, "GPU", "Counters are not available");
        y = Fact(g, x, y, inner, "Usage", Percent(_snap.GpuPercent));
        y = Graph(g, x, y, inner, _snap.GpuHistory, Gpu, true);
        if (_snap.GpuMemoryKnown)
            y = Fact(g, x, y, inner, "Dedicated", FormatBytes(_snap.GpuMemoryUsed));
        if (_snap.GpuSharedKnown)
            y = Fact(g, x, y, inner, "Shared", FormatBytes(_snap.GpuShared));
        return y;
    }

    private int CpuDetail(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "CPU", false, _snap.CpuTempKnown, _snap.CpuCelsius);
        y = Graph(g, x, y, inner, _snap.CpuHistory, Cpu, false);
        y = Fact(g, x, y, inner, "Usage", _snap.CpuKnown ? Percent(_snap.CpuPercent) : "--");
        y = ProcessorSection(g, x, y, inner);
        if (_snap.ClockKnown)
            y = Fact(g, x, y, inner, "Clock", FormatGHz(_snap.ClockMHz));
        if (_snap.CoreMHz != null && _snap.CoreMHz.Length > 0)
        {
            y = Caption(g, x, y, "Cores");
            y = CoreGrid(g, x, y, inner, _snap.CoreMHz);
        }
        y = ProcessSection(g, x, y, inner, false);
        return AppList(g, x, y, inner, _snap.ByCpu, true, 6);
    }

    private int MemoryDetail(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "Memory", false, _snap.MemTempKnown, _snap.MemCelsius);
        y = Graph(g, x, y, inner, _snap.MemoryHistory, Mem, true);
        y = Fact(g, x, y, inner, "In use", FormatBytes(_snap.MemoryUsed) + " of " + FormatBytes(_snap.MemoryTotal));
        if (_snap.CommitKnown)
            y = Fact(g, x, y, inner, "Committed", FormatBytes(_snap.Committed) + " / " + FormatBytes(_snap.CommitLimit));
        if (_snap.CachedKnown)
            y = Fact(g, x, y, inner, "Cached", FormatBytes(_snap.Cached));
        if (_snap.PoolKnown)
        {
            y = Fact(g, x, y, inner, "Paged pool", FormatBytes(_snap.PagedPool));
            y = Fact(g, x, y, inner, "Nonpaged pool", FormatBytes(_snap.NonPagedPool));
        }
        if (_snap.SpeedKnown)
            y = Fact(g, x, y, inner, "Speed", _snap.SpeedMHz + " MHz");
        if (_snap.ModuleCount > 0)
            y = Fact(g, x, y, inner, "Installed", _snap.ModuleCount + " modules  " + FormatBytes(_snap.InstalledBytes));
        if (_snap.ReservedKnown)
            y = Fact(g, x, y, inner, "Reserved", FormatBytes(_snap.HardwareReserved));
        y = ProcessSection(g, x, y, inner, true);
        return AppList(g, x, y, inner, _snap.ByMemory, false, 0);
    }

    private int GpuDetail(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "GPU", false, _snap.GpuTempKnown, _snap.GpuCelsius);
        if (!_snap.GpuKnown)
            return Fact(g, x, y, inner, "GPU", "Counters are not available");
        y = Fact(g, x, y, inner, "Usage", Percent(_snap.GpuPercent));
        y = Graph(g, x, y, inner, _snap.GpuHistory, Gpu, true);
        if (!_snap.GpuMemoryKnown && !_snap.GpuSharedKnown)
            return y;
        y = SectionBreak(g, x, y, inner);
        if (g != null)
        {
            using (SolidBrush brush = new SolidBrush(TextMain))
                g.DrawString("Adapter", _fontLabel, brush, x, y);
        }
        y += _textH + Px(8);
        if (_snap.GpuMemoryKnown)
            y = Fact(g, x, y, inner, "Dedicated", FormatBytes(_snap.GpuMemoryUsed));
        if (_snap.GpuSharedKnown)
            y = Fact(g, x, y, inner, "Shared", FormatBytes(_snap.GpuShared));
        return y;
    }

    private int DiskSection(Graphics g, int x, int y, int inner)
    {
        y = Heading(g, x, y, inner, "Disk", true);
        if (!_snap.DiskKnown && !_snap.DiskSpaceKnown)
            return Fact(g, x, y, inner, "Disk", "Counters are not available");
        if (_snap.DiskKnown)
            y = Graph(g, x, y, inner, _snap.DiskHistory, TextMain, true);
        if (_snap.DiskSpaceKnown)
            y = Fact(g, x, y, inner, "In use", FormatBytes(_snap.DiskUsed) + " of " + FormatBytes(_snap.DiskTotal));
        if (_snap.DiskKnown)
        {
            y = Fact(g, x, y, inner, "Read", FormatRate(_snap.DiskReadMBps));
            y = Fact(g, x, y, inner, "Write", FormatRate(_snap.DiskWriteMBps));
        }
        return y;
    }

    private int DiskDetail(Graphics g, int x, int y, int inner)
    {
        if (!_snap.DiskKnown && !_snap.DiskSpaceKnown && (_snap.Drives == null || _snap.Drives.Length == 0))
            return Fact(g, x, y, inner, "Disk", "Counters are not available");
        if (_snap.DiskKnown)
            y = Graph(g, x, y, inner, _snap.DiskHistory, TextMain, true);
        if (_snap.DiskSpaceKnown)
            y = Fact(g, x, y, inner, "In use", FormatBytes(_snap.DiskUsed) + " of " + FormatBytes(_snap.DiskTotal));
        if (_snap.DiskKnown)
        {
            y = Fact(g, x, y, inner, "Read", FormatRate(_snap.DiskReadMBps));
            y = Fact(g, x, y, inner, "Write", FormatRate(_snap.DiskWriteMBps));
        }
        if (_snap.Drives == null)
            return y;
        for (int i = 0; i < _snap.Drives.Length; i++)
        {
            DiskDrive drive = _snap.Drives[i];
            string name = string.IsNullOrEmpty(drive.Name) ? "Disk" : drive.Name;
            string value = FormatBytes(drive.Bytes);
            if (!string.IsNullOrEmpty(drive.Media))
                value = value + "  " + drive.Media;
            y = Fact(g, x, y, inner, name, value);
        }
        return y;
    }

    private int Heading(Graphics g, int x, int y, int inner, string title, bool rule)
    {
        return Heading(g, x, y, inner, title, rule, false, 0);
    }

    private int Heading(Graphics g, int x, int y, int inner, string title, bool rule, bool tempKnown, int celsius)
    {
        if (rule)
            y = SectionBreak(g, x, y, inner);
        if (g != null)
        {
            int tempW = 0;
            if (tempKnown)
            {
                string tempText = celsius.ToString() + " \u00b0C";
                SizeF size = g.MeasureString(tempText, _fontValue);
                tempW = (int)Math.Ceiling(size.Width) + Px(4);
                Rectangle box = new Rectangle(x + inner - tempW, y, tempW, _textH);
                using (SolidBrush brush = new SolidBrush(TempInk(celsius)))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.Alignment = StringAlignment.Far;
                    fmt.LineAlignment = StringAlignment.Center;
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(tempText, _fontValue, brush, box, fmt);
                }
            }
            Rectangle titleBox = new Rectangle(x, y, Math.Max(Px(24), inner - tempW), _textH);
            using (SolidBrush brush = new SolidBrush(TextMain))
            using (StringFormat left = new StringFormat())
            {
                left.Alignment = StringAlignment.Near;
                left.LineAlignment = StringAlignment.Center;
                left.FormatFlags = StringFormatFlags.NoWrap;
                left.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(title, _fontLabel, brush, titleBox, left);
            }
        }
        return y + _textH + Px(8);
    }

    private static Color TempInk(int celsius)
    {
        if (celsius >= 85)
            return TempHot;
        if (celsius >= 70)
            return TempWarm;
        return TempCool;
    }

    private int ProcessorFacts(Graphics g, int x, int y, int inner)
    {
        bool named = !string.IsNullOrEmpty(_snap.CpuName);
        bool cores = _snap.CpuCores > 0 && _snap.CpuThreads > 0;
        bool rated = _snap.CpuMaxMHz > 0 && _snap.CpuL3Kb > 0;
        if (!named && !cores && !rated)
            return y;
        if (named)
        {
            if (g != null)
            {
                using (SolidBrush brush = new SolidBrush(TextMain))
                using (StringFormat fmt = CenterFormat())
                {
                    Rectangle box = new Rectangle(x, y, inner, _textH);
                    g.DrawString(_snap.CpuName, _fontValue, brush, box, fmt);
                }
            }
            y += _textH + Px(8);
        }
        int gap = Px(16);
        int half = (inner - gap) / 2;
        if (cores)
            y = PairRow(g, x, y, half, gap, "Cores", _snap.CpuCores.ToString(), "Threads", _snap.CpuThreads.ToString());
        if (rated)
            y = PairRow(g, x, y, half, gap, "Rated", FormatGHz(_snap.CpuMaxMHz), "L3", FormatCache(_snap.CpuL3Kb));
        return y;
    }

    private int ModuleFacts(Graphics g, int x, int y, int inner)
    {
        bool speed = _snap.SpeedKnown && _snap.SpeedMHz > 0;
        bool count = _snap.ModuleCount > 0;
        bool installed = _snap.InstalledBytes > 0;
        bool reserved = _snap.ReservedKnown;
        if (!speed && !count && !installed && !reserved)
            return y;
        int gap = Px(16);
        int half = (inner - gap) / 2;
        if (speed && count)
            y = PairRow(g, x, y, half, gap, "Speed", _snap.SpeedMHz + " MHz", "Modules", _snap.ModuleCount.ToString());
        else if (speed)
            y = Fact(g, x, y, inner, "Speed", _snap.SpeedMHz + " MHz");
        else if (count)
            y = Fact(g, x, y, inner, "Modules", _snap.ModuleCount.ToString());
        if (installed && reserved)
            y = PairRow(g, x, y, half, gap, "Installed", FormatBytes(_snap.InstalledBytes), "Reserved", FormatBytes(_snap.HardwareReserved));
        else if (installed)
            y = Fact(g, x, y, inner, "Installed", FormatBytes(_snap.InstalledBytes));
        else if (reserved)
            y = Fact(g, x, y, inner, "Reserved", FormatBytes(_snap.HardwareReserved));
        return y;
    }

    private int AppList(Graphics g, int x, int y, int inner, AppUse[] rows, bool cpu, int max)
    {
        int n = 0;
        if (rows != null)
        {
            n = rows.Length;
            if (max > 0 && n > max)
                n = max;
        }
        if (n == 0)
            return Fact(g, x, y, inner, "Apps", "None yet");
        float peak = 1f;
        for (int i = 0; i < n; i++)
        {
            float v = cpu ? rows[i].CpuPercent : rows[i].MemoryBytes;
            if (v > peak) peak = v;
        }
        for (int i = 0; i < n; i++)
        {
            string label = rows[i].Name;
            if (rows[i].Count > 1)
                label = label + " \u00d7" + rows[i].Count;
            string value = cpu ? Percent(rows[i].CpuPercent) : FormatBytes(rows[i].MemoryBytes);
            float frac = cpu ? rows[i].CpuPercent / peak : rows[i].MemoryBytes / peak;
            y = AppRow(g, x, y, inner, label, value, frac, BarColor(rows[i].Name));
        }
        return y;
    }

    private int AppRow(Graphics g, int x, int y, int inner, string name, string value, float frac, Color color)
    {
        if (frac < 0f) frac = 0f;
        if (frac > 1f) frac = 1f;
        int barY = y + _textH + Px(4);
        int barH = Px(4);
        if (g != null)
        {
            TextPair(g, x, y, inner, name, value, true);
            FillRound(g, new Rectangle(x, barY, inner, barH), Px(2), Track);
            int fill = (int)(inner * frac);
            if (fill > 0)
                FillRound(g, new Rectangle(x, barY, Math.Max(fill, Px(3)), barH), Px(2), color);
        }
        return barY + barH + Px(10);
    }

    private int ProcessSection(Graphics g, int x, int y, int inner, bool trim)
    {
        y = SectionBreak(g, x, y, inner);
        int rowH = Px(28);
        if (trim)
        {
            _trimRect = new Rectangle(x + inner - Px(72), y, Px(72), rowH);
            if (g != null)
                DrawChip(g, _trimRect, "Trim", false, Mem, _trimHover);
        }
        if (g != null)
        {
            using (SolidBrush brush = new SolidBrush(TextMain))
                g.DrawString("Current processes", _fontLabel, brush, x, y + (rowH - _textH) / 2);
        }
        return y + rowH + Px(8);
    }

    private int SectionBreak(Graphics g, int x, int y, int inner)
    {
        y = PxGap(y, 6);
        if (g != null)
        {
            using (Pen hair = new Pen(Line, 1f))
                g.DrawLine(hair, x, y, x + inner, y);
        }
        return PxGap(y, 12);
    }

    private int Fact(Graphics g, int x, int y, int inner, string label, string value)
    {
        if (g != null)
            TextPair(g, x, y, inner, label, value, false);
        return y + _textH + Px(8);
    }

    private int ProcessorSection(Graphics g, int x, int y, int inner)
    {
        bool named = !string.IsNullOrEmpty(_snap.CpuName);
        bool cores = _snap.CpuCores > 0 || _snap.CpuThreads > 0;
        bool rated = _snap.CpuMaxMHz > 0 || _snap.CpuL3Kb > 0;
        if (!named && !cores && !rated)
            return y;
        if (named)
        {
            if (g != null)
            {
                using (SolidBrush brush = new SolidBrush(TextMain))
                using (StringFormat fmt = CenterFormat())
                {
                    Rectangle box = new Rectangle(x, y, inner, _textH);
                    g.DrawString(_snap.CpuName, _fontValue, brush, box, fmt);
                }
            }
            y += _textH + Px(8);
        }
        int gap = Px(16);
        int half = (inner - gap) / 2;
        if (cores)
        {
            string coreText = _snap.CpuCores > 0 ? _snap.CpuCores.ToString() : "--";
            string threadText = _snap.CpuThreads > 0 ? _snap.CpuThreads.ToString() : "--";
            y = PairRow(g, x, y, half, gap, "Cores", coreText, "Threads", threadText);
        }
        if (rated)
        {
            string clockText = _snap.CpuMaxMHz > 0 ? FormatGHz(_snap.CpuMaxMHz) : "--";
            string cacheText = _snap.CpuL3Kb > 0 ? FormatCache(_snap.CpuL3Kb) : "--";
            y = PairRow(g, x, y, half, gap, "Rated", clockText, "L3", cacheText);
        }
        return y;
    }

    private int PairRow(Graphics g, int x, int y, int half, int gap, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        if (g != null)
        {
            TextPair(g, x, y, half, leftLabel, leftValue, false);
            TextPair(g, x + half + gap, y, half, rightLabel, rightValue, false);
        }
        return y + _textH + Px(6);
    }

    private static string FormatCache(int kb)
    {
        if (kb >= 1024 && kb % 1024 == 0)
            return (kb / 1024).ToString() + " MB";
        if (kb >= 1024)
            return (kb / 1024f).ToString("0.0") + " MB";
        return kb.ToString() + " KB";
    }

    private int Caption(Graphics g, int x, int y, string text)
    {
        if (g != null)
        {
            using (SolidBrush brush = new SolidBrush(TextMuted))
                g.DrawString(text, _fontLabel, brush, x, y);
        }
        return y + _textH + Px(6);
    }

    private int Graph(Graphics g, int x, int y, int inner, float[] values, Color color, bool rangeLabel)
    {
        int h = Px(78);
        Rectangle bounds = new Rectangle(x, y, inner, h);
        if (g != null)
        {
            FillRound(g, bounds, Px(8), Track);
            if (values != null && values.Length >= 2)
            {
                float max = 100f;
                PointF[] pts = new PointF[values.Length];
                float step = (bounds.Width - Px(16)) / (float)(values.Length - 1);
                float lo = values[0];
                float hi = values[0];
                for (int i = 0; i < values.Length; i++)
                {
                    float v = values[i];
                    if (v < 0f) v = 0f;
                    if (v > max) v = max;
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                    float py = bounds.Bottom - Px(8) - (bounds.Height - Px(20)) * (v / max);
                    pts[i] = new PointF(bounds.X + Px(8) + step * i, py);
                }
                int mid = bounds.Top + (bounds.Height / 2);
                using (Pen grid = new Pen(Color.FromArgb(80, Line), 1f))
                    g.DrawLine(grid, bounds.Left + Px(8), mid, bounds.Right - Px(8), mid);
                PointF[] area = new PointF[pts.Length + 2];
                for (int i = 0; i < pts.Length; i++)
                    area[i] = pts[i];
                area[pts.Length] = new PointF(pts[pts.Length - 1].X, bounds.Bottom - Px(8));
                area[pts.Length + 1] = new PointF(pts[0].X, bounds.Bottom - Px(8));
                using (SolidBrush wash = new SolidBrush(Color.FromArgb(56, color)))
                    g.FillPolygon(wash, area);
                using (Pen pen = new Pen(color, 1.6f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, pts);
                }
                if (rangeLabel)
                {
                    string span = ((int)Math.Round(lo)).ToString() + "-" + ((int)Math.Round(hi)).ToString() + "%";
                    SizeF size = g.MeasureString(span, _fontLabel);
                    using (SolidBrush brush = new SolidBrush(TextMuted))
                        g.DrawString(span, _fontLabel, brush, bounds.Right - Px(8) - size.Width, bounds.Top + Px(4));
                }
            }
        }
        return y + h + Px(12);
    }

    private int CoreGrid(Graphics g, int x, int y, int inner, int[] mhz)
    {
        int cols = 6;
        int rows = (mhz.Length + cols - 1) / cols;
        int cellH = Px(28);
        int cellW = inner / cols;
        if (g != null)
        {
            for (int i = 0; i < mhz.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Rectangle cell = new Rectangle(x + col * cellW, y + row * cellH, cellW, cellH);
                using (SolidBrush brush = new SolidBrush(TextMain))
                using (StringFormat fmt = CenterFormat())
                {
                    g.DrawString((mhz[i] / 1000f).ToString("0.00"), _fontLabel, brush, cell, fmt);
                }
            }
        }
        return y + rows * cellH + Px(8);
    }

    private static void DrawPlug(Graphics g, Rectangle r, Color color)
    {
        using (Pen pen = new Pen(color, 1.6f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int left = r.Left + r.Width / 4;
            int right = r.Right - r.Width / 4;
            int prongBot = r.Top + r.Height / 3;
            g.DrawLine(pen, left, r.Top + 1, left, prongBot);
            g.DrawLine(pen, right, r.Top + 1, right, prongBot);
            int bodyBot = r.Top + (r.Height * 2) / 3;
            g.DrawRectangle(pen, left - 1, prongBot, (right - left) + 2, bodyBot - prongBot);
            g.DrawLine(pen, r.Left + r.Width / 2, bodyBot, r.Left + r.Width / 2, r.Bottom - 1);
        }
    }

    private static void DrawCell(Graphics g, Rectangle r, Color color)
    {
        int nub = Math.Max(2, r.Width / 8);
        Rectangle body = new Rectangle(r.Left, r.Top + 2, r.Width - nub - 1, r.Height - 4);
        using (Pen pen = new Pen(color, 1.6f))
            g.DrawRectangle(pen, body);
        int nubH = Math.Max(2, r.Height / 3);
        using (SolidBrush brush = new SolidBrush(color))
            g.FillRectangle(brush, body.Right, r.Top + (r.Height - nubH) / 2, nub, nubH);
    }

    private static void DrawBolt(Graphics g, Rectangle r, Color color)
    {
        Point[] pts = new Point[]
        {
            new Point(r.Left + r.Width * 3 / 5, r.Top + 1),
            new Point(r.Left + r.Width / 5, r.Top + r.Height / 2),
            new Point(r.Left + r.Width / 2, r.Top + r.Height / 2),
            new Point(r.Left + r.Width * 2 / 5, r.Bottom - 1),
            new Point(r.Left + r.Width * 4 / 5, r.Top + r.Height / 2),
            new Point(r.Left + r.Width * 11 / 20, r.Top + r.Height / 2)
        };
        using (SolidBrush brush = new SolidBrush(color))
            g.FillPolygon(brush, pts);
    }

    private void TextPair(Graphics g, int x, int y, int inner, string label, string value, bool hugValue)
    {
        using (StringFormat measure = new StringFormat(StringFormat.GenericTypographic))
        {
            measure.FormatFlags = StringFormatFlags.NoWrap;
            int valueW;
            if (hugValue)
            {
                SizeF measured = g.MeasureString(value, _fontValue, 1000, measure);
                valueW = (int)Math.Ceiling(measured.Width) + Px(4);
                int maxW = inner - Px(24);
                if (valueW > maxW)
                    valueW = maxW;
            }
            else
            {
                valueW = Px(220);
                int nameMin = Px(72);
                if (valueW > inner - nameMin)
                    valueW = inner - nameMin;
                if (valueW < Px(48))
                    valueW = Px(48);
            }
            int gap = Px(6);
            int nameW = inner - valueW - gap;
            if (nameW < Px(24))
                nameW = Px(24);
            Rectangle nameRect = new Rectangle(x, y, nameW, _textH);
            Rectangle valueRect = new Rectangle(x + inner - valueW, y, valueW, _textH);
            SizeF nameSize = g.MeasureString(label, _fontLabel, 4000, measure);
            bool clip = nameSize.Width > nameW;
            using (SolidBrush muted = new SolidBrush(TextMuted))
            using (SolidBrush main = new SolidBrush(TextMain))
            using (StringFormat left = new StringFormat())
            using (StringFormat right = new StringFormat())
            {
                left.Trimming = clip ? StringTrimming.EllipsisCharacter : StringTrimming.None;
                left.FormatFlags = StringFormatFlags.NoWrap;
                left.LineAlignment = StringAlignment.Center;
                right.Alignment = StringAlignment.Far;
                right.Trimming = StringTrimming.None;
                right.FormatFlags = StringFormatFlags.NoWrap;
                right.LineAlignment = StringAlignment.Center;
                g.DrawString(label, _fontLabel, muted, nameRect, left);
                g.DrawString(value, _fontValue, main, valueRect, right);
            }
        }
    }

    private void DrawChip(Graphics g, Rectangle rect, string text, bool on, Color accent)
    {
        DrawChip(g, rect, text, on, accent, false);
    }

    private void DrawChip(Graphics g, Rectangle rect, string text, bool on, Color accent, bool hover)
    {
        Color back = on || hover ? Color.FromArgb(255, 54, 58, 66) : Track;
        FillRound(g, rect, Px(6), back);
        if (on)
        {
            using (Pen pen = new Pen(accent, 1.5f))
            using (GraphicsPath path = Round(rect, Px(6)))
                g.DrawPath(pen, path);
        }
        using (SolidBrush brush = new SolidBrush(on || hover ? TextMain : TextMuted))
        using (StringFormat fmt = CenterFormat())
        {
            g.DrawString(text, _fontLabel, brush, rect, fmt);
        }
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

    private int PxGap(int y, int dip)
    {
        return y + Px(dip);
    }

    private static string Percent(float value)
    {
        return ((int)Math.Round(value)).ToString() + "%";
    }

    private static string FormatRate(float mbps)
    {
        if (mbps < 0f)
            mbps = 0f;
        return mbps.ToString("0.00") + " MB/s";
    }

    private static string FormatGHz(int mhz)
    {
        return (mhz / 1000f).ToString("0.00") + " GHz";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double gb = bytes / 1024d / 1024d / 1024d;
        if (gb >= 1d)
            return gb.ToString("0.00") + " GB";
        double mb = bytes / 1024d / 1024d;
        return mb.ToString("0") + " MB";
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
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            return;
        }
        Point p = e.Location;
        if (_barRect.Contains(p))
            return;
        Point body = p.Y > _headH ? new Point(p.X, p.Y + _scroll) : p;
        if (_chevronHit.Contains(p))
        {
            ToggleQuickPopup();
            return;
        }
        if (_wifiHit.Contains(p))
        {
            if (!SystemActions.OpenSettings("ms-settings:network-wifi"))
                SystemActions.OpenSettings("ms-settings:network");
            return;
        }
        if (_batteryHit.Contains(p))
        {
            if (!SystemActions.OpenSettings("ms-settings:powersleep"))
                SystemActions.OpenSettings("ms-settings:batterysaver");
            return;
        }
        if (_cpuChip.Contains(p)) { CloseQuickPopup(); SetDetail(Toggle(DetailMode.Cpu)); return; }
        if (_memChip.Contains(p)) { CloseQuickPopup(); SetDetail(Toggle(DetailMode.Memory)); return; }
        if (_gpuChip.Contains(p)) { CloseQuickPopup(); SetDetail(Toggle(DetailMode.Gpu)); return; }
        if (_diskChip.Contains(p)) { CloseQuickPopup(); SetDetail(Toggle(DetailMode.Disk)); return; }
        if (_detail == DetailMode.Memory && _trimRect.Contains(body))
        {
            TrimBackground();
            return;
        }
    }

    private void SetDetail(DetailMode next)
    {
        if (next != _detail)
        {
            _scroll = 0;
            _scrollPos = 0f;
            _scrollTarget = 0;
        }
        _detail = next;
        ApplySize();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        if (_gutter <= 0)
            return;
        if (_thumbRect.Contains(e.Location))
        {
            _dragging = true;
            _dragY = e.Y;
            _dragScroll = _scroll;
            Capture = true;
            return;
        }
        if (!_barRect.Contains(e.Location))
            return;
        int page = Math.Max(Px(48), Height - _headH - Px(24));
        if (e.Y < _thumbRect.Top)
            ScrollBy(-page);
        else
            ScrollBy(page);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point p = e.Location;
        Point body = p.Y > _headH ? new Point(p.X, p.Y + _scroll) : p;
        if (_dragging)
        {
            int travel = _barRect.Height - _thumbRect.Height;
            int max = MaxScroll(Height);
            if (travel < 1 || max < 1)
                return;
            int delta = e.Y - _dragY;
            _scroll = _dragScroll + (int)(delta * (max / (float)travel));
            _scrollTarget = _scroll;
            _scrollPos = _scroll;
            ClampScroll(Height);
            Invalidate();
            return;
        }
        UpdateHover(p, body);
    }

    private void UpdateHover(Point p, Point body)
    {
        QuickHit prev = _hover;
        bool prevCpu = _chipHoverCpu;
        bool prevMem = _chipHoverMem;
        bool prevGpu = _chipHoverGpu;
        bool prevDisk = _chipHoverDisk;
        bool prevTrim = _trimHover;

        _chipHoverCpu = _cpuChip.Contains(p);
        _chipHoverMem = _memChip.Contains(p);
        _chipHoverGpu = _gpuChip.Contains(p);
        _chipHoverDisk = _diskChip.Contains(p);
        _trimHover = _detail == DetailMode.Memory && _trimRect.Contains(body);
        _hover = QuickHit.None;
        bool prevWifi = _wifiHot;
        bool prevChevron = _chevronHot;
        bool prevBatt = _batteryHot;
        _wifiHot = _wifiHit.Contains(p);
        _chevronHot = _chevronHit.Contains(p);
        _batteryHot = _batteryHit.Contains(p);

        bool hand = _wifiHot || _chevronHot || _batteryHot || _chipHoverCpu || _chipHoverMem || _chipHoverGpu || _chipHoverDisk || _trimHover;
        Cursor = hand ? Cursors.Hand : Cursors.Default;
        UpdateTooltip(p);

        if (prev != _hover || prevCpu != _chipHoverCpu || prevMem != _chipHoverMem || prevGpu != _chipHoverGpu
            || prevDisk != _chipHoverDisk || prevTrim != _trimHover || prevWifi != _wifiHot || prevChevron != _chevronHot
            || prevBatt != _batteryHot)
            Invalidate();
    }

    private void UpdateTooltip(Point p)
    {
        string tip = "";
        if (_chevronHit.Contains(p)) tip = QuickPopupOpen() ? "Close settings" : "Quick settings";
        else if (_wifiHit.Contains(p)) tip = "Network settings";
        else if (_batteryHit.Contains(p)) tip = "Power options";
        if (tip == _lastTip)
            return;
        _lastTip = tip;
        if (string.IsNullOrEmpty(tip))
            _tip.Hide(this);
        else
            _tip.Show(tip, this, p.X + 12, p.Y + 16, 2000);
    }

    internal void ScrollBy(int delta)
    {
        _scrollTarget += delta;
        int max = MaxScroll(Height);
        if (_scrollTarget < 0) _scrollTarget = 0;
        if (_scrollTarget > max) _scrollTarget = max;
        Invalidate();
    }

    internal void ScrollByWheel(int wheelDelta)
    {
        // Five text lines per standard 120-notch; scales with DPI fit via _textH / Px.
        int line = Math.Max(Px(18), _textH + Px(2));
        int step = (int)Math.Round((wheelDelta / 120f) * line * 5f);
        if (step == 0)
            step = wheelDelta > 0 ? line : -line;
        ScrollBy(-step);
    }

    private int MaxScroll(int height)
    {
        int max = _docH - height;
        if (max < 0)
            return 0;
        return max;
    }

    private void DrawBar(Graphics g)
    {
        int x = Width - Px(10);
        int y = _headH + Px(4);
        int h = Height - y - Px(8);
        if (h < Px(20))
            return;
        _barRect = new Rectangle(x, y, Px(6), h);
        FillRound(g, _barRect, Px(3), Track);
        int max = MaxScroll(Height);
        int view = Height - _headH;
        int thumb = view;
        if (_docH > 0)
            thumb = (int)(h * (view / (float)_docH));
        if (thumb < Px(24))
            thumb = Px(24);
        if (thumb > h)
            thumb = h;
        int span = h - thumb;
        int top = y;
        float scrollPos = _scroll;
        if (max > 0 && span > 0)
            top = y + (int)(span * (scrollPos / (float)max));
        _thumbRect = new Rectangle(x, top, Px(6), thumb);
        bool thumbHover = _thumbRect.Contains(PointToClient(Control.MousePosition));
        FillRound(g, _thumbRect, Px(3), thumbHover ? TextMain : Line);
    }

    private static bool Named(string name, string expected)
    {
        return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static Color BarColor(string name)
    {
        if (IsInternal(name))
            return InternalInk;
        if (IsPlatform(name))
            return PlatformInk;
        return InstalledInk;
    }

    private static bool IsInternal(string name)
    {
        return Named(name, "System")
            || Named(name, "Memory Compression")
            || Named(name, "MemCompression")
            || Named(name, "Registry")
            || Named(name, "Secure System")
            || Named(name, "svchost")
            || Named(name, "csrss")
            || Named(name, "smss")
            || Named(name, "wininit")
            || Named(name, "winlogon")
            || Named(name, "lsass")
            || Named(name, "services")
            || Named(name, "dwm")
            || Named(name, "fontdrvhost")
            || Named(name, "sihost")
            || Named(name, "ctfmon")
            || Named(name, "Idle")
            || Named(name, "RuntimeBroker")
            || Named(name, "taskhostw")
            || Named(name, "dllhost")
            || Named(name, "spoolsv")
            || Named(name, "WmiPrvSE")
            || Named(name, "MsMpEng")
            || Named(name, "conhost")
            || Named(name, "audiodg");
    }

    private static bool IsPlatform(string name)
    {
        return Named(name, "explorer")
            || Named(name, "msedge")
            || Named(name, "msedgewebview2")
            || Named(name, "Teams")
            || Named(name, "ms-teams")
            || Named(name, "OneDrive")
            || Named(name, "outlook")
            || Named(name, "winword")
            || Named(name, "excel")
            || Named(name, "powerpnt")
            || Named(name, "onenote")
            || Named(name, "CompanyPortal")
            || Named(name, "WinStore.App")
            || Named(name, "SearchHost")
            || Named(name, "ShellExperienceHost")
            || Named(name, "StartMenuExperienceHost")
            || Named(name, "TextInputHost")
            || Named(name, "ApplicationFrameHost")
            || Named(name, "SystemSettings");
    }

    private static bool IsProtected(string name)
    {
        return string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Memory Compression", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "MemCompression", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Registry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Secure System", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "svchost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "csrss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "smss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "wininit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "winlogon", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "lsass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "services", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "dwm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "fontdrvhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "QuickView", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "WorkAppsDock", StringComparison.OrdinalIgnoreCase);
    }

    private void TrimBackground()
    {
        HashSet<int> visible = new HashSet<int>();
        EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
        {
            if (IsWindowVisible(hwnd))
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid != 0)
                    visible.Add((int)pid);
            }
            return true;
        }, IntPtr.Zero);

        HashSet<string> blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Process[] all = null;
        try
        {
            all = Process.GetProcesses();
        }
        catch
        {
            return;
        }
        for (int i = 0; i < all.Length; i++)
        {
            try
            {
                if (visible.Contains(all[i].Id) || IsProtected(all[i].ProcessName))
                    blocked.Add(all[i].ProcessName);
            }
            catch
            {
            }
        }
        for (int i = 0; i < all.Length; i++)
        {
            Process proc = all[i];
            try
            {
                string name = proc.ProcessName;
                if (IsProtected(name) || blocked.Contains(name))
                    continue;
                IntPtr handle = OpenProcess(0x0500, false, (uint)proc.Id);
                if (handle == IntPtr.Zero)
                    continue;
                try
                {
                    EmptyWorkingSet(handle);
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
            catch
            {
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private DetailMode Toggle(DetailMode mode)
    {
        if (_detail == mode)
            return DetailMode.None;
        return mode;
    }

    private sealed class WheelFilter : IMessageFilter
    {
        private readonly PanelForm _form;

        public WheelFilter(PanelForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            try
            {
                if (m.Msg != 0x020A || !_form.Visible || !_form._shown)
                    return false;
                if (!_form.Bounds.Contains(Cursor.Position))
                    return false;
                if (_form.MaxScroll(_form.Height) < 1)
                    return false;
                long w = m.WParam.ToInt64();
                int delta = (short)((w >> 16) & 0xFFFF);
                _form.ScrollByWheel(delta);
                return true;
            }
            catch (Exception ex)
            {
                Program.LogCrash(ex);
                return false;
            }
        }
    }
}
