namespace FileService.Api.Services;

public class FileFormatDetector
{
    private readonly Dictionary<string, FormatDefinition> _byExtension;

    public FileFormatDetector(FormatConfigLoader loader)
    {
        _byExtension = loader.Load()
            .Where(d => !string.IsNullOrEmpty(d.Extension))
            .ToDictionary(d => Normalize(d.Extension!), StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();

    public DetectedFile Detect(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName);

        if (!string.IsNullOrEmpty(ext) && _byExtension.TryGetValue(ext, out var def))
        {
            return new DetectedFile(IsKnown: true, FormatId: def.Id, ContentType: def.ContentType ?? contentType ?? "application/octet-stream");
        }

        return new DetectedFile(IsKnown: false, FormatId: null, ContentType: contentType);
    }
}
