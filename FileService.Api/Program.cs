using FileService.Api.Endpoints;
using FileService.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Linq;

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
builder.Services.AddSingleton<FormatConfigLoader>();

// Register FileProcessorRegistry using DI (avoids calling BuildServiceProvider)
builder.Services.AddSingleton<FileProcessorRegistry>(sp =>
{
    var loader = sp.GetRequiredService<FormatConfigLoader>();
    var defs = loader.Load();
    var processors = defs.Select(d => (IFileProcessor)new GenericFileProcessor(d));
    return new FileProcessorRegistry(processors);
});

var app = builder.Build();
app.MapFilesEndpoints();
app.Run();
