using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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
                if (n < 100) n = 100;
                if (n > 225) n = 225;
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
        if (percent < 100) percent = 100;
        if (percent > 225) percent = 225;
        percent = (percent / 25) * 25;
        if (percent < 100) percent = 100;
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
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet",
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

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, IntPtr lpdwResult);
}
