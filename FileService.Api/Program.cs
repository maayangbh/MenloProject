using FileService.Api.Endpoints;
using FileService.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Configure maximum upload size (defaults to 100MB). This centralizes
// upload size limits in one place and applies them to both model binding
// (multipart) and Kestrel's request body limits.
var maxUploadBytes =
    builder.Configuration.GetValue<long?>("UploadLimits:MaxBytes")
    ?? 100L * 1024 * 1024;

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxUploadBytes;
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = maxUploadBytes;
});

// Register shared services as singletons. The registry is a singleton that
// stores the mapping of extension -> FormatDefinition. The registry creates
// a new processor instance per file at Resolve time.
builder.Services.AddSingleton<FileFormatDetector>();
builder.Services.AddSingleton<FormatConfigLoader>();

// Register FileProcessorRegistry using DI. We pass the loaded definitions
// into the registry so the singleton holds the configuration map, while
// processors themselves are instantiated per-file when resolved.
builder.Services.AddSingleton<FileProcessorRegistry>(sp =>
{
    var loader = sp.GetRequiredService<FormatConfigLoader>();
    var defs = loader.Load();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileProcessorRegistry>>();
    var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
    return new FileProcessorRegistry(defs, logger, loggerFactory);
});

var app = builder.Build();
app.MapFilesEndpoints();
app.Run();
