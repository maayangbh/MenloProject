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

            var detected = detector.Detect(file.FileName, file.ContentType);
            if (!detected.IsKnown)
                return Results.BadRequest("Unsupported file format (by extension).");

            var processor = registry.Resolve(detected);
            if (processor is null)
                return Results.BadRequest($"No processor registered for format '{detected.FormatId}'.");

            // Create a temp file for sanitized output (no RAM blowups)
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sanitized");
            await using var tempOut = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);

            ProcessResult result;
            try
            {
                // Stream the upload (no CopyTo MemoryStream)
                await using var input = file.OpenReadStream(); // server still buffers multipart parsing, but this avoids your big allocations

                result = await processor.ProcessAsync(input, tempOut, detected, ct);

                if (!result.Success)
                {
                    try { System.IO.File.Delete(tempPath); } catch { }
                    return Results.BadRequest(result.ErrorMessage);
                }

                await tempOut.FlushAsync(ct);
            }
            catch
            {
                try { System.IO.File.Delete(tempPath); } catch { }
                throw;
            }

            // Reopen for reading to return to client
            var readStream = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

            // Ensure we delete the temp file when the response is done
            http.Response.OnCompleted(() =>
            {
                try { readStream.Dispose(); } catch { }
                try { System.IO.File.Delete(tempPath); } catch { }
                return Task.CompletedTask;
            });

            var outName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.sanitized{Path.GetExtension(file.FileName)}";
            var response = Results.File(
                readStream,
                detected.ContentType ?? "application/octet-stream",
                outName
            );

            // Feedback in headers (works well with file download)
            return response
                .WithHeader("X-Format", detected.FormatId!)
                .WithHeader("X-Was-Malicious", result.Report!.WasMalicious.ToString().ToLowerInvariant())
                .WithHeader("X-Replaced-Blocks", result.Report!.ReplacedBlocks.ToString())
                .WithHeader("X-Notes", result.Report!.Notes);
        })
        .DisableAntiforgery();
    }
}

// small helper for adding headers to IResult
static class ResultHeaderExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value)
        => Results.Extensions.WithHeader(result, name, value);
}
