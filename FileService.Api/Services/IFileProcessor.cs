using FileService.Api.Dtos;

namespace FileService.Api.Services;

public interface IFileProcessor
{
    string FormatId { get; }
    Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct);
}

public record ProcessResult(
    bool Success,
    SanitizationReportDto? Report,
    ProcessingError? Error
);
