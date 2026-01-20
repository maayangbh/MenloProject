namespace FileService.Api.Dtos;

public record SanitizationReportDto(
    bool WasMalicious,
    int ReplacedBlocks,
    string Notes
);
