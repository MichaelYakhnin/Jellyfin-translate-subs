using JellyfinSubtitleTranslator.Configuration;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services;

public interface ISubtitleDiscoveryService
{
    List<string> DiscoverSubtitles(string mediaFilePath);
}

public class SubtitleDiscoveryService : ISubtitleDiscoveryService
{
    private readonly ILogger<SubtitleDiscoveryService> _logger;
    private readonly TranslatorOptions _options;
    private readonly ILanguageMapper _languageMapper;

    public SubtitleDiscoveryService(
        ILogger<SubtitleDiscoveryService> logger,
        IOptions<TranslatorOptions> options,
        ILanguageMapper languageMapper)
    {
        _logger = logger;
        _options = options.Value;
        _languageMapper = languageMapper;
    }

    public List<string> DiscoverSubtitles(string mediaFilePath)
    {
        var subtitles = new List<string>();

        if (string.IsNullOrEmpty(mediaFilePath))
        {
            _logger.LogWarning("Media file path is empty");
            return subtitles;
        }

        var mediaDirectory = Path.GetDirectoryName(mediaFilePath);
        if (string.IsNullOrEmpty(mediaDirectory) || !Directory.Exists(mediaDirectory))
        {
            _logger.LogWarning("Media directory not found: {MediaDirectory}", mediaDirectory);
            return subtitles;
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(mediaFilePath);
        var targetLangIso6391 = _languageMapper.ToIso6391(_options.TargetLanguage);
        var targetLangIso6392 = _languageMapper.ToIso6392(_options.TargetLanguage);

        var srtFiles = Directory.GetFiles(mediaDirectory, "*.srt");

        foreach (var srtFile in srtFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(srtFile);
            _logger.LogInformation("Checking subtitle file: {FilePath}", srtFile);
            if (fileName.EndsWith($".{targetLangIso6391}", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith($".{targetLangIso6392}", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping already translated file: {FilePath}", srtFile);
                continue;
            }

            // if (!fileName.StartsWith(mediaFileName, StringComparison.OrdinalIgnoreCase))
            // {
            //     _logger.LogDebug("Skipping unrelated subtitle: {FilePath}", srtFile);
            //     continue;
            // }

            subtitles.Add(srtFile);
        }

        _logger.LogInformation("Discovered {Count} subtitle files in {Directory}", subtitles.Count, mediaDirectory);
        return subtitles;
    }
}
