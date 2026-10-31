using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace QuickDock.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
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
        }
    }
}
