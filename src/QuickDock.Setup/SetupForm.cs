using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickDock.Setup;

public class SetupForm : Form
{
    private readonly bool _isUninstall;
    
    // UI Controls
    private PictureBox _iconBox = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _pathLabel = null!;
    private TextBox _pathTextBox = null!;
    private Button _browseButton = null!;
    private CheckBox _licenseCheckBox = null!;
    private CheckBox _desktopShortcutCheckBox = null!;
    private CheckBox _startMenuShortcutCheckBox = null!;
    private CheckBox _autoStartCheckBox = null!;
    private CheckBox _firewallCheckBox = null!;
    private CheckBox _launchAfterCheckBox = null!;
    private ProgressBar _progressBar = null!;
    private Label _statusLabel = null!;
    private Button _installButton = null!;
    private Button _cancelButton = null!;

    public SetupForm(bool isUninstall)
    {
        _isUninstall = isUninstall;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = _isUninstall ? "QuickDock — Uninstall" : "QuickDock 1.0.0 — Setup";
        ClientSize = new Size(520, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        Font = new Font("Segoe UI Variable Display", 9.5f, FontStyle.Regular);

        // Load Icon
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch { }

        // Header Icon
        _iconBox = new PictureBox
        {
            Location = new Point(24, 20),
            Size = new Size(52, 52),
            SizeMode = PictureBoxSizeMode.Zoom
        };
        try
        {
            string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon-64x64.png");
            if (File.Exists(pngPath))
            {
                _iconBox.Image = Image.FromFile(pngPath);
            }
        }
        catch { }
        Controls.Add(_iconBox);

        // Header Title
        _titleLabel = new Label
        {
            Location = new Point(88, 20),
            Size = new Size(400, 28),
            Text = _isUninstall ? "Uninstall QuickDock" : "QuickDock 1.0.0 Setup",
            Font = new Font("Segoe UI Variable Display", 15f, FontStyle.Bold),
            ForeColor = Color.White
        };
        Controls.Add(_titleLabel);

        // Subtitle
        _subtitleLabel = new Label
        {
            Location = new Point(88, 50),
            Size = new Size(400, 20),
            Text = "Author: JOJIN JOHN • Windows 11 Native Utility Dock",
            Font = new Font("Segoe UI Variable Display", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 180, 180)
        };
        Controls.Add(_subtitleLabel);

        // Divider
        var divider = new Panel
        {
            Location = new Point(24, 82),
            Size = new Size(472, 1),
            BackColor = Color.FromArgb(55, 55, 55)
        };
        Controls.Add(divider);

        int currentY = 96;

        if (!_isUninstall)
        {
            // Destination path
            _pathLabel = new Label
            {
                Location = new Point(24, currentY),
                Size = new Size(472, 20),
                Text = "Installation Directory:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_pathLabel);
            currentY += 24;

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QuickDock");
            _pathTextBox = new TextBox
            {
                Location = new Point(24, currentY),
                Size = new Size(380, 26),
                Text = defaultDir,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_pathTextBox);

            _browseButton = new Button
            {
                Location = new Point(412, currentY - 1),
                Size = new Size(84, 28),
                Text = "Browse...",
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _browseButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            _browseButton.Click += (s, e) =>
            {
                using var folderDialog = new FolderBrowserDialog();
                folderDialog.SelectedPath = _pathTextBox.Text;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _pathTextBox.Text = folderDialog.SelectedPath;
                }
            };
            Controls.Add(_browseButton);
            currentY += 40;

            // Permissions and Setup Options
            var optionsHeader = new Label
            {
                Location = new Point(24, currentY),
                Size = new Size(472, 20),
                Text = "Setup & Permissions Preferences:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(optionsHeader);
            currentY += 24;

            _licenseCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "I accept the Proprietary License (Closed source; no copying/cloning)",
                Checked = true,
                ForeColor = Color.White
            };
            _licenseCheckBox.CheckedChanged += (s, e) => _installButton.Enabled = _licenseCheckBox.Checked;
            Controls.Add(_licenseCheckBox);
            currentY += 26;

            _desktopShortcutCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "Create Desktop Shortcut",
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_desktopShortcutCheckBox);
            currentY += 26;

            _startMenuShortcutCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "Add to Windows Start Menu",
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_startMenuShortcutCheckBox);
            currentY += 26;

            _autoStartCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "Start QuickDock automatically when signing in to Windows",
                Checked = false,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_autoStartCheckBox);
            currentY += 26;

            _firewallCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "Allow QuickDrop Wi-Fi file transfer through Windows Firewall",
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_firewallCheckBox);
            currentY += 26;

            _launchAfterCheckBox = new CheckBox
            {
                Location = new Point(26, currentY),
                Size = new Size(470, 22),
                Text = "Launch QuickDock when setup completes",
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(_launchAfterCheckBox);
            currentY += 36;
        }
        else
        {
            var uninstallInfo = new Label
            {
                Location = new Point(24, currentY),
                Size = new Size(472, 60),
                Text = "Are you sure you want to completely uninstall QuickDock?\n\nThis will remove the application binaries and shortcuts from your computer.",
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(uninstallInfo);
            currentY += 80;
        }

        // Progress bar
        _progressBar = new ProgressBar
        {
            Location = new Point(24, currentY),
            Size = new Size(472, 10),
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };
        Controls.Add(_progressBar);
        currentY += 16;

        // Status Label
        _statusLabel = new Label
        {
            Location = new Point(24, currentY),
            Size = new Size(472, 20),
            Text = _isUninstall ? "Ready to uninstall." : "Ready to install QuickDock.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(160, 160, 160)
        };
        Controls.Add(_statusLabel);

        // Bottom Action Buttons
        _cancelButton = new Button
        {
            Location = new Point(412, 428),
            Size = new Size(84, 30),
            Text = "Cancel",
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
        _cancelButton.Click += (s, e) => Close();
        Controls.Add(_cancelButton);

        _installButton = new Button
        {
            Location = new Point(296, 428),
            Size = new Size(106, 30),
            Text = _isUninstall ? "Uninstall" : "Install Now",
            BackColor = Color.FromArgb(0, 120, 212), // Windows 11 Accent
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += async (s, e) =>
        {
            if (_isUninstall)
            {
                await ExecuteUninstallAsync();
            }
            else
            {
                await ExecuteInstallAsync();
            }
        };
        Controls.Add(_installButton);
    }

    private async Task ExecuteInstallAsync()
    {
        string targetDir = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Please choose a valid destination folder.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Disable input during execution
        _installButton.Enabled = false;
        _browseButton.Enabled = false;
        _pathTextBox.Enabled = false;
        _cancelButton.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Value = 10;

        try
        {
            // Step 1: Kill running instances if updating
            _statusLabel.Text = "Checking for existing QuickDock instances...";
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("QuickDock.App"))
                {
                    try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                }
            });
            _progressBar.Value = 20;

            // Step 2: Create directory
            _statusLabel.Text = "Preparing destination directory...";
            Directory.CreateDirectory(targetDir);
            _progressBar.Value = 30;

            // Step 3: Extract payload
            _statusLabel.Text = "Extracting application binaries...";
            await Task.Run(() => ExtractEmbeddedPayload(targetDir));
            _progressBar.Value = 70;

            // Step 4: Copy setup.exe as uninstaller in install dir
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            string uninstallerExe = Path.Combine(targetDir, "QuickDock-Setup.exe");
            if (File.Exists(currentExe) && !string.Equals(currentExe, uninstallerExe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, uninstallerExe, true);
            }
            _progressBar.Value = 75;

            // Step 5: Shortcuts
            string appExe = Path.Combine(targetDir, "QuickDock.App.exe");
            if (_desktopShortcutCheckBox.Checked)
            {
                _statusLabel.Text = "Creating Desktop shortcut...";
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "QuickDock.lnk");
                CreateShortcut(desktopPath, appExe, targetDir);
            }
            _progressBar.Value = 82;

            if (_startMenuShortcutCheckBox.Checked)
            {
                _statusLabel.Text = "Creating Start Menu shortcut...";
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "QuickDock.lnk");
                CreateShortcut(startMenuPath, appExe, targetDir);
            }
            _progressBar.Value = 88;

            // Step 6: Startup with Windows if requested
            if (_autoStartCheckBox.Checked)
            {
                _statusLabel.Text = "Configuring Windows startup...";
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.SetValue("QuickDock", $"\"{appExe}\"");
            }

            // Step 7: Optional Firewall configuration for QuickDrop
            if (_firewallCheckBox.Checked)
            {
                _statusLabel.Text = "Configuring local firewall rule for QuickDrop...";
                await Task.Run(() => TryAddFirewallRule(appExe));
            }
            _progressBar.Value = 94;

            // Step 8: Register in Windows Add/Remove Programs
            _statusLabel.Text = "Registering application in Windows...";
            RegisterUninstallKey(targetDir, appExe, uninstallerExe);
            _progressBar.Value = 100;

            _statusLabel.Text = "QuickDock installation completed successfully!";
            _statusLabel.ForeColor = Color.FromArgb(76, 175, 80);

            // Launch application
            if (_launchAfterCheckBox.Checked && File.Exists(appExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }

            await Task.Delay(1200);
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Installation failed: " + ex.Message;
            _statusLabel.ForeColor = Color.FromArgb(244, 67, 54);
            MessageBox.Show("Installation failed:\n" + ex.Message, "QuickDock Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installButton.Enabled = true;
            _cancelButton.Enabled = true;
        }
    }

    private void ExtractEmbeddedPayload(string targetDir)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("payload.zip");
        if (stream == null)
        {
            // If running during dev/testing without embedded payload, copy from publish folder
            string devPublishDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "publish", "QuickDock-win-x64");
            if (Directory.Exists(devPublishDir))
            {
                CopyDirectoryRecursive(devPublishDir, targetDir);
                return;
            }
            throw new InvalidOperationException("Installation payload resource 'payload.zip' was not found in the executable.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        string destFull = Path.GetFullPath(targetDir);
        if (!destFull.EndsWith(Path.DirectorySeparatorChar))
        {
            destFull += Path.DirectorySeparatorChar;
        }

        foreach (var entry in archive.Entries)
        {
            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            // Zip-Slip Guard
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

    private static void TryAddFirewallRule(string appExePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"QuickDock QuickDrop P2P\" dir=in action=allow program=\"{appExePath}\" enable=yes",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit(3000);
        }
        catch { }
    }

    private static void RegisterUninstallKey(string installDir, string appExe, string uninstallerExe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock");
            if (key != null)
            {
                key.SetValue("DisplayName", "QuickDock");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "JOJIN JOHN");
                key.SetValue("DisplayIcon", $"{appExe},0");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", $"\"{uninstallerExe}\" /uninstall");
                key.SetValue("URLInfoAbout", "https://www.linkedin.com/in/jojin-john/");
                key.SetValue("HelpLink", "https://github.com/jojin1709/QuickDock/issues");
                key.SetValue("EstimatedSize", 320000, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private async Task ExecuteUninstallAsync()
    {
        _installButton.Enabled = false;
        _cancelButton.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Value = 20;
        _statusLabel.Text = "Closing running QuickDock instances...";

        try
        {
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("QuickDock.App"))
                {
                    try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                }
            });
            _progressBar.Value = 40;

            _statusLabel.Text = "Removing Start Menu and Desktop shortcuts...";
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "QuickDock.lnk");
            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

            string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "QuickDock.lnk");
            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

            _progressBar.Value = 60;

            _statusLabel.Text = "Removing Windows registry settings...";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock", false);
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("QuickDock", false);
            }
            catch { }

            _progressBar.Value = 80;

            _statusLabel.Text = "QuickDock has been successfully uninstalled.";
            _statusLabel.ForeColor = Color.FromArgb(76, 175, 80);
            _progressBar.Value = 100;

            MessageBox.Show("QuickDock has been successfully uninstalled from your computer.", "QuickDock Uninstalled", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Schedule self-delete of directory on exit via cmd
            string appDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            if (Directory.Exists(appDir) && appDir.Contains("QuickDock", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 & rmdir /s /q \"{appDir}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error during uninstall: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
