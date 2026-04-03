using JellyfinSubtitleTranslator.Configuration;
using JellyfinSubtitleTranslator.Services;
using JellyfinSubtitleTranslator.Services.Jellyfin;
using JellyfinSubtitleTranslator.Workers;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TranslatorOptions>(options =>
{
    options.MediaPath = builder.Configuration["Translator:MediaPath"] ?? "/media";
    options.SourceLanguage = builder.Configuration["Translator:SourceLanguage"] ?? "";
    options.TargetLanguage = builder.Configuration["Translator:TargetLanguage"] ?? "rus";
    options.LibreTranslateUrl = builder.Configuration["Translator:LibreTranslateUrl"] ?? "http://localhost:5000";
    options.MaxBatchSize = int.Parse(builder.Configuration["Translator:MaxBatchSize"] ?? "50");
    options.MaxConcurrency = int.Parse(builder.Configuration["Translator:MaxConcurrency"] ?? "2");
    options.FileReadyDelayMs = int.Parse(builder.Configuration["Translator:FileReadyDelayMs"] ?? "2000");
    options.WebhookProcessingDelayMs = int.Parse(builder.Configuration["Translator:WebhookProcessingDelayMs"] ?? "5000");
    options.MaxRetryAttempts = int.Parse(builder.Configuration["Translator:MaxRetryAttempts"] ?? "3");
    options.RetryBaseDelayMs = int.Parse(builder.Configuration["Translator:RetryBaseDelayMs"] ?? "1000");

    options.Jellyfin = new JellyfinOptions
    {
        BaseUrl = builder.Configuration["Translator:Jellyfin:BaseUrl"] ?? builder.Configuration["Jellyfin:BaseUrl"] ?? "http://localhost:8096",
        ApiKey = builder.Configuration["Translator:Jellyfin:ApiKey"] ?? builder.Configuration["Jellyfin:ApiKey"] ?? "",
        UserId = builder.Configuration["Translator:Jellyfin:UserId"] ?? builder.Configuration["Jellyfin:UserId"] ?? ""
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<IFileQueue, FileQueue>();
builder.Services.AddSingleton<ISrtParser, SrtParser>();
builder.Services.AddSingleton<ILanguageMapper, LanguageMapper>();

//builder.Services.AddSingleton<IWebhookProcessingService, WebhookProcessingService>();
builder.Services.AddSingleton<ISubtitleTranslationService, SubtitleTranslationService>();

builder.Services.AddHttpClient<ITranslationClient, LibreTranslateClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHttpClient<IJellyfinService, JellyfinService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    status = "running",
    service = "JellyfinSubtitleTranslator",
    endpoints = new[]
    {
        "GET /api/media - List media items",
        "GET /api/media/libraries - List libraries",
        "POST /api/translate/manual - Translate subtitles",
        "POST /api/translate/batch - Batch translate",
        "GET /api/translate/discover - Discover subtitles"
    }
}));

app.Run();
