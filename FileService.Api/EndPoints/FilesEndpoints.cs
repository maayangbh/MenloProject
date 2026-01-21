using FileService.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using FileService.Api.Infrastructure;
using FileService.Api.Dtos;


namespace FileService.Api.Endpoints;

public static class FilesEndpoints
{
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
                return Results.Problem(
                    title: "No file uploaded",
                    detail: "The request did not contain a file.",
                    statusCode: StatusCodes.Status400BadRequest);

            var maxSize = http.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
            if (maxSize.HasValue && file.Length > maxSize.Value)
                return Results.Problem(
                    title: "File too large",
                    detail: $"Maximum allowed size is {maxSize.Value} bytes.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);

            var detected = detector.Detect(file.FileName, file.ContentType);
            if (!detected.IsKnown)
                return Results.Problem(
                    title: "Unsupported file format",
                    detail: $"Extension '{Path.GetExtension(file.FileName)}' is not supported.",
                    statusCode: StatusCodes.Status400BadRequest);


            var processor = registry.Resolve(detected);
            if (processor is null)
                return Results.Problem(
                    title: "Unsupported file format",
                    detail: $"No processor registered for format '{detected.FormatId}'.",
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

                    return Results.Problem(
                        title: $"Invalid {detected.FormatId} file",
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

                var outName =
                    $"{Path.GetFileNameWithoutExtension(file.FileName)}.sanitized{Path.GetExtension(file.FileName)}";

                var response = Results.File(
                    memoryOut,
                    detected.ContentType ?? "application/octet-stream",
                    outName);

                return response
                    .WithHeader("X-Format", detected.FormatId!)
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
