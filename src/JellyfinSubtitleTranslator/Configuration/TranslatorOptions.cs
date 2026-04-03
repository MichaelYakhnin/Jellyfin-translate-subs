namespace JellyfinSubtitleTranslator.Configuration;

public class TranslatorOptions
{
    public string MediaPath { get; set; } = "/media";
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "rus";
    public string LibreTranslateUrl { get; set; } = "http://localhost:5000";
    public int MaxBatchSize { get; set; } = 50;
    public int MaxConcurrency { get; set; } = 2;
    public int FileReadyDelayMs { get; set; } = 2000;
    public int WebhookProcessingDelayMs { get; set; } = 5000;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 1000;
    public JellyfinOptions Jellyfin { get; set; } = new();
}

public class JellyfinOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8096";
    public string ApiKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
