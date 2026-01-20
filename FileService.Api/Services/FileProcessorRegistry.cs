namespace FileService.Api.Services;

public class FileProcessorRegistry
{
    private readonly Dictionary<string, IFileProcessor> _byFormat;

    public FileProcessorRegistry(IEnumerable<IFileProcessor> processors)
    {
        _byFormat = processors.ToDictionary(p => p.FormatId, p => p, StringComparer.OrdinalIgnoreCase);
    }

    public IFileProcessor? Resolve(DetectedFile detected)
    {
        if (!detected.IsKnown || detected.FormatId is null) return null;
        return _byFormat.TryGetValue(detected.FormatId, out var p) ? p : null;
    }
}
