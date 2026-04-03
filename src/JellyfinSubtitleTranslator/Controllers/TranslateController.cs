using JellyfinSubtitleTranslator.Models;
using JellyfinSubtitleTranslator.Services;
using Microsoft.AspNetCore.Mvc;

namespace JellyfinSubtitleTranslator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslateController : ControllerBase
{
    private readonly ILogger<TranslateController> _logger;
    private readonly ISubtitleTranslationService _translationService;
    //private readonly IWebhookProcessingService? _webhookProcessingService;

    public TranslateController(
        ILogger<TranslateController> logger,
        ISubtitleTranslationService translationService)
    {
        _logger = logger;
        _translationService = translationService;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> TranslateManual([FromBody] TranslateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        _logger.LogInformation("Manual translation requested for: {Path}", request.Path);

        try
        {
            var subtitles = _translationService.DiscoverSubtitles(request.Path);

            if (subtitles.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "No subtitles found",
                    path = request.Path
                });
            }

            var result = await _translationService.TranslateSubtitlesAsync(subtitles, cancellationToken);

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                path = request.Path,
                translatedFiles = result.TranslatedFiles.Select(f => new
                {
                    source = f.SourcePath,
                    output = f.OutputPath,
                    entries = f.EntriesCount
                }),
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating {Path}", request.Path);
            return StatusCode(500, new { error = "Translation failed", message = ex.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> TranslateBatch([FromBody] BatchTranslateRequest request, CancellationToken cancellationToken)
    {
        if (request.Paths == null || request.Paths.Count == 0)
        {
            return BadRequest(new { error = "At least one path is required" });
        }

        _logger.LogInformation("Batch translation requested for {Count} items", request.Paths.Count);

        var results = new List<object>();

        foreach (var path in request.Paths)
        {
            try
            {
                var subtitles = _translationService.DiscoverSubtitles(path);
                if (subtitles.Count == 0)
                {
                    results.Add(new { path, success = false, message = "No subtitles found" });
                    continue;
                }

                var result = await _translationService.TranslateSubtitlesAsync(subtitles, cancellationToken);
                results.Add(new
                {
                    path,
                    success = result.Success,
                    message = result.Message,
                    translatedFiles = result.TranslatedFiles.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating {Path}", path);
                results.Add(new { path, success = false, message = ex.Message });
            }
        }

        return Ok(new
        {
            total = request.Paths.Count,
            results
        });
    }

    // [HttpPost]
    // public async Task<IActionResult> TranslateWebhook([FromBody] JellyfinWebhookPayload payload)
    // {
    //     if (_webhookProcessingService == null)
    //     {
    //         return BadRequest(new { error = "Webhook processing not enabled" });
    //     }

    //     _logger.LogInformation(
    //         "Webhook received - Type: {NotificationType}, Item: {ItemName}, Path: {ItemPath}, Tags: [{Tags}]",
    //         payload.NotificationType,
    //         payload.Item?.Name ?? "N/A",
    //         payload.Item?.Path ?? "N/A",
    //         payload.Item?.Tags != null ? string.Join(", ", payload.Item.Tags) : "none");

    //     if (!_webhookProcessingService.ShouldProcessEvent(payload))
    //     {
    //         return Ok(new { status = "skipped", reason = "Event type not supported" });
    //     }

    //     if (!_webhookProcessingService.HasTranslateTag(payload))
    //     {
    //         _logger.LogInformation("Skipped: no translate tag");
    //         return Ok(new { status = "skipped", reason = "no translate tag" });
    //     }

    //     if (string.IsNullOrEmpty(payload.Item?.Path))
    //     {
    //         _logger.LogWarning("Invalid payload: Item.Path is null or empty");
    //         return BadRequest(new { status = "error", reason = "Item.Path is required" });
    //     }

    //     var processed = await _webhookProcessingService.ProcessWebhookAsync(payload);

    //     if (processed)
    //     {
    //         return Ok(new { status = "processing", message = "Subtitle(s) enqueued for translation" });
    //     }

    //     return Ok(new { status = "completed", message = "No subtitles to translate" });
    // }

    [HttpGet("discover")]
    public IActionResult DiscoverSubtitles([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        var subtitles = _translationService.DiscoverSubtitles(path);

        return Ok(new
        {
            path,
            subtitlesFound = subtitles.Count,
            subtitles
        });
    }

    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}

public class TranslateRequest
{
    public string Path { get; set; } = string.Empty;
}

public class BatchTranslateRequest
{
    public List<string> Paths { get; set; } = new();
}
