using System.Text.Json.Serialization;

namespace JellyfinSubtitleTranslator.Models;

public class TranslateRequest
{
    [JsonPropertyName("q")]
    public string Q { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "auto";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "he";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "text";
}

public class TranslateResponse
{
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = string.Empty;
}

public class DetectRequest
{
    [JsonPropertyName("q")]
    public string Q { get; set; } = string.Empty;
}

public class DetectResponse
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
