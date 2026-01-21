namespace FileService.Api.Services;

public enum ProcessingErrorCode
{
    EmptyFile,
    InvalidHeader,
    InvalidFooter,
    InvalidBlock,
    UnexpectedByte,
    TruncatedFile,
    TrailingData,
    InvalidFormat
}

public record ProcessingError(
    ProcessingErrorCode Code,
    string Detail
    );

