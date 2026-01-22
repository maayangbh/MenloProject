namespace FileService.Api.Services;

/// <summary>
/// Represents the result of format detection for a file.
///
/// - <see cref="IsKnown"/> indicates whether the extension matched a configured format.
/// - <see cref="Extension"/> is the normalized extension (e.g. ".abc") when available.
/// </summary>
public record DetectedFile(
    bool IsKnown,
    string? Extension
);
