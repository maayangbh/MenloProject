namespace FileService.Api.Services;

public enum ProcessingErrorCode
{
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

