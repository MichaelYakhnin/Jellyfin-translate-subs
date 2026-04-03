using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Services;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Workers;

public class TranslationWorker : BackgroundService
{
    private readonly ILogger<TranslationWorker> _logger;
    private readonly TranslatorOptions _options;
    private readonly IFileQueue _fileQueue;
    private readonly ISrtParser _srtParser;
    private readonly ITranslationClient _translationClient;
    private readonly ILanguageMapper _languageMapper;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public TranslationWorker(
        ILogger<TranslationWorker> logger,
        IOptions<TranslatorOptions> options,
        IFileQueue fileQueue,
        ISrtParser srtParser,
        ITranslationClient translationClient,
        ILanguageMapper languageMapper)
    {
        _logger = logger;
        _options = options.Value;
        _fileQueue = fileQueue;
        _srtParser = srtParser;
        _translationClient = translationClient;
        _languageMapper = languageMapper;
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Translation worker started with {MaxConcurrency} concurrent workers", _options.MaxConcurrency);

        var tasks = new List<Task>();

        for (int i = 0; i < _options.MaxConcurrency; i++)
        {
            var workerId = i;
            tasks.Add(ProcessQueueAsync(workerId, stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessQueueAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        await foreach (var filePath in _fileQueue.DequeueAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            try
            {
                await ProcessFileAsync(filePath, workerId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId}: Error processing file {FilePath}", workerId, filePath);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessFileAsync(string filePath, int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {WorkerId}: Processing file: {FilePath}", workerId, filePath);

        var entries = _srtParser.Parse(await File.ReadAllTextAsync(filePath, cancellationToken));

        if (entries.Count == 0)
        {
            _logger.LogWarning("Worker {WorkerId}: No subtitle entries found in {FilePath}", workerId, filePath);
            return;
        }

        var sourceLanguage = string.IsNullOrEmpty(_options.SourceLanguage)
            ? "auto"
            : _languageMapper.ToIso6391(_options.SourceLanguage);

        var targetLanguage = _languageMapper.ToIso6391(_options.TargetLanguage);

        if (string.IsNullOrEmpty(_options.SourceLanguage))
        {
            var sampleText = string.Join(" ", entries.Take(5).Select(e => e.Text));
            sourceLanguage = await _translationClient.DetectLanguageAsync(sampleText, cancellationToken);
            _logger.LogInformation("Worker {WorkerId}: Detected source language: {SourceLanguage}", workerId, sourceLanguage);
        }

        var outputPath = GetOutputPath(filePath, sourceLanguage);

        if (File.Exists(outputPath))
        {
            _logger.LogInformation("Worker {WorkerId}: Output file already exists, skipping: {OutputPath}", workerId, outputPath);
            return;
        }

        _logger.LogInformation("Worker {WorkerId}: Translating file using file upload method", workerId);

        var translatedContent = await _translationClient.TranslateFileAsync(
            filePath,
            sourceLanguage,
            targetLanguage,
            cancellationToken);

        await File.WriteAllTextAsync(outputPath, translatedContent, System.Text.Encoding.UTF8, cancellationToken);

        _logger.LogInformation(
            "Worker {WorkerId}: Translation completed. Output: {OutputPath}",
            workerId,
            outputPath);
    }

    private string GetOutputPath(string inputPath, string? detectedSourceLang = null)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        var targetLang = _languageMapper.ToIso6392(_options.TargetLanguage);

        if (!string.IsNullOrEmpty(detectedSourceLang))
        {
            var sourceLangIso6392 = _languageMapper.ToIso6392(detectedSourceLang);
            var sourceLangIso6391 = _languageMapper.ToIso6391(detectedSourceLang);
            
            if (fileName.EndsWith($".{sourceLangIso6392}", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^($".{sourceLangIso6392}".Length)];
            }
            else if (fileName.EndsWith($".{sourceLangIso6391}", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^($".{sourceLangIso6391}".Length)];
            }
        }

        return Path.Combine(directory, $"{fileName}.{targetLang}{extension}");
    }
}
