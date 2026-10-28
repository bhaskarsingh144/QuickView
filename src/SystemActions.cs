using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class SystemActions
{
    public static string LastError { get; private set; }

    // Dry-path verification for QA (S7/S8): last shutdown argv without executing when dryRun=true.
    public static string LastPowerArgs { get; private set; }

    public static int GetTextScalePercent()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility", false))
            {
                if (key == null)
                    return 100;
                object v = key.GetValue("TextScaleFactor");
                if (v == null)
                    return 100;
                int n = Convert.ToInt32(v);
                if (n < TextScale.MinPercent) n = TextScale.MinPercent;
                if (n > TextScale.MaxPercent) n = TextScale.MaxPercent;
                return n;
            }
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            return 100;
        }
    }

    public static bool SetTextScalePercent(int percent)
    {
        LastError = "";
        percent = TextScale.ClampAndSnap(percent);
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility"))
            {
                if (key == null)
                {
                    LastError = "Text size unavailable";
                    return false;
                }
                key.SetValue("TextScaleFactor", percent, RegistryValueKind.DWord);
            }
            // Same path Settings uses on Accessibility > Text size > Apply:
            // write TextScaleFactor, then publish WNF_EOA_UISETTINGS_CHANGED so the
            // shell shows "Please wait" and redraws apps (browser, etc.).
            PublishUiSettingsChanged();
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "TextScaleFactor",
                SMTO_ABORTIFHUNG, 1000, IntPtr.Zero);
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Text size failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    private static void PublishUiSettingsChanged()
    {
        try
        {
            // ntdll!RtlPublishWnfStateData(WNF_EOA_UISETTINGS_CHANGED, ...)
            IntPtr ntdll = GetModuleHandle("ntdll.dll");
            if (ntdll == IntPtr.Zero)
                return;
            IntPtr proc = GetProcAddress(ntdll, "RtlPublishWnfStateData");
            if (proc == IntPtr.Zero)
                return;
            RtlPublishWnfStateDataFn fn = (RtlPublishWnfStateDataFn)Marshal.GetDelegateForFunctionPointer(
                proc, typeof(RtlPublishWnfStateDataFn));
            fn(WNF_EOA_UISETTINGS_CHANGED, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
    }

    public static bool RestartExplorer()
    {
        LastError = "";
        try
        {
            Process[] list = Process.GetProcessesByName("explorer");
            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    list[i].Kill();
                    list[i].WaitForExit(4000);
                }
                catch
                {
                }
                finally
                {
                    list[i].Dispose();
                }
            }
            Thread.Sleep(400);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Explorer restart failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    /// <summary>
    /// Relaunch this exe after a short delay so the single-instance mutex can release.
    /// </summary>
    public static bool RestartQuickView()
    {
        LastError = "";
        try
        {
            string exe = Application.ExecutablePath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                LastError = "QuickView path missing";
                return false;
            }
            string dir = Path.GetDirectoryName(exe) ?? "";
            string comspec = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrEmpty(comspec))
                comspec = "cmd.exe";
            // Delay so this process exits and drops Local\QuickView before the new start.
            Process.Start(new ProcessStartInfo
            {
                FileName = comspec,
                Arguments = "/c timeout /t 1 /nobreak >nul & start \"\" \"" + exe + "\"",
                WorkingDirectory = dir,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });
            Application.Exit();
            return true;
        }
        catch (Exception ex)
        {
            LastError = "QuickView restart failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public static bool LockWorkstation()
    {
        LastError = "";
        try
        {
            if (!LockWorkStation())
            {
                LastError = "Lock failed";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Lock failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public static bool Sleep()
    {
        LastError = "";
        try
        {
            return SetSuspendState(false, true, true);
        }
        catch (Exception ex)
        {
            LastError = "Sleep failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public static bool RestartPc(bool dryRun)
    {
        return RunShutdown("/r /t 0", dryRun);
    }

    public static bool ShutdownPc(bool dryRun)
    {
        return RunShutdown("/s /t 0", dryRun);
    }

    private static bool RunShutdown(string args, bool dryRun)
    {
        LastError = "";
        LastPowerArgs = "shutdown.exe " + args;
        if (dryRun)
            return true;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Power action failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public static bool OpenSettings(string uri)
    {
        LastError = "";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Settings failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;
    // Ease of Access / Accessibility UI settings changed (undocumented WNF name).
    private const ulong WNF_EOA_UISETTINGS_CHANGED = 0x41870128a3bc1875UL;

    private delegate int RtlPublishWnfStateDataFn(
        ulong stateName, IntPtr typeId, IntPtr buffer, uint length, IntPtr explicitScope);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, IntPtr lpdwResult);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
}
