using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QuickDock.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool isUninstall = args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("-u", StringComparison.OrdinalIgnoreCase));

        Application.Run(new SetupForm(isUninstall));
    }
}
