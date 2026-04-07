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
        var targetLangIso6391 = _languageMapper.ToIso6391(lang);
        var sourceLanguage = string.IsNullOrEmpty(_options.SourceLanguage) ? "auto" : _languageMapper.ToIso6391(_options.SourceLanguage);

        _logger.LogInformation("Starting translation for {Count} subtitle files to {TargetLang}", paths.Count, lang);

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

    private string GetOutputPath(string inputPath, string targetLangIso6392)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        return Path.Combine(directory, $"{fileName}.{targetLangIso6392}{extension}");
    }
}
