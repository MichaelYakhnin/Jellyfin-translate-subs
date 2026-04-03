using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using JellyfinSubtitleTranslator.Configuration;
using Microsoft.Extensions.Options;

namespace JellyfinSubtitleTranslator.Services.Jellyfin;

public interface IJellyfinService
{
    Task<List<MediaItem>> GetMoviesAsync(CancellationToken cancellationToken = default);
    Task<List<MediaItem>> GetEpisodesAsync(CancellationToken cancellationToken = default);
    Task<List<MediaItem>> GetLibraryItemsAsync(string libraryId, CancellationToken cancellationToken = default);
    Task<List<Library>> GetLibrariesAsync(CancellationToken cancellationToken = default);
}

public class JellyfinService : IJellyfinService
{
    private readonly HttpClient _httpClient;
    private readonly JellyfinOptions _options;
    private readonly ILogger<JellyfinService> _logger;
    private readonly string _apiKey;

    public JellyfinService(
        HttpClient httpClient,
        IOptions<TranslatorOptions> options,
        ILogger<JellyfinService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.Jellyfin;
        _logger = logger;
        _apiKey = _options.ApiKey;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string AddApiKey(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}api_key={_apiKey}";
    }

    public async Task<List<MediaItem>> GetMoviesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = await GetLibrariesAsync(cancellationToken);
        var movies = new List<MediaItem>();

        foreach (var library in libraries.Where(l => l.CollectionType == "movies"))
        {
            var items = await GetLibraryItemsAsync(library.ItemId, cancellationToken);
            movies.AddRange(items);
        }

        return movies;
    }

    public async Task<List<MediaItem>> GetEpisodesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = await GetLibrariesAsync(cancellationToken);
        var episodes = new List<MediaItem>();

        foreach (var library in libraries.Where(l => l.CollectionType == "tvshows"))
        {
            var items = await GetLibraryItemsAsync(library.ItemId, cancellationToken);
            episodes.AddRange(items);
        }

        return episodes;
    }

    public async Task<List<MediaItem>> GetLibraryItemsAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AddApiKey($"Items?ParentId={libraryId}&IncludeItemTypes=Movie,Episode&Recursive=true&Fields=Path,Tags,Overview");
            var response = await _httpClient.GetAsync(url, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<JellyfinItemsResponse>(cancellationToken: cancellationToken);
            return content?.Items ?? new List<MediaItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching library items for {LibraryId}", libraryId);
            return new List<MediaItem>();
        }
    }

    public async Task<List<Library>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AddApiKey("Library/VirtualFolders?Recursive=true");
            var response = await _httpClient.GetAsync(url, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<List<Library>>(cancellationToken: cancellationToken);
            return content ?? new List<Library>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching libraries");
            return new List<Library>();
        }
    }
}

public class JellyfinItemsResponse
{
    [JsonPropertyName("Items")]
    public List<MediaItem> Items { get; set; } = new();
}

public class MediaItem
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("Tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("Overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonPropertyName("ProductionYear")]
    public int? ProductionYear { get; set; }

    [JsonPropertyName("PremiereDate")]
    public DateTime? PremiereDate { get; set; }

    [JsonPropertyName("ParentId")]
    public string? ParentId { get; set; }
}

public class Library
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("CollectionType")]
    public string CollectionType { get; set; } = string.Empty;

    [JsonPropertyName("ItemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("LibraryOptions")]
    public LibraryOptions? LibraryOptions { get; set; }
}

public class LibraryOptions
{
    [JsonPropertyName("PrimaryImageAspectRatio")]
    public double? PrimaryImageAspectRatio { get; set; }
}
