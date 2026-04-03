using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Models;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services;

public interface IWebhookProcessingService
{
    Task<bool> ProcessWebhookAsync(JellyfinWebhookPayload payload, CancellationToken cancellationToken = default);
    bool ShouldProcessEvent(JellyfinWebhookPayload payload);
    bool HasTranslateTag(JellyfinWebhookPayload payload);
}

public class WebhookProcessingService : IWebhookProcessingService
{
    private readonly ILogger<WebhookProcessingService> _logger;
    private readonly TranslatorOptions _options;
    private readonly ISubtitleDiscoveryService _subtitleDiscovery;
    private readonly ISrtParser _srtParser;
    private readonly ITranslationClient _translationClient;
    private readonly ILanguageMapper _languageMapper;
    private readonly IFileQueue _fileQueue;

    private static readonly HashSet<string> ProcessedItemIds = new();
    private static readonly object ProcessedItemsLock = new();

    public WebhookProcessingService(
        ILogger<WebhookProcessingService> logger,
        IOptions<TranslatorOptions> options,
        ISubtitleDiscoveryService subtitleDiscovery,
        ISrtParser srtParser,
        ITranslationClient translationClient,
        ILanguageMapper languageMapper,
        IFileQueue fileQueue)
    {
        _logger = logger;
        _options = options.Value;
        _subtitleDiscovery = subtitleDiscovery;
        _srtParser = srtParser;
        _translationClient = translationClient;
        _languageMapper = languageMapper;
        _fileQueue = fileQueue;
    }

    public bool ShouldProcessEvent(JellyfinWebhookPayload payload)
    {
        if (string.IsNullOrEmpty(payload.NotificationType))
        {
            _logger.LogWarning("Webhook received with empty NotificationType");
            return false;
        }

        var eventType = payload.NotificationType.Trim();
        
        if (!eventType.Equals("ItemUpdated", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping event type: {EventType}", eventType);
            return false;
        }

        return true;
    }

    public bool HasTranslateTag(JellyfinWebhookPayload payload)
    {
        if (payload.Item?.Tags == null || payload.Item.Tags.Count == 0)
        {
            _logger.LogInformation("No tags found in payload");
            return false;
        }

        var normalizedTags = payload.Item.Tags
            .Select(t => t.Trim().ToLowerInvariant())
            .ToList();

        var hasTranslateTag = normalizedTags.Contains("translate");

        _logger.LogInformation(
            "Tags: [{Tags}] - Translate tag: {HasTranslateTag}",
            string.Join(", ", normalizedTags),
            hasTranslateTag);

        return hasTranslateTag;
    }

    public async Task<bool> ProcessWebhookAsync(JellyfinWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        var itemId = payload.Item?.Id ?? Guid.NewGuid().ToString();

        lock (ProcessedItemsLock)
        {
            if (ProcessedItemIds.Contains(itemId))
            {
                _logger.LogInformation("Item {ItemId} already processed, skipping", itemId);
                return false;
            }
        }

        var mediaPath = payload.Item?.Path;
        if (string.IsNullOrEmpty(mediaPath))
        {
            _logger.LogWarning("Invalid payload: Item.Path is null or empty");
            return false;
        }

        _logger.LogInformation("Processing media path: {MediaPath}", mediaPath);

        await Task.Delay(_options.WebhookProcessingDelayMs, cancellationToken);

        var subtitles = _subtitleDiscovery.DiscoverSubtitles(mediaPath);

        if (subtitles.Count == 0)
        {
            _logger.LogInformation("No subtitles found for: {MediaPath}", mediaPath);
            lock (ProcessedItemsLock)
            {
                ProcessedItemIds.Add(itemId);
            }
            return false;
        }

        foreach (var subtitlePath in subtitles)
        {
            await _fileQueue.EnqueueAsync(subtitlePath);
            _logger.LogInformation("Enqueued subtitle for translation: {SubtitlePath}", subtitlePath);
        }

        lock (ProcessedItemsLock)
        {
            ProcessedItemIds.Add(itemId);
        }

        return true;
    }
}
