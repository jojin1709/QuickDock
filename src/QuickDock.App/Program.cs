using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickDock.Infrastructure.Windows;

namespace QuickDock.App;

public static class Program
{
    private static Mutex? _instanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickDock");
        Directory.CreateDirectory(dir);
        string mutexName = $"Global\\QuickDock_SingleInstance_{Environment.UserName}";
        bool isInitialInstance;
        try
        {
            _instanceMutex = new Mutex(true, mutexName, out isInitialInstance);
        }
        catch (AbandonedMutexException)
        {
            isInitialInstance = true;
        }

        if (!isInitialInstance)
        {
            // Verify if there really is an active QuickDock process
            var currentProc = System.Diagnostics.Process.GetCurrentProcess();
            var existingProcs = System.Diagnostics.Process.GetProcessesByName("QuickDock.App")
                .Where(p => p.Id != currentProc.Id)
                .ToList();

            if (existingProcs.Count > 0)
            {
                // Another instance is genuinely running; signal it to activate and bring to front
                uint activateMsg = NativeMethods.RegisterWindowMessageW("QUICKDOCK_ACTIVATE_INSTANCE");
                if (activateMsg != 0)
                {
                    NativeMethods.PostMessageW(NativeMethods.HWND_BROADCAST, activateMsg, IntPtr.Zero, IntPtr.Zero);
                }
                _instanceMutex?.Dispose();
                _instanceMutex = null;
                return;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickDock");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "firstchance.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] (HR: 0x{e.Exception.HResult:X8}) {e.Exception}\n\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickDock");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UnhandledException: {e.ExceptionObject}\n\n");
            }
            catch { }
        };

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            bool isPackaged = false;
            try
            {
                var package = Windows.ApplicationModel.Package.Current;
                isPackaged = package != null;
            }
            catch
            {
                isPackaged = false;
            }

            // In self-contained unpackaged WinUI 3 (WindowsAppSDKSelfContained=true),
            // do not invoke Bootstrap.Initialize because all runtime binaries are deployed side-by-side.

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        finally
        {
            try
            {
                Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
            }
            catch
            {
                // Best effort shutdown
            }

            if (_instanceMutex != null)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Mutex may have already been released or abandoned
                }
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
        }
    }
}
