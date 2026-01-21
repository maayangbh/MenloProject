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
        var normExt = string.IsNullOrEmpty(ext) ? ext : Normalize(ext);

        // DEBUG LOGGING
        Console.WriteLine($"[DEBUG] FileFormatDetector: fileName={fileName}, ext={ext}, normExt={normExt}");
        Console.WriteLine($"[DEBUG] Known extensions: {string.Join(", ", _byExtension.Keys)}");

        if (!string.IsNullOrEmpty(normExt) && _byExtension.TryGetValue(normExt, out var def))
        {
            Console.WriteLine($"[DEBUG] Matched extension: {normExt} to format {def.Id}");
            return new DetectedFile(IsKnown: true, FormatId: def.Id, ContentType: def.ContentType ?? contentType ?? "application/octet-stream");
        }

        Console.WriteLine($"[DEBUG] No match for extension: {normExt}");
        return new DetectedFile(IsKnown: false, FormatId: null, ContentType: contentType);
    }
}
