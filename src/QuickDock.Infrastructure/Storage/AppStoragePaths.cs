namespace QuickDock.Infrastructure.Storage;

/// <summary>
/// Centralized management of application directories and file paths in %LOCALAPPDATA%\QuickDock.
/// </summary>
public static class AppStoragePaths
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickDock");

    public static string RootDirectory => BaseDirectory;
    public static string ConfigFile => Path.Combine(BaseDirectory, "config.json");
    public static string DatabaseFile => Path.Combine(BaseDirectory, "quickdock.db");
    public static string LogsDirectory => Path.Combine(BaseDirectory, "Logs");
    public static string CacheDirectory => Path.Combine(BaseDirectory, "Cache");
    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");
    public static string QuickDropDirectory => Path.Combine(BaseDirectory, "QuickDrop");

    /// <summary>
    /// Ensures that all required application directories exist on disk.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(QuickDropDirectory);
    }
}

