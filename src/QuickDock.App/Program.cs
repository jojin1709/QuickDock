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
        string mutexName = $"Global\\QuickDock_SingleInstance_{Environment.UserName}";
        _instanceMutex = new Mutex(true, mutexName, out bool isInitialInstance);

        if (!isInitialInstance)
        {
            // Another instance is already running; signal it to activate and bring to front
            uint activateMsg = NativeMethods.RegisterWindowMessageW("QUICKDOCK_ACTIVATE_INSTANCE");
            if (activateMsg != 0)
            {
                NativeMethods.PostMessageW(NativeMethods.HWND_BROADCAST, activateMsg, IntPtr.Zero, IntPtr.Zero);
            }
            return;
        }

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

            if (!isPackaged)
            {
                try
                {
                    const int majorMinorVersion = 0x00020006; // Version 2.6
                    Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(majorMinorVersion);
                }
                catch
                {
                    // Fall back to self-contained binaries if dynamic dependency registration is skipped
                }
            }

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
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
        }
    }
}
