namespace FileService.Api.Services;

using Microsoft.Extensions.Logging;

/// <summary>
/// Detects the file format by filename extension using configured format definitions.
///
/// This class loads a map of known extensions to <see cref="FormatDefinition"/>
/// and exposes <see cref="Detect(string)"/> which returns a <see cref="DetectedFile"/>.
/// The detector normalizes extensions to a canonical form (lower-case, leading dot)
/// so subsequent resolution can be performed reliably by extension.
/// </summary>
public class FileFormatDetector
{
    private readonly Dictionary<string, FormatDefinition> _formatsByExtension;
    private readonly ILogger<FileFormatDetector> _logger;

    public FileFormatDetector(FormatConfigLoader loader, ILogger<FileFormatDetector> logger)
    {
        _logger = logger;
        _formatsByExtension = loader.Load()
            .Where(d => !string.IsNullOrEmpty(d.Extension))
            .ToDictionary(d => Normalize(d.Extension!), d => d, StringComparer.OrdinalIgnoreCase);

        // Informational: how many formats were loaded at startup
        _logger.LogInformation("Loaded {Count} format definitions.", _formatsByExtension.Count);
        if (_formatsByExtension.Count == 0)
        {
            _logger.LogWarning("No format definitions found in configuration.");
        }
        // Debug: list the configured extensions for diagnostics (less verbose than Trace)
        _logger.LogDebug("Known extensions: {Extensions}", string.Join(", ", _formatsByExtension.Keys));
    }

    private static string Normalize(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();

    public DetectedFile Detect(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var normExt = string.IsNullOrEmpty(ext) ? ext : Normalize(ext);

        _logger.LogDebug("FileFormatDetector: fileName={FileName}, ext={Ext}, normExt={NormExt}", fileName, ext, normExt);
        _logger.LogDebug("Known extensions: {Extensions}", string.Join(", ", _formatsByExtension.Keys));

        if (string.IsNullOrEmpty(ext))
        {
            _logger.LogWarning("Filename '{FileName}' has no extension; treating as unknown format.", fileName);
        }

            if (!string.IsNullOrEmpty(normExt) && _formatsByExtension.TryGetValue(normExt, out var def))
            {
                // Do not expose or rely on FormatId for resolution; only return extension
                return new DetectedFile(IsKnown: true, Extension: normExt);
            }

        _logger.LogDebug("No match for extension: {NormExt}", normExt);
        // Unknown formats: DetectedFile doesn't include content-type
        return new DetectedFile(IsKnown: false, Extension: string.IsNullOrEmpty(normExt) ? null : normExt);
    }
}
