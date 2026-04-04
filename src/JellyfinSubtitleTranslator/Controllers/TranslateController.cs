using JellyfinSubtitleTranslator.Models;
using JellyfinSubtitleTranslator.Services;
using Microsoft.AspNetCore.Mvc;
using SysFile = System.IO.File;

namespace JellyfinSubtitleTranslator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslateController : ControllerBase
{
    private readonly ILogger<TranslateController> _logger;
    private readonly ISubtitleTranslationService _translationService;

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
        if (string.IsNullOrEmpty(request.MediaPath))
        {
            return BadRequest(new { error = "MediaPath is required" });
        }

        _logger.LogInformation("Manual translation requested for: {Path}, TargetLang: {TargetLang}, Subtitle: {SubtitlePath}", 
            request.MediaPath, request.TargetLanguage ?? "default", request.SubtitlePath ?? "auto");

        try
        {
            List<string> subtitlePaths;

            if (!string.IsNullOrEmpty(request.SubtitlePath))
            {
                if (!SysFile.Exists(request.SubtitlePath))
                {
                    return BadRequest(new { error = "Subtitle file not found", path = request.SubtitlePath });
                }
                subtitlePaths = new List<string> { request.SubtitlePath };
            }
            else
            {
                subtitlePaths = _translationService.DiscoverSubtitles(request.MediaPath);
            }

            if (subtitlePaths.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "No subtitles found",
                    path = request.MediaPath
                });
            }

            var result = await _translationService.TranslateSubtitlesAsync(
                subtitlePaths, 
                request.TargetLanguage, 
                cancellationToken);

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                path = request.MediaPath,
                targetLanguage = request.TargetLanguage ?? "default",
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
            _logger.LogError(ex, "Error translating {Path}", request.MediaPath);
            return StatusCode(500, new { error = "Translation failed", message = ex.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> TranslateBatch([FromBody] BatchTranslateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { error = "At least one item is required" });
        }

        _logger.LogInformation("Batch translation requested for {Count} items, TargetLang: {TargetLang}", 
            request.Items.Count, request.TargetLanguage ?? "default");

        var results = new List<object>();

        foreach (var item in request.Items)
        {
            try
            {
                List<string> subtitlePaths;

                if (!string.IsNullOrEmpty(item.SubtitlePath))
                {
                    if (!SysFile.Exists(item.SubtitlePath))
                    {
                        results.Add(new { mediaPath = item.MediaPath, subtitlePath = item.SubtitlePath, success = false, message = "Subtitle file not found" });
                        continue;
                    }
                    subtitlePaths = new List<string> { item.SubtitlePath };
                }
                else
                {
                    subtitlePaths = _translationService.DiscoverSubtitles(item.MediaPath);
                }

                if (subtitlePaths.Count == 0)
                {
                    results.Add(new { mediaPath = item.MediaPath, success = false, message = "No subtitles found" });
                    continue;
                }

                var result = await _translationService.TranslateSubtitlesAsync(
                    subtitlePaths, 
                    item.TargetLanguage ?? request.TargetLanguage, 
                    cancellationToken);

                results.Add(new
                {
                    mediaPath = item.MediaPath,
                    success = result.Success,
                    message = result.Message,
                    translatedFiles = result.TranslatedFiles.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating {Path}", item.MediaPath);
                results.Add(new { mediaPath = item.MediaPath, success = false, message = ex.Message });
            }
        }

        return Ok(new
        {
            total = request.Items.Count,
            targetLanguage = request.TargetLanguage ?? "default",
            results
        });
    }

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

    [HttpGet("languages")]
    public IActionResult GetSupportedLanguages()
    {
        return Ok(new
        {
            languages = new[]
            {
                new { code = "rus", name = "Russian" },
                new { code = "heb", name = "Hebrew" }
            }
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
    public string MediaPath { get; set; } = string.Empty;
    public string? SubtitlePath { get; set; }
    public string? TargetLanguage { get; set; }
}

public class BatchTranslateRequest
{
    public List<BatchTranslateItem> Items { get; set; } = new();
    public string? TargetLanguage { get; set; }
}

public class BatchTranslateItem
{
    public string MediaPath { get; set; } = string.Empty;
    public string? SubtitlePath { get; set; }
    public string? TargetLanguage { get; set; }
}
