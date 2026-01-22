using FileService.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using FileService.Api.Infrastructure;
using FileService.Api.Dtos;
using Microsoft.Extensions.Logging;


namespace FileService.Api.Endpoints;

public static class FilesEndpoints
{
    /// <summary>
    /// Registers HTTP endpoints under /sanitize for file upload and sanitization.
    ///
    /// The endpoint performs: validation of upload size, format detection by
    /// extension, resolution of a processor from the registry, processing of
    /// the file stream, and returning the sanitized file with informative headers.
    /// </summary>
    public static void MapFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sanitize");

        group.MapPost("/", async Task<IResult>(
            HttpContext http,
            IFormFile file,
            FileFormatDetector detector,
            FileProcessorRegistry registry,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                // write log of the error
                return Results.Problem(
                    title: "No file uploaded",
                    detail: "The request did not contain a file.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
                
            
            var maxSize = http.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
            if (maxSize.HasValue && file.Length > maxSize.Value)
                return Results.Problem(
                    title: "File too large",
                    detail: $"Maximum allowed size is {maxSize.Value} bytes.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);

            var detected = detector.Detect(file.FileName);
            if (!detected.IsKnown)
                return Results.Problem(
                    title: "Unsupported file format",
                    detail: $"Extension '{Path.GetExtension(file.FileName)}' is not supported.",
                    statusCode: StatusCodes.Status400BadRequest);


            IFileProcessor? processor;
            try
            {
                processor = registry.Resolve(detected);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    title: "Internal server error",
                    detail: "Server failed to create processor for the requested format.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (processor is null)
                return Results.Problem(
                    title: "Unsupported file format",
                    detail: $"No processor registered for extension '{detected.Extension ?? Path.GetExtension(file.FileName)}'.",
                    statusCode: StatusCodes.Status400BadRequest);

            try
            {
                ProcessResult result;

                var memoryOut = new MemoryStream();

                await using var input = file.OpenReadStream();

                result = await processor.ProcessAsync(input, memoryOut, detected, ct);

                if (!result.Success)
                {
                    try { memoryOut.Dispose(); } catch { }

                    // result.Error is guaranteed non-null when Success == false
                    var err = result.Error!;

                    // Return a generic invalid-file problem; details are included
                    // in the response body so clients can surface them.
                    return Results.Problem(
                        title: $"Invalid file",
                        detail: err.Detail,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                await memoryOut.FlushAsync(ct);
                memoryOut.Position = 0;

                http.Response.OnCompleted(() =>
                {
                    try { memoryOut.Dispose(); } catch { }
                    return Task.CompletedTask;
                });

                var originalName = Path.GetFileName(file.FileName) ?? string.Empty;
                var ext = Path.GetExtension(originalName);
                var baseName = Path.GetFileNameWithoutExtension(originalName) ?? string.Empty;

                // Always use the pattern '{baseName}.sanitized{ext}' per request.
                var outName = $"{baseName}.sanitized{ext}";

                // Attempt to persist the sanitized file to disk for auditing.
                try
                {
                    var loggerFactory = http.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger(nameof(FilesEndpoints));

                    var config = http.RequestServices.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                    var configuredDir = config?.GetValue<string>("SanitizedOutput:Directory");
                    var outputDir = string.IsNullOrEmpty(configuredDir)
                        ? Path.Combine(Directory.GetCurrentDirectory(), "SanitizedFiles")
                        : configuredDir!;

                    Directory.CreateDirectory(outputDir);

                    var bytes = memoryOut.ToArray();
                    var savePath = Path.Combine(outputDir, outName);
                    await File.WriteAllBytesAsync(savePath, bytes, ct);
                    logger.LogInformation("Saved sanitized file to {Path}", savePath);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the client response; saving is best-effort
                    var loggerFactory = http.RequestServices.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                    var logger = loggerFactory?.CreateLogger(nameof(FilesEndpoints));
                    logger?.LogError(ex, "Failed to save sanitized file for {FileName}", outName);
                }

                var response = Results.File(
                    memoryOut,
                    "application/octet-stream",
                    outName);

                // Include extension and sanitization metadata in headers so
                // callers can quickly inspect what happened.
                return response
                    .WithHeader("X-Extension", detected.Extension ?? Path.GetExtension(file.FileName))
                    .WithHeader("X-Was-Malicious", result.Report!.WasMalicious.ToString().ToLowerInvariant())
                    .WithHeader("X-Replaced-Blocks", result.Report!.ReplacedBlocks.ToString())
                    .WithHeader("X-Notes", result.Report!.Notes);
            }
            catch
            {
                throw;
            }
        })
        .DisableAntiforgery();
    }
}
