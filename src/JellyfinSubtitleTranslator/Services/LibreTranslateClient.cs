using System.Net.Http.Json;
using System.Text.Json;
using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace JellyfinSubtitleTranslator.Services;

public interface ITranslationClient
{
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
    Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
    Task<string> TranslateFileAsync(string filePath, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
}

public class LibreTranslateClient : ITranslationClient
{
    private readonly HttpClient _httpClient;
    private readonly TranslatorOptions _options;
    private readonly ILogger<LibreTranslateClient> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

    public LibreTranslateClient(
        HttpClient httpClient,
        IOptions<TranslatorOptions> options,
        ILogger<LibreTranslateClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.LibreTranslateUrl);

        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => ex.InnerException is IOException or System.Net.Sockets.SocketException)
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {AttemptNumber} after {Delay}ms due to: {Exception}",
                        args.AttemptNumber,
                        _options.RetryBaseDelayMs * Math.Pow(2, args.AttemptNumber - 1),
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request was cancelled by caller!");
        }

        var request = new TranslateRequest
        {
            Q = text,
            Source = string.IsNullOrEmpty(sourceLanguage) ? "auto" : sourceLanguage,
            Target = targetLanguage
        };

        // var response = await _retryPipeline.ExecuteAsync(
        //     async ct => await _httpClient.PostAsJsonAsync("/translate", request, ct),
        //     cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("/translate", request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: cancellationToken);
        return result?.TranslatedText ?? text;
    }

    public async Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "en";

        var request = new DetectRequest { Q = text };

        var response = await _retryPipeline.ExecuteAsync(
            async ct => await _httpClient.PostAsJsonAsync("/detect", request, ct),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<DetectResponse>>(cancellationToken: cancellationToken);
        return results?.FirstOrDefault()?.Language ?? "en";
    }

    public async Task<string> TranslateFileAsync(string filePath, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Subtitle file not found", filePath);
        }

        using var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, cancellationToken));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        form.Add(new StringContent(string.IsNullOrEmpty(sourceLanguage) ? "auto" : sourceLanguage), "source_lang");
        form.Add(new StringContent(targetLanguage), "target_lang");

        var response = await _httpClient.PostAsync("/translate_file", form, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
        {
            _logger.LogWarning("Server does not support file translation, falling back to text-based translation");
            var textContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await TranslateAsync(textContent, sourceLanguage, targetLanguage, cancellationToken);
        }

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        var jsonDoc = JsonDocument.Parse(responseContent);
        if (jsonDoc.RootElement.TryGetProperty("translatedFileUrl", out var translatedFileElement))
        {
            var translatedFile = translatedFileElement.GetString();
            if (!string.IsNullOrEmpty(translatedFile))
            {
                return translatedFile;
            }
        }

        return responseContent;
    }
}
