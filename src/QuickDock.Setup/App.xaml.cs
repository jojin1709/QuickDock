using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace QuickDock.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string[] args = e.Args;
        bool isUninstall = args.Any(a => string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a, "-uninstall", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));

        if (isUninstall)
        {
            HandleUninstall(args);
            return;
        }

        // Normal installation / setup wizard flow
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void HandleUninstall(string[] args)
    {
        bool isWorker = args.Any(a => string.Equals(a, "/worker", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(a, "-worker", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(a, "/uninstall-worker", StringComparison.OrdinalIgnoreCase));

        string defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "QuickDock");

        string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        if (!isWorker)
        {
            // Running from inside install directory or elsewhere.
            // Copy ourselves to %TEMP% to allow deleting the install directory completely.
            try
            {
                string tempDir = Path.GetTempPath();
                string tempExe = Path.Combine(tempDir, "QuickDock-Uninstall.exe");

                if (File.Exists(currentExe))
                {
                    File.Copy(currentExe, tempExe, true);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = $"/uninstall /worker \"{defaultInstallDir}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to launch uninstaller worker: " + ex);
            }

            Environment.Exit(0);
            return;
        }

        // Worker process executing from %TEMP%
        try
        {
            // 1. Terminate all QuickDock processes
            KillQuickDockProcesses();

            // 2. Remove Shortcuts
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "QuickDock.lnk");
            if (File.Exists(desktopShortcut))
            {
                try { File.Delete(desktopShortcut); } catch { }
            }

            string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "QuickDock.lnk");
            if (File.Exists(startMenuShortcut))
            {
                try { File.Delete(startMenuShortcut); } catch { }
            }

            // 3. Remove Windows Startup entry
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("QuickDock", false);
            }
            catch { }

            // 4. Remove Windows Installed Apps Uninstall Registry entry
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock", false);
            }
            catch { }

            // 5. Delete application installation files
            string installDir = args.FirstOrDefault(a => !a.StartsWith('/') && !a.StartsWith('-') && a.Contains("QuickDock")) ?? defaultInstallDir;
            if (Directory.Exists(installDir))
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        Directory.Delete(installDir, true);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            // 6. Schedule self deletion from %TEMP%
            try
            {
                if (!string.IsNullOrEmpty(currentExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c timeout /t 2 >nul & del \"{currentExe}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error during uninstall execution: " + ex);
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    private static void KillQuickDockProcesses()
    {
        int currentPid = Process.GetCurrentProcess().Id;
        string[] targetNames = ["QuickDock.App", "QuickDock", "QuickDock-Setup"];

        foreach (var name in targetNames)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                if (proc.Id == currentPid) continue;
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch { }
            }
        }
    }
}
