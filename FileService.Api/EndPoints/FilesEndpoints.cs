using FileService.Api.Services;

namespace FileService.Api.Endpoints;

public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sanitize");

        group.MapPost("/", async (
            HttpContext http,
            IFormFile file,
            FileFormatDetector detector,
            FileProcessorRegistry registry,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            // Optional: also enforce per-file limit (defense-in-depth)
            // (Kestrel/form options already limit, but this is a nice explicit guard)
            if (file.Length > http.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize)
                return Results.BadRequest("File too large.");

            var detected = detector.Detect(file.FileName, file.ContentType);
            if (!detected.IsKnown)
                return Results.BadRequest("Unsupported file format (by extension).");

            var processor = registry.Resolve(detected);
            if (processor is null)
                return Results.BadRequest($"No processor registered for format '{detected.FormatId}'.");

            // Write to temp file so we can still return a clean 400 if invalid
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sanitized");
            await using var tempOut = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true);

            ProcessResult result;
            try
            {
                await using var input = file.OpenReadStream();
                result = await processor.ProcessAsync(input, tempOut, detected, ct);

                if (!result.Success)
                {
                    try { System.IO.File.Delete(tempPath); } catch { }
                    return Results.BadRequest(result.ErrorMessage);
                }

                await tempOut.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { System.IO.File.Delete(tempPath); } catch { }
                return Results.StatusCode(499); // Client Closed Request (common pattern)
            }
            catch
            {
                try { System.IO.File.Delete(tempPath); } catch { }
                throw;
            }

            // Return temp file as response
            var readStream = new FileStream(
                tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);

            http.Response.OnCompleted(() =>
            {
                try { readStream.Dispose(); } catch { }
                try { System.IO.File.Delete(tempPath); } catch { }
                return Task.CompletedTask;
            });

            var outName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.sanitized{Path.GetExtension(file.FileName)}";
            var response = Results.File(readStream, detected.ContentType ?? "application/octet-stream", outName);

            return response
                .WithHeader("X-Format", detected.FormatId!)
                .WithHeader("X-Was-Malicious", result.Report!.WasMalicious.ToString().ToLowerInvariant())
                .WithHeader("X-Replaced-Blocks", result.Report!.ReplacedBlocks.ToString())
                .WithHeader("X-Notes", result.Report!.Notes);
        })
        .DisableAntiforgery();
    }
}

static class ResultHeaderExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value)
        => Results.Extensions.WithHeader(result, name, value);
}
