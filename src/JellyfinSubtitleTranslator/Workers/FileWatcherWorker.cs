using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Services;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Workers;

public class FileWatcherWorker : BackgroundService
{
    private readonly ILogger<FileWatcherWorker> _logger;
    private readonly TranslatorOptions _options;
    private readonly IFileQueue _fileQueue;
    private readonly ILanguageMapper _languageMapper;
    private FileSystemWatcher? _watcher;

    public FileWatcherWorker(
        ILogger<FileWatcherWorker> logger,
        IOptions<TranslatorOptions> options,
        IFileQueue fileQueue,
        ILanguageMapper languageMapper)
    {
        _logger = logger;
        _options = options.Value;
        _fileQueue = fileQueue;
        _languageMapper = languageMapper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_options.MediaPath))
        {
            _logger.LogError("Media path does not exist: {MediaPath}", _options.MediaPath);
            return;
        }

        _logger.LogInformation("Starting file watcher on: {MediaPath}", _options.MediaPath);

        _watcher = new FileSystemWatcher(_options.MediaPath)
        {
            Filter = "*.srt",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
        _watcher.EnableRaisingEvents = true;

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        await ProcessFileAsync(e.FullPath);
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        await ProcessFileAsync(e.FullPath);
    }

    private async Task ProcessFileAsync(string filePath)
    {
        try
        {
            if (!ShouldProcessFile(filePath))
            {
                return;
            }

            _logger.LogInformation("File detected: {FilePath}", filePath);

            await Task.Delay(_options.FileReadyDelayMs);

            if (!IsFileReady(filePath))
            {
                _logger.LogWarning("File not ready yet, waiting: {FilePath}", filePath);
                await WaitForFileReady(filePath);
            }

            await _fileQueue.EnqueueAsync(filePath);
            _logger.LogInformation("File enqueued for processing: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
        }
    }

    private bool ShouldProcessFile(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        if (!extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var targetLang = _options.TargetLanguage;
        var targetLangIso6391 = _languageMapper.ToIso6391(targetLang);

        if (fileName.EndsWith($".{targetLang}", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith($".{targetLangIso6391}", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Skipping already translated file: {FilePath}", filePath);
            return false;
        }

        return true;
    }

    private static bool IsFileReady(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private async Task WaitForFileReady(string filePath, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            if (IsFileReady(filePath))
            {
                return;
            }
            await Task.Delay(1000);
        }
        _logger.LogWarning("File may not be fully written: {FilePath}", filePath);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _logger.LogInformation("File watcher stopped");
        await base.StopAsync(cancellationToken);
    }
}
