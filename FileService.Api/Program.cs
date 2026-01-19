using FileService.Api.Dtos;

var builder = WebApplication.CreateBuilder(args);



var app = builder.Build();



// POST /sanitize
app.MapPost("/sanitize", static async (IFormFile file) =>
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

app.Run();
