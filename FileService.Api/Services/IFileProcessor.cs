using FileService.Api.Dtos;

namespace FileService.Api.Services;

public interface IFileProcessor
{
    /// <summary>
    /// Process the provided input stream and write sanitized output to <paramref name="output"/>.
    /// The <paramref name="detected"/> value carries detection metadata such as the file extension.
    /// </summary>
    Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct);
}

/// <summary>
/// Represents a file processor instance which can process a single file.
/// Implementations perform sanitization/validation and return a <see cref="ProcessResult"/>.
/// </summary>
public record ProcessResult(
    bool Success,
    SanitizationReportDto? Report,
    ProcessingError? Error
);
/// <summary>Result returned from a processor run.</summary>



