namespace FileService.Api.Services;

public record DetectedFile(
    bool IsKnown,
    string? FormatId,
    string? ContentType
);
