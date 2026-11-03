using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class SystemActions
{
    public static string LastError { get; private set; }

    // Dry-path verification for QA (S7/S8): last shutdown argv without executing when dryRun=true.
    public static string LastPowerArgs { get; private set; }

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

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
