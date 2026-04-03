using JellyfinSubtitleTranslator.Models;
using JellyfinSubtitleTranslator.Services;
using JellyfinSubtitleTranslator.Services.Jellyfin;
using Microsoft.AspNetCore.Mvc;

namespace JellyfinSubtitleTranslator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly ILogger<MediaController> _logger;
    private readonly IJellyfinService _jellyfinService;
    private readonly ISubtitleTranslationService _translationService;

    public MediaController(
        ILogger<MediaController> logger,
        IJellyfinService jellyfinService,
        ISubtitleTranslationService translationService)
    {
        _logger = logger;
        _jellyfinService = jellyfinService;
        _translationService = translationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMedia(
        [FromQuery] string? type = null,
        [FromQuery] string? libraryId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<MediaItemDto> items;

            if (!string.IsNullOrEmpty(libraryId))
            {
                var libraryItems = await _jellyfinService.GetLibraryItemsAsync(libraryId, cancellationToken);
                items = libraryItems.Select(MapToDto).ToList();
            }
            else if (string.Equals(type, "movies", StringComparison.OrdinalIgnoreCase))
            {
                var movies = await _jellyfinService.GetMoviesAsync(cancellationToken);
                items = movies.Select(MapToDto).ToList();
            }
            else if (string.Equals(type, "episodes", StringComparison.OrdinalIgnoreCase))
            {
                var episodes = await _jellyfinService.GetEpisodesAsync(cancellationToken);
                items = episodes.Select(MapToDto).ToList();
            }
            else
            {
                var movies = await _jellyfinService.GetMoviesAsync(cancellationToken);
                var episodes = await _jellyfinService.GetEpisodesAsync(cancellationToken);
                items = movies.Concat(episodes).Select(MapToDto).ToList();
            }

            foreach (var item in items)
            {
                item.SubtitlePaths = _translationService.DiscoverSubtitles(item.Path);
                item.HasSubtitles = item.SubtitlePaths.Count > 0;
            }

            return Ok(new { items, total = items.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching media");
            return StatusCode(500, new { error = "Failed to fetch media", message = ex.Message });
        }
    }

    [HttpGet("libraries")]
    public async Task<IActionResult> GetLibraries(CancellationToken cancellationToken = default)
    {
        try
        {
            var libraries = await _jellyfinService.GetLibrariesAsync(cancellationToken);
            var dtos = libraries.Select(l => new
            {
                id = l.ItemId,
                name = l.Name,
                type = l.CollectionType
            }).ToList();

            return Ok(new { libraries = dtos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching libraries");
            return StatusCode(500, new { error = "Failed to fetch libraries", message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMediaById(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var movies = await _jellyfinService.GetMoviesAsync(cancellationToken);
            var item = movies.FirstOrDefault(m => m.Id == id);

            if (item == null)
            {
                var episodes = await _jellyfinService.GetEpisodesAsync(cancellationToken);
                item = episodes.FirstOrDefault(e => e.Id == id);
            }

            if (item == null)
            {
                return NotFound(new { error = "Item not found" });
            }

            var dto = MapToDto(item);
            dto.SubtitlePaths = _translationService.DiscoverSubtitles(dto.Path);
            dto.HasSubtitles = dto.SubtitlePaths.Count > 0;

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching media item {Id}", id);
            return StatusCode(500, new { error = "Failed to fetch media item", message = ex.Message });
        }
    }

    private static MediaItemDto MapToDto(MediaItem item)
    {
        return new MediaItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Type = item.Type,
            Path = item.Path,
            Tags = item.Tags,
            Overview = item.Overview,
            ProductionYear = item.ProductionYear,
            PremiereDate = item.PremiereDate
        };
    }
}

public class MediaItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Overview { get; set; } = string.Empty;
    public int? ProductionYear { get; set; }
    public DateTime? PremiereDate { get; set; }
    public List<string> SubtitlePaths { get; set; } = new();
    public bool HasSubtitles { get; set; }
}
