namespace FileService.Api.Services;

public class FileFormatDetector
{
    public DetectedFile Detect(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName);

        if (ext.Equals(".abc", StringComparison.OrdinalIgnoreCase))
        {
            return new DetectedFile(
                IsKnown: true,
                FormatId: "ABC",
                ContentType: contentType ?? "application/octet-stream"
            );
        }

        return new DetectedFile(IsKnown: false, FormatId: null, ContentType: contentType);
    }
}
