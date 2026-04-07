using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Models;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services;

public interface ISubtitleTranslationService
{
    Task<TranslationResult> TranslateSubtitlesAsync(string mediaPath, string? targetLanguage = null, CancellationToken cancellationToken = default);
    Task<TranslationResult> TranslateSubtitlesAsync(IEnumerable<string> subtitlePaths, string? targetLanguage = null, CancellationToken cancellationToken = default);
    List<string> DiscoverSubtitles(string mediaPath, string? targetLanguage = null);
}

public class TranslationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<TranslatedFile> TranslatedFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class TranslatedFile
{
    public string SourcePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int EntriesCount { get; set; }
}

public class SubtitleTranslationService : ISubtitleTranslationService
{
    private readonly ILogger<SubtitleTranslationService> _logger;
    private readonly TranslatorOptions _options;
    private readonly ISrtParser _srtParser;
    private readonly ITranslationClient _translationClient;
    private readonly ILanguageMapper _languageMapper;
    private readonly IPathMapper _pathMapper;

    public SubtitleTranslationService(
        ILogger<SubtitleTranslationService> logger,
        IOptions<TranslatorOptions> options,
        ISrtParser srtParser,
        ITranslationClient translationClient,
        ILanguageMapper languageMapper,
        IPathMapper pathMapper)
    {
        _logger = logger;
        _options = options.Value;
        _srtParser = srtParser;
        _translationClient = translationClient;
        _languageMapper = languageMapper;
        _pathMapper = pathMapper;
    }

    public List<string> DiscoverSubtitles(string mediaPath, string? targetLanguage = null)
    {
        var subtitles = new List<string>();

        if (string.IsNullOrEmpty(mediaPath))
            return subtitles;

        var mappedPath = _pathMapper.MapToDockerPath(mediaPath);
        var mediaDirectory = Path.GetDirectoryName(mappedPath);

        if (string.IsNullOrEmpty(mediaDirectory) || !Directory.Exists(mediaDirectory))
        {
            _logger.LogWarning("Media directory not found: {MediaDirectory}", mediaDirectory);
            return subtitles;
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(mediaPath);
        var lang = targetLanguage ?? _options.TargetLanguage;
        var targetLangIso6391 = _languageMapper.ToIso6391(lang);
        var targetLangIso6392 = _languageMapper.ToIso6392(lang);

        var srtFiles = Directory.GetFiles(mediaDirectory, "*.srt");

        foreach (var srtFile in srtFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(srtFile);

            if (fileName.EndsWith($".{targetLangIso6391}", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith($".{targetLangIso6392}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!fileName.StartsWith(mediaFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            subtitles.Add(srtFile);
        }

        return subtitles;
    }

    public async Task<TranslationResult> TranslateSubtitlesAsync(string mediaPath, string? targetLanguage = null, CancellationToken cancellationToken = default)
    {
        var subtitles = DiscoverSubtitles(mediaPath, targetLanguage);

        if (subtitles.Count == 0)
        {
            return new TranslationResult
            {
                Success = false,
                Message = "No subtitles found for this media"
            };
        }

        return await TranslateSubtitlesAsync(subtitles, targetLanguage, cancellationToken);
    }

    public async Task<TranslationResult> TranslateSubtitlesAsync(IEnumerable<string> subtitlePaths, string? targetLanguage = null, CancellationToken cancellationToken = default)
    {
        var result = new TranslationResult();
        var paths = subtitlePaths.Select(p => _pathMapper.MapToDockerPath(p)).ToList();
        var lang = targetLanguage ?? _options.TargetLanguage;
        var useFileTranslation = string.Equals(_options.TranslationMode, "file", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("Starting translation for {Count} subtitle files to {TargetLang} (Mode: {Mode})", paths.Count, lang, _options.TranslationMode);

        foreach (var subtitlePath in paths)
        {
            try
            {
                var outputPath = GetOutputPath(subtitlePath, _languageMapper.ToIso6392(lang));

                if (File.Exists(outputPath))
                {
                    _logger.LogInformation("Output file already exists, skipping: {OutputPath}", outputPath);
                    result.TranslatedFiles.Add(new TranslatedFile
                    {
                        SourcePath = subtitlePath,
                        OutputPath = outputPath,
                        EntriesCount = 0
                    });
                    continue;
                }

                if (useFileTranslation)
                {
                    await TranslateUsingFileAsync(subtitlePath, lang, outputPath, result, cancellationToken);
                }
                else
                {
                    await TranslateUsingBatchAsync(subtitlePath, lang, outputPath, result, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating {FilePath}", subtitlePath);
                result.Errors.Add($"{subtitlePath}: {ex.Message}");
            }
        }

        result.Success = result.Errors.Count == 0;
        result.Message = result.TranslatedFiles.Count > 0
            ? $"Translated {result.TranslatedFiles.Count} file(s)"
            : "No files were translated";

        return result;
    }

    private async Task TranslateUsingFileAsync(string subtitlePath, string targetLanguage, string outputPath, TranslationResult result, CancellationToken cancellationToken)
    {
        var targetLangIso6391 = _languageMapper.ToIso6391(targetLanguage);
        var sourceLanguage = string.IsNullOrEmpty(_options.SourceLanguage) ? "auto" : _languageMapper.ToIso6391(_options.SourceLanguage);

        _logger.LogInformation("Translating file using TranslateFileAsync: {FilePath}", subtitlePath);

        var translatedContent = await _translationClient.TranslateFileAsync(
            subtitlePath,
            sourceLanguage,
            targetLangIso6391,
            cancellationToken);

        await File.WriteAllTextAsync(outputPath, translatedContent, System.Text.Encoding.UTF8, cancellationToken);

        result.TranslatedFiles.Add(new TranslatedFile
        {
            SourcePath = subtitlePath,
            OutputPath = outputPath,
            EntriesCount = 0
        });

        _logger.LogInformation("Translated file: {OutputPath}", outputPath);
    }

    private async Task TranslateUsingBatchAsync(string subtitlePath, string targetLanguage, string outputPath, TranslationResult result, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(subtitlePath, cancellationToken);
        var entries = _srtParser.Parse(content);

        if (entries.Count == 0)
        {
            _logger.LogWarning("No subtitle entries found in {FilePath}", subtitlePath);
            return;
        }

        var targetLangIso6391 = _languageMapper.ToIso6391(targetLanguage);
        var sourceLanguage = _options.SourceLanguage;

        if (string.IsNullOrEmpty(sourceLanguage))
        {
            var sampleText = string.Join(" ", entries.Take(5).Select(e => e.Text));
            sourceLanguage = await _translationClient.DetectLanguageAsync(sampleText, cancellationToken);
            _logger.LogInformation("Detected source language: {SourceLanguage}", sourceLanguage);
        }
        else
        {
            sourceLanguage = _languageMapper.ToIso6391(sourceLanguage);
        }

        var translatedEntries = new List<SubtitleEntry>();

        var batches = entries
            .Select((entry, index) => new { entry, index })
            .GroupBy(x => x.index / _options.MaxBatchSize)
            .Select(g => g.Select(x => x.entry).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var textsToTranslate = batch
                .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                .Select(e => e.Text)
                .ToList();

            if (textsToTranslate.Count == 0)
            {
                translatedEntries.AddRange(batch);
                continue;
            }

            var combinedText = string.Join("\n", textsToTranslate);
            var translatedText = await _translationClient.TranslateAsync(
                combinedText,
                sourceLanguage,
                targetLangIso6391,
                cancellationToken);

            var translatedLines = translatedText.Split('\n');

            int translatedIndex = 0;
            foreach (var entry in batch)
            {
                if (string.IsNullOrWhiteSpace(entry.Text))
                {
                    translatedEntries.Add(entry);
                }
                else if (translatedIndex < translatedLines.Length)
                {
                    translatedEntries.Add(new SubtitleEntry
                    {
                        Index = entry.Index,
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime,
                        Lines = translatedLines[translatedIndex].Split('\n').ToList()
                    });
                    translatedIndex++;
                }
                else
                {
                    translatedEntries.Add(entry);
                }
            }
        }

        var outputContent = _srtParser.Serialize(translatedEntries);
        await File.WriteAllTextAsync(outputPath, outputContent, System.Text.Encoding.UTF8, cancellationToken);

        result.TranslatedFiles.Add(new TranslatedFile
        {
            SourcePath = subtitlePath,
            OutputPath = outputPath,
            EntriesCount = translatedEntries.Count
        });

        _logger.LogInformation("Translated {Entries} entries: {OutputPath}", translatedEntries.Count, outputPath);
    }

    private string GetOutputPath(string inputPath, string targetLangIso6392)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        return Path.Combine(directory, $"{fileName}.{targetLangIso6392}{extension}");
    }
}
