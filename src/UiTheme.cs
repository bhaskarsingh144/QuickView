using System.Drawing;

internal static class UiTheme
{
    public static readonly Color Bg = Color.FromArgb(255, 0x1E, 0x20, 0x24);
    public static readonly Color Surface = Color.FromArgb(255, 0x29, 0x2C, 0x32);
    public static readonly Color Raised = Color.FromArgb(255, 0x30, 0x34, 0x3B);
    public static readonly Color Border = Color.FromArgb(255, 0x3B, 0x40, 0x49);
    public static readonly Color Text = Color.FromArgb(255, 0xE1, 0xE5, 0xEC);
    public static readonly Color Muted = Color.FromArgb(255, 0x96, 0x9E, 0xAA);
    public static readonly Color Accent = Color.FromArgb(255, 0x6E, 0xA8, 0xFE);
    public static readonly Color Down = Color.FromArgb(255, 0x78, 0xB4, 0xFF);
    public static readonly Color Up = Color.FromArgb(255, 0xB6, 0x9C, 0xFF);
    public static readonly Color Hover = Color.FromArgb(255, 0x38, 0x3D, 0x46);

    public static readonly Color Cpu = Color.FromArgb(255, 91, 156, 245);
    public static readonly Color Mem = Color.FromArgb(255, 142, 124, 196);
    public static readonly Color Gpu = Color.FromArgb(255, 88, 196, 140);
    public static readonly Color InternalInk = Color.FromArgb(255, 168, 172, 180);
    public static readonly Color PlatformInk = Color.FromArgb(255, 72, 150, 255);
    public static readonly Color InstalledInk = Color.FromArgb(255, 240, 164, 48);
    public static readonly Color TempCool = Color.FromArgb(255, 88, 196, 140);
    public static readonly Color TempWarm = Color.FromArgb(255, 230, 186, 64);
    public static readonly Color TempHot = Color.FromArgb(255, 220, 72, 72);
}
