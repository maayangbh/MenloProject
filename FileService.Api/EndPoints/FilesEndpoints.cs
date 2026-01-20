using FileService.Api.Dtos;

namespace FileService.Api.Endpoints;

public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {

        var group = app.MapGroup("sanitize");
        
        // POST /sanitize
        group.MapPost("/", static async (IFormFile file) =>
        {

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] bytes = ms.ToArray();

            FileDto fileDto = new(
                file.FileName,
                file.ContentType,
                bytes
                    );

            return Results.Ok(fileDto);
        });
    }
}