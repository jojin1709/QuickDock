using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace QuickDock.Setup;

public partial class MainWindow : Window
{
    private int _currentStep = 1;

    public MainWindow()
    {
        InitializeComponent();
        UpdateStep(1);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            UpdateStep(_currentStep - 1);
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStep(4);
    }

    private async void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < 4)
        {
            UpdateStep(_currentStep + 1);
        }
        else
        {
            await RunInstallationAsync();
        }
    }

    private void UpdateStep(int step)
    {
        _currentStep = Math.Clamp(step, 1, 4);

        Step1Grid.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Grid.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Grid.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Grid.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Pagination Dots
        Dot1.Fill = new SolidColorBrush(_currentStep == 1 ? Colors.White : Color.FromRgb(0x33, 0x33, 0x33));
        Dot2.Fill = new SolidColorBrush(_currentStep == 2 ? Colors.White : Color.FromRgb(0x33, 0x33, 0x33));
        Dot3.Fill = new SolidColorBrush(_currentStep == 3 ? Colors.White : Color.FromRgb(0x33, 0x33, 0x33));
        Dot4.Fill = new SolidColorBrush(_currentStep == 4 ? Colors.White : Color.FromRgb(0x33, 0x33, 0x33));

        // Navigation Buttons
        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = (_currentStep == 1 || _currentStep == 2) ? Visibility.Visible : Visibility.Collapsed;

        PrimaryActionButton.Content = _currentStep switch
        {
            1 => "Get started",
            2 => "Continue",
            3 => "Continue",
            4 => "Start using QuickDock",
            _ => "Continue"
        };
    }

    private void SelectTheme(int index)
    {
        var activeBrush = new SolidColorBrush(Colors.White);
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E));
        var activeText = new SolidColorBrush(Colors.White);
        var inactiveText = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        ThemePureBlackBorder.BorderBrush = index == 0 ? activeBrush : inactiveBrush;
        ThemePureBlackBorder.BorderThickness = new Thickness(index == 0 ? 1.5 : 1);
        ThemePureBlackText.Foreground = index == 0 ? activeText : inactiveText;

        ThemeVividBorder.BorderBrush = index == 1 ? activeBrush : inactiveBrush;
        ThemeVividBorder.BorderThickness = new Thickness(index == 1 ? 1.5 : 1);
        ThemeVividText.Foreground = index == 1 ? activeText : inactiveText;

        ThemeEmberBorder.BorderBrush = index == 2 ? activeBrush : inactiveBrush;
        ThemeEmberBorder.BorderThickness = new Thickness(index == 2 ? 1.5 : 1);
        ThemeEmberText.Foreground = index == 2 ? activeText : inactiveText;

        ThemeSilverBorder.BorderBrush = index == 3 ? activeBrush : inactiveBrush;
        ThemeSilverBorder.BorderThickness = new Thickness(index == 3 ? 1.5 : 1);
        ThemeSilverText.Foreground = index == 3 ? activeText : inactiveText;
    }

    private void Theme_PureBlack_Click(object sender, MouseButtonEventArgs e) => SelectTheme(0);
    private void Theme_Vivid_Click(object sender, MouseButtonEventArgs e) => SelectTheme(1);
    private void Theme_Ember_Click(object sender, MouseButtonEventArgs e) => SelectTheme(2);
    private void Theme_Silver_Click(object sender, MouseButtonEventArgs e) => SelectTheme(3);

    private void SelectStyle(int index)
    {
        var activeBg = new SolidColorBrush(Colors.White);
        var inactiveBg = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26));
        var activeFg = new SolidColorBrush(Colors.Black);
        var inactiveFg = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

        StyleCometPill.Background = index == 0 ? activeBg : inactiveBg;
        StyleCometText.Foreground = index == 0 ? activeFg : inactiveFg;
        StyleCometText.FontWeight = index == 0 ? FontWeights.SemiBold : FontWeights.Normal;

        StyleTwinCometsPill.Background = index == 1 ? activeBg : inactiveBg;
        StyleTwinCometsText.Foreground = index == 1 ? activeFg : inactiveFg;
        StyleTwinCometsText.FontWeight = index == 1 ? FontWeights.SemiBold : FontWeights.Normal;

        StyleBreathePill.Background = index == 2 ? activeBg : inactiveBg;
        StyleBreatheText.Foreground = index == 2 ? activeFg : inactiveFg;
        StyleBreatheText.FontWeight = index == 2 ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void StyleComet_Click(object sender, MouseButtonEventArgs e) => SelectStyle(0);
    private void StyleTwinComets_Click(object sender, MouseButtonEventArgs e) => SelectStyle(1);
    private void StyleBreathe_Click(object sender, MouseButtonEventArgs e) => SelectStyle(2);

    private async Task RunInstallationAsync()
    {
        PrimaryActionButton.IsEnabled = false;
        BackButton.IsEnabled = false;
        SetupProgressBar.Visibility = Visibility.Visible;
        StatusMessageText.Text = "Setting up QuickDock on your PC...";

        string targetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "QuickDock");

        try
        {
            // Close running QuickDock instances
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("QuickDock.App"))
                {
                    try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                }
            });

            // Extract application binaries
            StatusMessageText.Text = "Extracting application binaries...";
            await Task.Run(() => ExtractPayload(targetDir));

            // Copy setup as uninstaller
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            string uninstallerExe = Path.Combine(targetDir, "QuickDock-Setup.exe");
            if (File.Exists(currentExe) && !string.Equals(currentExe, uninstallerExe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, uninstallerExe, true);
            }

            // Shortcuts
            string appExe = Path.Combine(targetDir, "QuickDock.App.exe");
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "QuickDock.lnk");
            string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "QuickDock.lnk");

            CreateShortcut(desktopShortcut, appExe, targetDir);
            CreateShortcut(startMenuShortcut, appExe, targetDir);

            // Windows Startup
            if (LaunchAtLoginCheckBox.IsChecked == true)
            {
                try
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    runKey?.SetValue("QuickDock", $"\"{appExe}\"");
                }
                catch { }
            }

            // Windows Uninstall Registration
            RegisterUninstall(targetDir, appExe, uninstallerExe);

            StatusMessageText.Text = "QuickDock is ready!";
            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

            // Launch QuickDock
            if (File.Exists(appExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }

            await Task.Delay(1000);
            Close();
        }
        catch (Exception ex)
        {
            StatusMessageText.Text = "Installation error: " + ex.Message;
            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            PrimaryActionButton.IsEnabled = true;
            BackButton.IsEnabled = true;
            SetupProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private static void ExtractPayload(string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        Stream? stream = null;
        var assembly = Assembly.GetExecutingAssembly();

        // 1. Try exact logical name
        stream = assembly.GetManifestResourceStream("payload.zip");

        // 2. Try any manifest resource ending with payload.zip or .zip
        if (stream == null)
        {
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    stream = assembly.GetManifestResourceStream(name);
                    if (stream != null) break;
                }
            }
        }

        // 3. Fallback: check file next to executable or in dev folders
        if (stream == null)
        {
            string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
            if (File.Exists(localZip))
            {
                stream = File.OpenRead(localZip);
            }
        }

        if (stream == null)
        {
            string devPublishDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "publish", "QuickDock-win-x64");
            if (Directory.Exists(devPublishDir))
            {
                CopyDirectoryRecursive(devPublishDir, targetDir);
                return;
            }

            throw new InvalidOperationException("Installation payload resource 'payload.zip' was not found in the executable.");
        }

        using (stream)
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            string destFull = Path.GetFullPath(targetDir);
            if (!destFull.EndsWith(Path.DirectorySeparatorChar))
            {
                destFull += Path.DirectorySeparatorChar;
            }

            foreach (var entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                // Zip-Slip Security Defense
                if (!destinationPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? parentDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(subDir, Path.Combine(targetDir, Path.GetFileName(subDir)));
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetExePath, string targetDir)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetExePath;
                shortcut.WorkingDirectory = targetDir;
                shortcut.Description = "QuickDock — Windows 11 Native Utility Dock";
                shortcut.IconLocation = targetExePath + ",0";
                shortcut.Save();
            }
        }
        catch { }
    }

    private static void RegisterUninstall(string installDir, string appExe, string uninstallerExe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock");
            if (key != null)
            {
                key.SetValue("DisplayName", "QuickDock");
                key.SetValue("DisplayVersion", "1.0.1");
                key.SetValue("Publisher", "JOJIN JOHN");
                key.SetValue("DisplayIcon", $"{appExe},0");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", $"\"{uninstallerExe}\" /uninstall");
                key.SetValue("QuietUninstallString", $"\"{uninstallerExe}\" /uninstall /quiet");
                key.SetValue("URLInfoAbout", "https://www.linkedin.com/in/jojin-john/");
                key.SetValue("HelpLink", "https://github.com/jojin1709/QuickDock/issues");
                key.SetValue("EstimatedSize", 320000, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}
