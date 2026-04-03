using System.Text.Json.Serialization;

namespace JellyfinSubtitleTranslator.Models;

public class JellyfinWebhookPayload
{
    [JsonPropertyName("NotificationType")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("Item")]
    public JellyfinItem? Item { get; set; }
}

public class JellyfinItem
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("Path")]
    public string? Path { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }
}
