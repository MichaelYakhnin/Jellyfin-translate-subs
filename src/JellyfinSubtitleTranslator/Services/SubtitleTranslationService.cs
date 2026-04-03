using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Models;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services;

public interface ISubtitleTranslationService
{
    Task<TranslationResult> TranslateSubtitlesAsync(string mediaPath, CancellationToken cancellationToken = default);
    Task<TranslationResult> TranslateSubtitlesAsync(IEnumerable<string> subtitlePaths, CancellationToken cancellationToken = default);
    List<string> DiscoverSubtitles(string mediaPath);
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

    public SubtitleTranslationService(
        ILogger<SubtitleTranslationService> logger,
        IOptions<TranslatorOptions> options,
        ISrtParser srtParser,
        ITranslationClient translationClient,
        ILanguageMapper languageMapper)
    {
        _logger = logger;
        _options = options.Value;
        _srtParser = srtParser;
        _translationClient = translationClient;
        _languageMapper = languageMapper;
    }

    public List<string> DiscoverSubtitles(string mediaPath)
    {
        var subtitles = new List<string>();

        if (string.IsNullOrEmpty(mediaPath))
            return subtitles;

        var mediaDirectory = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrEmpty(mediaDirectory) || !Directory.Exists(mediaDirectory))
        {
            _logger.LogWarning("Media directory not found: {MediaDirectory}", mediaDirectory);
            return subtitles;
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(mediaPath);
        var targetLangIso6391 = _languageMapper.ToIso6391(_options.TargetLanguage);
        var targetLangIso6392 = _languageMapper.ToIso6392(_options.TargetLanguage);

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

    public async Task<TranslationResult> TranslateSubtitlesAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        var subtitles = DiscoverSubtitles(mediaPath);

        if (subtitles.Count == 0)
        {
            return new TranslationResult
            {
                Success = false,
                Message = "No subtitles found for this media"
            };
        }

        return await TranslateSubtitlesAsync(subtitles, cancellationToken);
    }

    public async Task<TranslationResult> TranslateSubtitlesAsync(IEnumerable<string> subtitlePaths, CancellationToken cancellationToken = default)
    {
        var result = new TranslationResult();
        var paths = subtitlePaths.ToList();

        _logger.LogInformation("Starting translation for {Count} subtitle files", paths.Count);

        foreach (var subtitlePath in paths)
        {
            try
            {
                var outputPath = GetOutputPath(subtitlePath);

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

                var content = await File.ReadAllTextAsync(subtitlePath, cancellationToken);
                var entries = _srtParser.Parse(content);

                if (entries.Count == 0)
                {
                    _logger.LogWarning("No subtitle entries found in {FilePath}", subtitlePath);
                    continue;
                }

                var translatedEntries = await TranslateEntriesAsync(entries, cancellationToken);
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

    private async Task<List<SubtitleEntry>> TranslateEntriesAsync(List<SubtitleEntry> entries, CancellationToken cancellationToken)
    {
        var sourceLanguage = _options.SourceLanguage;

        if (string.IsNullOrEmpty(sourceLanguage))
        {
            var sampleText = string.Join(" ", entries.Take(5).Select(e => e.Text));
            sourceLanguage = await _translationClient.DetectLanguageAsync(sampleText, cancellationToken);
        }

        var targetLanguageIso6391 = _languageMapper.ToIso6391(_options.TargetLanguage);
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
                targetLanguageIso6391,
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

        return translatedEntries;
    }

    private string GetOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        var targetLang = _languageMapper.ToIso6392(_options.TargetLanguage);
        return Path.Combine(directory, $"{fileName}.{targetLang}{extension}");
    }
}
