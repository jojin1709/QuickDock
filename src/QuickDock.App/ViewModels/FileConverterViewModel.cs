using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using Windows.Storage.Pickers;

namespace QuickDock.App.ViewModels;

public sealed partial class FileConverterViewModel : ObservableObject
{
    private readonly IFileConversionService _conversionService;
    private readonly ILogger<FileConverterViewModel> _logger;

    [ObservableProperty]
    private string _sourceFilePath = string.Empty;

    [ObservableProperty]
    private string _fileName = "No file selected";

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private string _selectedTargetFormat = "PNG";

    [ObservableProperty]
    private ObservableCollection<string> _availableFormats = new();

    [ObservableProperty]
    private int _quality = 90;

    [ObservableProperty]
    private bool _isImageConversion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConverting))]
    private bool _isConverting;

    public bool IsNotConverting => !IsConverting;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusMessage = "Select a file to convert.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastOutputFile))]
    private string? _lastOutputFilePath;

    public bool HasLastOutputFile => !string.IsNullOrEmpty(LastOutputFilePath);

    public FileConverterViewModel(
        IFileConversionService conversionService,
        ILogger<FileConverterViewModel> logger)
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        AvailableFormats.Add("PNG");
        AvailableFormats.Add("JPEG");
        AvailableFormats.Add("BMP");
        AvailableFormats.Add("GIF");
        AvailableFormats.Add("ZIP");
        AvailableFormats.Add("UTF-8");
        AvailableFormats.Add("UTF-16");
        AvailableFormats.Add("ASCII");
    }

    public void SetSourceFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return;
        }

        SourceFilePath = Path.GetFullPath(path);
        FileName = Path.GetFileName(SourceFilePath);
        var isDir = Directory.Exists(SourceFilePath);

        if (!isDir && File.Exists(SourceFilePath))
        {
            var fi = new FileInfo(SourceFilePath);
            FormattedSize = FileShelfItem.FormatBytes(fi.Length);
            var ext = fi.Extension.ToLowerInvariant();
            IsImageConversion = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

            if (IsImageConversion)
            {
                SelectedTargetFormat = ext is ".png" ? "JPEG" : "PNG";
            }
            else
            {
                SelectedTargetFormat = "ZIP";
            }
        }
        else
        {
            FormattedSize = "Folder";
            IsImageConversion = false;
            SelectedTargetFormat = "ZIP";
        }

        StatusMessage = "Ready to convert.";
    }

    [RelayCommand]
    public async Task ConvertAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        IsConverting = true;
        Progress = 0;
        StatusMessage = "Converting...";
        LastOutputFilePath = null;

        try
        {
            var format = SelectedTargetFormat.ToUpperInvariant();
            string outputResult;

            if (format is "PNG" or "JPEG" or "BMP" or "GIF")
            {
                outputResult = await _conversionService.ConvertImageAsync(SourceFilePath, format.ToLowerInvariant(), Quality);
            }
            else if (format == "ZIP")
            {
                var targetZip = Path.Combine(
                    Path.GetDirectoryName(SourceFilePath) ?? AppContext.BaseDirectory,
                    $"{Path.GetFileNameWithoutExtension(SourceFilePath)}.zip");

                var progressReporter = new Progress<double>(p => Progress = p);
                outputResult = await _conversionService.CreateZipArchiveAsync(new[] { SourceFilePath }, targetZip, progressReporter);
            }
            else if (format is "UTF-8" or "UTF-16" or "ASCII")
            {
                Encoding enc = format switch
                {
                    "UTF-16" => Encoding.Unicode,
                    "ASCII" => Encoding.ASCII,
                    _ => Encoding.UTF8
                };
                outputResult = await _conversionService.ConvertTextEncodingAsync(SourceFilePath, enc);
            }
            else
            {
                throw new NotSupportedException($"Format {format} is not supported.");
            }

            LastOutputFilePath = outputResult;
            Progress = 100;
            StatusMessage = $"Successfully created: {Path.GetFileName(outputResult)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion failed");
            StatusMessage = $"Conversion failed: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
        }
    }
}
