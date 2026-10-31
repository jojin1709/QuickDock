using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace QuickDock.App.ViewModels;

public sealed partial class DeveloperViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly IDockerService _dockerService;
    private readonly ITerminalLauncherService _terminalService;
    private readonly IJsonToolkitService _jsonService;
    private readonly IJwtToolkitService _jwtService;
    private readonly IEncodingToolkitService _encodingService;
    private readonly IDeveloperHashService _hashService;
    private readonly IUuidToolkitService _uuidService;
    private readonly ITimestampToolkitService _timestampService;
    private readonly IRegexToolkitService _regexService;
    private readonly IQrGeneratorService _qrService;
    private readonly IHttpToolkitService _httpService;
    private readonly ILocalDevelopmentService _localDevService;
    private readonly IDeveloperProcessService _processService;
    private readonly ILogger<DeveloperViewModel> _logger;

    private CancellationTokenSource? _httpCts;

    [ObservableProperty]
    private string _selectedSection = "Git"; // Git, Docker, Terminal, Tools, Http, LocalDev, Processes

    #region Git Properties

    [ObservableProperty]
    private string _gitRepoPath = Environment.CurrentDirectory;

    [ObservableProperty]
    private bool _isGitInstalled = true;

    [ObservableProperty]
    private string _gitVersionText = "Checking...";

    [ObservableProperty]
    private bool _isGitRepo;

    [ObservableProperty]
    private string _gitBranch = string.Empty;

    [ObservableProperty]
    private string _gitHeadMessage = string.Empty;

    [ObservableProperty]
    private string _gitDiffText = string.Empty;

    [ObservableProperty]
    private string _gitCommitMessage = string.Empty;

    [ObservableProperty]
    private string _gitStatusMessage = string.Empty;

    [ObservableProperty]
    private GitFileStatus? _selectedGitFile;

    [ObservableProperty]
    private bool _isGitBusy;

    public ObservableCollection<GitFileStatus> GitFiles { get; } = [];
    public ObservableCollection<GitCommitInfo> GitCommits { get; } = [];
    public ObservableCollection<GitBranchInfo> GitBranches { get; } = [];

    #endregion

    #region Docker Properties

    [ObservableProperty]
    private bool _isDockerAvailable;

    [ObservableProperty]
    private string _dockerVersion = "Unknown";

    [ObservableProperty]
    private string _dockerEngineState = "Checking...";

    [ObservableProperty]
    private string _dockerStatusMessage = string.Empty;

    [ObservableProperty]
    private DockerContainerInfo? _selectedDockerContainer;

    [ObservableProperty]
    private string _dockerLogsText = string.Empty;

    [ObservableProperty]
    private bool _isDockerBusy;

    public ObservableCollection<DockerContainerInfo> DockerContainers { get; } = [];
    public ObservableCollection<DockerImageInfo> DockerImages { get; } = [];

    #endregion

    #region Terminal Properties

    [ObservableProperty]
    private string _terminalDirectory = Environment.CurrentDirectory;

    [ObservableProperty]
    private TerminalType _selectedTerminalType = TerminalType.WindowsTerminal;

    [ObservableProperty]
    private bool _terminalAsAdmin;

    [ObservableProperty]
    private string _terminalStatusMessage = string.Empty;

    #endregion

    #region Dev Tools Properties

    [ObservableProperty]
    private string _selectedTool = "Json"; // Json, Jwt, Encoding, Hash, Uuid, Timestamp, Regex, Qr

    // JSON
    [ObservableProperty]
    private string _jsonInput = "{\n  \"name\": \"QuickDock\",\n  \"phase\": 7,\n  \"status\": \"Production\"\n}";

    [ObservableProperty]
    private string _jsonOutput = string.Empty;

    [ObservableProperty]
    private string _jsonStatusMessage = string.Empty;

    // JWT
    [ObservableProperty]
    private string _jwtInput = string.Empty;

    [ObservableProperty]
    private string _jwtSecret = string.Empty;

    [ObservableProperty]
    private string _jwtHeaderJson = string.Empty;

    [ObservableProperty]
    private string _jwtPayloadJson = string.Empty;

    [ObservableProperty]
    private string _jwtAlg = string.Empty;

    [ObservableProperty]
    private string _jwtStatusMessage = "Decoding a JWT does not verify its signature.";

    [ObservableProperty]
    private bool _isJwtDecoded;

    [ObservableProperty]
    private bool _isJwtVerified;

    // Encoding
    [ObservableProperty]
    private string _encodingInput = "Hello QuickDock Developer Toolkit!";

    [ObservableProperty]
    private string _encodingOutput = string.Empty;

    [ObservableProperty]
    private string _encodingStatusMessage = string.Empty;

    // Hash
    [ObservableProperty]
    private string _hashInput = string.Empty;

    [ObservableProperty]
    private string _hashOutput = string.Empty;

    [ObservableProperty]
    private DeveloperHashAlgorithm _selectedHashAlgorithm = DeveloperHashAlgorithm.SHA256;

    [ObservableProperty]
    private string _hashStatusMessage = string.Empty;

    // UUID
    [ObservableProperty]
    private int _uuidCount = 1;

    [ObservableProperty]
    private bool _uuidUppercase;

    [ObservableProperty]
    private bool _uuidHyphens = true;

    [ObservableProperty]
    private bool _uuidBraces;

    [ObservableProperty]
    private string _uuidOutput = string.Empty;

    // Timestamp
    [ObservableProperty]
    private string _timestampInput = string.Empty;

    [ObservableProperty]
    private string _timestampIsoOutput = string.Empty;

    [ObservableProperty]
    private string _timestampRelativeOutput = string.Empty;

    [ObservableProperty]
    private string _timestampStatusMessage = string.Empty;

    // Regex
    [ObservableProperty]
    private string _regexPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";

    [ObservableProperty]
    private string _regexInput = "Contact us at dev@quickdock.local or support@quickdock.com.";

    [ObservableProperty]
    private string _regexReplacement = "[EMAIL]";

    [ObservableProperty]
    private string _regexOutput = string.Empty;

    [ObservableProperty]
    private string _regexStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _regexIgnoreCase = true;

    [ObservableProperty]
    private bool _regexMultiline;

    [ObservableProperty]
    private bool _regexSingleline;

    // QR
    [ObservableProperty]
    private string _qrInput = "https://github.com/jojin1709/QuickDock";

    [ObservableProperty]
    private string _qrType = "Text"; // Text, WiFi

    [ObservableProperty]
    private string _wiFiSsid = string.Empty;

    [ObservableProperty]
    private string _wiFiPassword = string.Empty;

    [ObservableProperty]
    private string _wiFiAuthType = "WPA";

    [ObservableProperty]
    private byte[]? _qrPngBytes;

    [ObservableProperty]
    private string _qrStatusMessage = string.Empty;

    #endregion

    #region HTTP Properties

    [ObservableProperty]
    private string _httpMethod = "GET";

    [ObservableProperty]
    private string _httpUrl = "https://httpbin.org/get";

    [ObservableProperty]
    private string _httpHeadersInput = "User-Agent: QuickDock/1.0\nAccept: application/json";

    [ObservableProperty]
    private string _httpBodyInput = string.Empty;

    [ObservableProperty]
    private string _httpContentType = "application/json";

    [ObservableProperty]
    private int _httpTimeoutSeconds = 30;

    [ObservableProperty]
    private string _httpResponseStatus = string.Empty;

    [ObservableProperty]
    private string _httpResponseMetrics = string.Empty;

    [ObservableProperty]
    private string _httpResponseHeaders = string.Empty;

    [ObservableProperty]
    private string _httpResponseBody = string.Empty;

    [ObservableProperty]
    private bool _isHttpBusy;

    #endregion

    #region Local Dev Properties

    [ObservableProperty]
    private string _portSearchFilter = string.Empty;

    [ObservableProperty]
    private bool _isPortsBusy;

    private readonly List<ListeningPortInfo> _allPorts = [];
    public ObservableCollection<ListeningPortInfo> FilteredPorts { get; } = [];

    #endregion

    #region Process Properties

    [ObservableProperty]
    private string _processSearchFilter = string.Empty;

    [ObservableProperty]
    private DeveloperProcessInfo? _selectedProcess;

    [ObservableProperty]
    private bool _isProcessesBusy;

    [ObservableProperty]
    private string _processStatusMessage = string.Empty;

    private readonly List<DeveloperProcessInfo> _allProcesses = [];
    public ObservableCollection<DeveloperProcessInfo> FilteredProcesses { get; } = [];

    #endregion

    public DeveloperViewModel(
        IGitService gitService,
        IDockerService dockerService,
        ITerminalLauncherService terminalService,
        IJsonToolkitService jsonService,
        IJwtToolkitService jwtService,
        IEncodingToolkitService encodingService,
        IDeveloperHashService hashService,
        IUuidToolkitService uuidService,
        ITimestampToolkitService timestampService,
        IRegexToolkitService regexService,
        IQrGeneratorService qrService,
        IHttpToolkitService httpService,
        ILocalDevelopmentService localDevService,
        IDeveloperProcessService processService,
        ILogger<DeveloperViewModel> logger)
    {
        _gitService = gitService;
        _dockerService = dockerService;
        _terminalService = terminalService;
        _jsonService = jsonService;
        _jwtService = jwtService;
        _encodingService = encodingService;
        _hashService = hashService;
        _uuidService = uuidService;
        _timestampService = timestampService;
        _regexService = regexService;
        _qrService = qrService;
        _httpService = httpService;
        _localDevService = localDevService;
        _processService = processService;
        _logger = logger;
    }

    public async Task InitializeSectionAsync(string section)
    {
        SelectedSection = section;

        switch (section)
        {
            case "Git":
                await RefreshGitAsync();
                break;
            case "Docker":
                await RefreshDockerAsync();
                break;
            case "Tools":
                ExecuteInitialToolAction();
                break;
            case "LocalDev":
                await RefreshPortsAsync();
                break;
            case "Processes":
                await RefreshProcessesAsync();
                break;
        }
    }

    #region Git Commands

    [RelayCommand]
    public async Task RefreshGitAsync()
    {
        IsGitBusy = true;
        GitStatusMessage = "Querying repository status...";
        try
        {
            IsGitInstalled = await _gitService.IsGitInstalledAsync();
            GitVersionText = await _gitService.GetGitVersionAsync();

            if (!IsGitInstalled)
            {
                GitStatusMessage = "Git is not installed or could not be located.";
                IsGitRepo = false;
                return;
            }

            string targetDir = string.IsNullOrWhiteSpace(GitRepoPath) ? Environment.CurrentDirectory : GitRepoPath;
            IsGitRepo = await _gitService.IsGitRepositoryAsync(targetDir);

            if (!IsGitRepo)
            {
                GitStatusMessage = $"'{targetDir}' is not a Git repository.";
                GitFiles.Clear();
                GitCommits.Clear();
                GitBranches.Clear();
                GitBranch = string.Empty;
                GitHeadMessage = string.Empty;
                return;
            }

            var status = await _gitService.GetRepositoryStatusAsync(targetDir);
            GitBranch = status.CurrentBranch;
            GitHeadMessage = $"{status.HeadCommitHash[..Math.Min(7, status.HeadCommitHash.Length)]} {status.HeadCommitMessage}";

            GitFiles.Clear();
            foreach (var f in status.Files)
            {
                GitFiles.Add(f);
            }

            var commits = await _gitService.GetRecentCommitsAsync(targetDir, 15);
            GitCommits.Clear();
            foreach (var c in commits)
            {
                GitCommits.Add(c);
            }

            var branches = await _gitService.GetBranchesAsync(targetDir);
            GitBranches.Clear();
            foreach (var b in branches)
            {
                GitBranches.Add(b);
            }

            GitStatusMessage = status.IsClean
                ? $"Working tree clean on branch '{status.CurrentBranch}'."
                : $"{status.Files.Count} modified/untracked file(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh git status.");
            GitStatusMessage = $"Git error: {ex.Message}";
        }
        finally
        {
            IsGitBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewDiffAsync(GitFileStatus? file)
    {
        if (file == null) return;
        SelectedGitFile = file;
        try
        {
            GitDiffText = await _gitService.GetDiffAsync(GitRepoPath, file.Path);
            if (string.IsNullOrWhiteSpace(GitDiffText))
            {
                GitDiffText = "(No unstaged diff or untracked file)";
            }
        }
        catch (Exception ex)
        {
            GitDiffText = $"Error getting diff: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StageFileAsync(GitFileStatus? file)
    {
        if (file == null) return;
        var res = await _gitService.StageFileAsync(GitRepoPath, file.Path);
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Stage failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task StageAllAsync()
    {
        var res = await _gitService.StageAllAsync(GitRepoPath);
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Stage all failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task UnstageFileAsync(GitFileStatus? file)
    {
        if (file == null) return;
        var res = await _gitService.UnstageFileAsync(GitRepoPath, file.Path);
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Unstage failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(GitCommitMessage))
        {
            GitStatusMessage = "Please enter a commit message.";
            return;
        }

        var res = await _gitService.CommitAsync(GitRepoPath, GitCommitMessage.Trim());
        if (res.Success)
        {
            GitCommitMessage = string.Empty;
            await RefreshGitAsync();
            GitStatusMessage = "Committed successfully.";
        }
        else
        {
            GitStatusMessage = $"Commit failed: {res.ErrorOutput}";
        }
    }

    [RelayCommand]
    public async Task FetchAsync()
    {
        IsGitBusy = true;
        GitStatusMessage = "Fetching remotes...";
        var res = await _gitService.FetchAsync(GitRepoPath);
        IsGitBusy = false;
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Fetch failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task PullAsync()
    {
        IsGitBusy = true;
        GitStatusMessage = "Pulling latest changes...";
        var res = await _gitService.PullAsync(GitRepoPath);
        IsGitBusy = false;
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Pull failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task PushAsync()
    {
        IsGitBusy = true;
        GitStatusMessage = "Pushing to remote...";
        var res = await _gitService.PushAsync(GitRepoPath);
        IsGitBusy = false;
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Push failed: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task DiscardChangesAsync(GitFileStatus? file)
    {
        if (file == null) return;
        var res = await _gitService.DiscardFileChangesAsync(GitRepoPath, file.Path);
        if (res.Success) await RefreshGitAsync();
        else GitStatusMessage = $"Discard failed: {res.ErrorOutput}";
    }

    #endregion

    #region Docker Commands

    [RelayCommand]
    public async Task RefreshDockerAsync()
    {
        IsDockerBusy = true;
        DockerStatusMessage = "Checking Docker state...";
        try
        {
            var status = await _dockerService.GetDockerStatusAsync();
            IsDockerAvailable = status.IsAvailable;
            DockerVersion = status.Version;
            DockerEngineState = status.EngineState;
            DockerStatusMessage = status.StatusMessage;

            if (status.IsAvailable && status.EngineState.Equals("Running", StringComparison.OrdinalIgnoreCase))
            {
                var containers = await _dockerService.GetContainersAsync(all: true);
                DockerContainers.Clear();
                foreach (var c in containers) DockerContainers.Add(c);

                var images = await _dockerService.GetImagesAsync();
                DockerImages.Clear();
                foreach (var img in images) DockerImages.Add(img);
            }
            else
            {
                DockerContainers.Clear();
                DockerImages.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Docker state.");
            DockerStatusMessage = $"Docker error: {ex.Message}";
        }
        finally
        {
            IsDockerBusy = false;
        }
    }

    [RelayCommand]
    public async Task StartContainerAsync(DockerContainerInfo? container)
    {
        if (container == null) return;
        var res = await _dockerService.StartContainerAsync(container.Id);
        if (res.Success) await RefreshDockerAsync();
        else DockerStatusMessage = $"Failed to start container: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task StopContainerAsync(DockerContainerInfo? container)
    {
        if (container == null) return;
        var res = await _dockerService.StopContainerAsync(container.Id);
        if (res.Success) await RefreshDockerAsync();
        else DockerStatusMessage = $"Failed to stop container: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task RestartContainerAsync(DockerContainerInfo? container)
    {
        if (container == null) return;
        var res = await _dockerService.RestartContainerAsync(container.Id);
        if (res.Success) await RefreshDockerAsync();
        else DockerStatusMessage = $"Failed to restart container: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task ViewContainerLogsAsync(DockerContainerInfo? container)
    {
        if (container == null) return;
        SelectedDockerContainer = container;
        DockerLogsText = await _dockerService.GetContainerLogsAsync(container.Id);
    }

    [RelayCommand]
    public async Task RemoveContainerAsync(DockerContainerInfo? container)
    {
        if (container == null) return;
        var res = await _dockerService.RemoveContainerAsync(container.Id, force: true);
        if (res.Success) await RefreshDockerAsync();
        else DockerStatusMessage = $"Failed to remove container: {res.ErrorOutput}";
    }

    [RelayCommand]
    public async Task RemoveImageAsync(DockerImageInfo? image)
    {
        if (image == null) return;
        var res = await _dockerService.RemoveImageAsync(image.Id, force: false);
        if (res.Success) await RefreshDockerAsync();
        else DockerStatusMessage = $"Failed to remove image: {res.ErrorOutput}";
    }

    #endregion

    #region Terminal Commands

    [RelayCommand]
    public void LaunchTerminal()
    {
        var request = new TerminalLaunchRequest
        {
            Type = SelectedTerminalType,
            WorkingDirectory = TerminalDirectory,
            AsAdministrator = TerminalAsAdmin
        };

        bool ok = _terminalService.LaunchTerminal(request);
        TerminalStatusMessage = ok ? $"Launched {SelectedTerminalType}." : "Failed to launch terminal.";
    }

    [RelayCommand]
    public void SetTerminalDirectoryToCurrent()
    {
        TerminalDirectory = Environment.CurrentDirectory;
    }

    [RelayCommand]
    public void SetTerminalDirectoryToGit()
    {
        if (!string.IsNullOrWhiteSpace(GitRepoPath))
        {
            TerminalDirectory = GitRepoPath;
        }
    }

    #endregion

    #region Tool Commands

    private void ExecuteInitialToolAction()
    {
        switch (SelectedTool)
        {
            case "Json": FormatJson(); break;
            case "Encoding": Base64Encode(); break;
            case "Hash": ComputeHash(); break;
            case "Uuid": GenerateUuid(); break;
            case "Timestamp": SetCurrentTimestamp(); break;
            case "Regex": TestRegex(); break;
            case "Qr": GenerateQr(); break;
        }
    }

    // JSON
    [RelayCommand]
    public void FormatJson()
    {
        var res = _jsonService.Format(JsonInput, indented: true);
        if (res.Success)
        {
            JsonOutput = res.FormattedText;
            JsonStatusMessage = "JSON formatted successfully.";
        }
        else
        {
            JsonOutput = string.Empty;
            JsonStatusMessage = $"Invalid JSON (Line {res.ErrorLine ?? 0}, Col {res.ErrorColumn ?? 0}): {res.ErrorMessage}";
        }
    }

    [RelayCommand]
    public void MinifyJson()
    {
        var res = _jsonService.Minify(JsonInput);
        if (res.Success)
        {
            JsonOutput = res.FormattedText;
            JsonStatusMessage = "JSON minified successfully.";
        }
        else
        {
            JsonOutput = string.Empty;
            JsonStatusMessage = $"Invalid JSON: {res.ErrorMessage}";
        }
    }

    [RelayCommand]
    public void ValidateJson()
    {
        var res = _jsonService.Validate(JsonInput);
        JsonStatusMessage = res.Success ? "JSON is syntactically valid." : $"Invalid JSON: {res.ErrorMessage}";
    }

    [RelayCommand]
    public void ClearJson()
    {
        JsonInput = string.Empty;
        JsonOutput = string.Empty;
        JsonStatusMessage = string.Empty;
    }

    // JWT
    [RelayCommand]
    public void DecodeJwt()
    {
        var res = _jwtService.Decode(JwtInput, string.IsNullOrWhiteSpace(JwtSecret) ? null : JwtSecret);
        IsJwtDecoded = res.IsDecoded;
        IsJwtVerified = res.IsVerified;
        JwtHeaderJson = res.HeaderJson;
        JwtPayloadJson = res.PayloadJson;
        JwtAlg = res.Algorithm;
        JwtStatusMessage = res.VerificationMessage ?? (res.IsDecoded ? "JWT decoded." : "Invalid JWT.");
    }

    [RelayCommand]
    public void ClearJwt()
    {
        JwtInput = string.Empty;
        JwtSecret = string.Empty;
        JwtHeaderJson = string.Empty;
        JwtPayloadJson = string.Empty;
        JwtAlg = string.Empty;
        IsJwtDecoded = false;
        IsJwtVerified = false;
        JwtStatusMessage = "Decoding a JWT does not verify its signature.";
    }

    // Encoding
    [RelayCommand]
    public void Base64Encode()
    {
        var res = _encodingService.Base64Encode(EncodingInput);
        if (res.Success)
        {
            EncodingOutput = res.OutputText;
            EncodingStatusMessage = "Base64 encoded.";
        }
        else EncodingStatusMessage = res.ErrorMessage;
    }

    [RelayCommand]
    public void Base64Decode()
    {
        var res = _encodingService.Base64Decode(EncodingInput);
        if (res.Success)
        {
            EncodingOutput = res.OutputText;
            EncodingStatusMessage = "Base64 decoded.";
        }
        else EncodingStatusMessage = res.ErrorMessage;
    }

    [RelayCommand]
    public void UrlEncode()
    {
        var res = _encodingService.UrlEncode(EncodingInput);
        if (res.Success)
        {
            EncodingOutput = res.OutputText;
            EncodingStatusMessage = "URL encoded.";
        }
        else EncodingStatusMessage = res.ErrorMessage;
    }

    [RelayCommand]
    public void UrlDecode()
    {
        var res = _encodingService.UrlDecode(EncodingInput);
        if (res.Success)
        {
            EncodingOutput = res.OutputText;
            EncodingStatusMessage = "URL decoded.";
        }
        else EncodingStatusMessage = res.ErrorMessage;
    }

    // Hash
    [RelayCommand]
    public void ComputeHash()
    {
        var res = _hashService.HashText(HashInput, SelectedHashAlgorithm);
        HashOutput = res.HexHash;
        HashStatusMessage = $"{SelectedHashAlgorithm} computed in {res.ElapsedMilliseconds} ms.";
    }

    // UUID
    [RelayCommand]
    public void GenerateUuid()
    {
        var options = new UuidOptions
        {
            Count = UuidCount,
            Uppercase = UuidUppercase,
            Hyphens = UuidHyphens,
            Braces = UuidBraces
        };
        var list = _uuidService.Generate(options);
        UuidOutput = string.Join(Environment.NewLine, list);
    }

    // Timestamp
    [RelayCommand]
    public void ConvertTimestamp()
    {
        if (long.TryParse(TimestampInput.Trim(), out long val))
        {
            // Auto detect seconds vs milliseconds (ms > 100_000_000_000)
            var res = val > 100_000_000_000
                ? _timestampService.ConvertFromUnixMilliseconds(val)
                : _timestampService.ConvertFromUnixSeconds(val);

            TimestampIsoOutput = res.FormattedIso;
            TimestampRelativeOutput = res.RelativeDescription;
            TimestampStatusMessage = $"Parsed as {(val > 100_000_000_000 ? "milliseconds" : "seconds")}.";
        }
        else
        {
            TimestampStatusMessage = "Invalid timestamp number.";
        }
    }

    [RelayCommand]
    public void SetCurrentTimestamp()
    {
        var current = _timestampService.GetCurrent();
        TimestampInput = current.UnixSeconds.ToString();
        TimestampIsoOutput = current.FormattedIso;
        TimestampRelativeOutput = current.RelativeDescription;
        TimestampStatusMessage = "Current timestamp set.";
    }

    // Regex
    [RelayCommand]
    public void TestRegex()
    {
        RegexOptions options = RegexOptions.None;
        if (RegexIgnoreCase) options |= RegexOptions.IgnoreCase;
        if (RegexMultiline) options |= RegexOptions.Multiline;
        if (RegexSingleline) options |= RegexOptions.Singleline;

        var res = _regexService.TestRegex(RegexPattern, RegexInput, options, RegexReplacement);
        if (res.Success)
        {
            RegexStatusMessage = $"Found {res.MatchCount} match(es) in {res.ExecutionTimeMs:0.##} ms.";
            var summary = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(res.ReplacementResult))
            {
                summary.AppendLine($"[Replacement Preview]");
                summary.AppendLine(res.ReplacementResult);
                summary.AppendLine();
            }
            summary.AppendLine($"[Matches ({res.MatchCount})]");
            foreach (var m in res.Matches)
            {
                summary.AppendLine($"• Index {m.Index}, Length {m.Length}: \"{m.Value}\"");
                if (m.Groups.Count > 1)
                {
                    for (int g = 1; g < m.Groups.Count; g++)
                    {
                        var grp = m.Groups[g];
                        summary.AppendLine($"    Group [{grp.Name}]: \"{grp.Value}\"");
                    }
                }
            }
            RegexOutput = summary.ToString();
        }
        else
        {
            RegexOutput = string.Empty;
            RegexStatusMessage = res.ErrorMessage;
        }
    }

    // QR
    [RelayCommand]
    public void GenerateQr()
    {
        QrGenerationResult res = QrType switch
        {
            "WiFi" => _qrService.GenerateWiFiQr(WiFiSsid, WiFiPassword, WiFiAuthType),
            _ => _qrService.GenerateQr(QrInput)
        };

        if (res.Success)
        {
            QrPngBytes = res.PngBytes;
            QrStatusMessage = "QR code generated.";
        }
        else
        {
            QrStatusMessage = res.ErrorMessage;
        }
    }

    #endregion

    #region HTTP Commands

    [RelayCommand]
    public async Task SendHttpRequestAsync()
    {
        IsHttpBusy = true;
        HttpResponseStatus = "Connecting...";
        HttpResponseMetrics = string.Empty;
        HttpResponseHeaders = string.Empty;
        HttpResponseBody = string.Empty;

        _httpCts?.Dispose();
        _httpCts = new CancellationTokenSource();

        try
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(HttpHeadersInput))
            {
                var lines = HttpHeadersInput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        string k = line[..colon].Trim();
                        string v = line[(colon + 1)..].Trim();
                        headers[k] = v;
                    }
                }
            }

            var req = new HttpRequestModel
            {
                Method = HttpMethod,
                Url = HttpUrl,
                Headers = headers,
                Body = HttpBodyInput,
                ContentType = HttpContentType,
                TimeoutSeconds = HttpTimeoutSeconds
            };

            var res = await _httpService.SendRequestAsync(req, _httpCts.Token);
            HttpResponseStatus = $"{res.StatusCode} {res.StatusText}";
            HttpResponseMetrics = $"{res.ElapsedMilliseconds} ms  •  {res.ContentLengthBytes} bytes";

            var sb = new System.Text.StringBuilder();
            foreach (var (k, v) in res.Headers)
            {
                sb.AppendLine($"{k}: {v}");
            }
            HttpResponseHeaders = sb.ToString();
            HttpResponseBody = string.IsNullOrWhiteSpace(res.FormattedBody) ? res.Body : res.FormattedBody;
        }
        catch (Exception ex)
        {
            HttpResponseStatus = "Error";
            HttpResponseBody = ex.Message;
        }
        finally
        {
            IsHttpBusy = false;
        }
    }

    [RelayCommand]
    public void CancelHttpRequest()
    {
        _httpCts?.Cancel();
    }

    #endregion

    #region Local Dev Commands

    [RelayCommand]
    public async Task RefreshPortsAsync()
    {
        IsPortsBusy = true;
        try
        {
            var ports = await _localDevService.GetListeningPortsAsync();
            _allPorts.Clear();
            _allPorts.AddRange(ports);
            ApplyPortFilter();
        }
        finally
        {
            IsPortsBusy = false;
        }
    }

    partial void OnPortSearchFilterChanged(string value)
    {
        ApplyPortFilter();
    }

    private void ApplyPortFilter()
    {
        FilteredPorts.Clear();
        string q = (PortSearchFilter ?? "").Trim().ToLowerInvariant();
        foreach (var p in _allPorts)
        {
            if (string.IsNullOrEmpty(q) ||
                p.Port.ToString().Contains(q) ||
                p.ProcessName.ToLowerInvariant().Contains(q) ||
                p.LocalAddress.Contains(q))
            {
                FilteredPorts.Add(p);
            }
        }
    }

    [RelayCommand]
    public void OpenPortUrl(ListeningPortInfo? port)
    {
        if (port == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = port.LocalhostUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Url}", port.LocalhostUrl);
        }
    }

    #endregion

    #region Process Commands

    [RelayCommand]
    public async Task RefreshProcessesAsync()
    {
        IsProcessesBusy = true;
        ProcessStatusMessage = "Querying processes...";
        try
        {
            var procs = await _processService.GetDeveloperProcessesAsync();
            _allProcesses.Clear();
            _allProcesses.AddRange(procs);
            ApplyProcessFilter();
            ProcessStatusMessage = $"{_allProcesses.Count} processes listed.";
        }
        catch (Exception ex)
        {
            ProcessStatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessesBusy = false;
        }
    }

    partial void OnProcessSearchFilterChanged(string value)
    {
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        FilteredProcesses.Clear();
        string q = (ProcessSearchFilter ?? "").Trim().ToLowerInvariant();
        foreach (var p in _allProcesses)
        {
            if (string.IsNullOrEmpty(q) ||
                p.ProcessName.ToLowerInvariant().Contains(q) ||
                p.ProcessId.ToString().Contains(q) ||
                p.PortsSummary.Contains(q))
            {
                FilteredProcesses.Add(p);
            }
        }
    }

    [RelayCommand]
    public async Task TerminateProcessAsync(DeveloperProcessInfo? proc)
    {
        if (proc == null) return;
        bool success = _processService.TerminateProcess(proc.ProcessId);
        if (success)
        {
            ProcessStatusMessage = $"Terminated process {proc.ProcessName} ({proc.ProcessId}).";
            await RefreshProcessesAsync();
        }
        else
        {
            ProcessStatusMessage = $"Failed to terminate process {proc.ProcessId} (Access Denied or System Protected).";
        }
    }

    [RelayCommand]
    public void OpenProcessLocation(DeveloperProcessInfo? proc)
    {
        if (proc == null || string.IsNullOrWhiteSpace(proc.MainModulePath) || !File.Exists(proc.MainModulePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{proc.MainModulePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open process location for {Path}", proc.MainModulePath);
        }
    }

    #endregion

    [RelayCommand]
    public void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }
}
