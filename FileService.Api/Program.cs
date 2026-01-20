using FileService.Api.Endpoints;
using FileService.Api.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// configurable max upload size (defaults to 100MB)
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

builder.Services.AddSingleton<FileFormatDetector>();
builder.Services.AddSingleton<FileProcessorRegistry>();
builder.Services.AddSingleton<IFileProcessor, AbcProcessor>();

var app = builder.Build();
app.MapFilesEndpoints();
app.Run();
