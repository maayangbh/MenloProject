namespace FileService.Api.Dtos;

public record FileDto(
    string FileName,
    string ContentType,
    byte[] Content
);
