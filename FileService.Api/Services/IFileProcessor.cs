namespace FileService.Api.Services;

public interface IFileSanitizer
{
    string FormatId { get; }
    SanitizationResult Sanitize(byte[] input, DetectedFile detected);
}

public record SanitizationResult(
    byte[] SanitizedBytes,
    Dtos.SanitizationReportDto Report
    );

public record DetectedFile(
    bool IsKnown,
    string? FormatId,
    string? ContentType
);
