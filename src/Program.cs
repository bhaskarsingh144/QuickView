using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private const string ShortcutName = "QuickView.lnk";

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    private static void Main()
    {
        try
        {
            // Per-monitor V2: draw at native pixels so 4K text stays sharp.
            SetProcessDpiAwarenessContext(new IntPtr(-4));
        }
        catch
        {
        }
        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\QuickView", out created))
        {
            if (!created)
                return;
            EnsureStartup();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PanelForm());
        }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        LogCrash(e.Exception);
    }

    private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        LogCrash(e.ExceptionObject as Exception);
    }

    internal static void LogCrash(Exception ex)
    {
        try
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(dir))
                return;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + (ex == null ? "unknown" : ex.ToString()) + Environment.NewLine;
            File.AppendAllText(Path.Combine(dir, "quickview.log"), line);
        }
        catch
        {
        }
    }

    private static void EnsureStartup()
    {
        try
        {
            string exe = Application.ExecutablePath;
            if (string.IsNullOrEmpty(exe))
                return;
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcut = Path.Combine(startupDir, ShortcutName);
            string prefDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickView");
            string pref = Path.Combine(prefDir, "startup.txt");
            string choice = ReadChoice(pref);
            bool exists = File.Exists(shortcut);
            if (choice == "off")
                return;
            if (choice == "on")
            {
                if (exists)
                    return;
                Directory.CreateDirectory(prefDir);
                File.WriteAllText(pref, "off");
                return;
            }
            if (!exists)
                CreateShortcut(shortcut, exe);
            if (File.Exists(shortcut))
            {
                Directory.CreateDirectory(prefDir);
                File.WriteAllText(pref, "on");
            }
        }
        catch
        {
        }
    }

    private static string ReadChoice(string pref)
    {
        try
        {
            if (!File.Exists(pref))
                return "";
            string text = File.ReadAllText(pref).Trim().ToLowerInvariant();
            if (text == "on" || text == "off")
                return text;
        }
        catch
        {
        }
        return "";
    }

    private static void CreateShortcut(string lnk, string exe)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            return;
        object shell = Activator.CreateInstance(shellType);
        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
        Type shortcutType = shortcut.GetType();
        string dir = Path.GetDirectoryName(exe);
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { dir ?? "" });
        shortcutType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "QuickView" });
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }
}
