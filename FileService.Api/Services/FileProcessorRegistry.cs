namespace FileService.Api.Services;

using Microsoft.Extensions.Logging;

/// <summary>
/// Registry which maps file extensions to their <see cref="FormatDefinition"/>.
///
/// Purpose: keep a single, application-wide map of configured formats (by extension)
/// and provide a <see cref="Resolve(DetectedFile)"/> method that creates a new
/// <see cref="IFileProcessor"/> instance for each file being processed.
/// </summary>
public class FileProcessorRegistry
{
    private readonly Dictionary<string, FormatDefinition> _byExtension;
    private readonly ILogger<FileProcessorRegistry> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Default constructor used by tests and simple callers: uses NullLoggerFactory
    public FileProcessorRegistry(IEnumerable<FormatDefinition> defs)
        : this(defs, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileProcessorRegistry>.Instance)
    {
    }

    public FileProcessorRegistry(IEnumerable<FormatDefinition> defs, ILoggerFactory loggerFactory, ILogger<FileProcessorRegistry> logger)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;

        _byExtension = defs
            .Where(d => !string.IsNullOrEmpty(d.Extension))
            .ToDictionary(d => Normalize(d.Extension!), d => d, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("FileProcessorRegistry initialized with {Count} entries.", _byExtension.Count);
    }

    private static string Normalize(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();

    // Resolve should create a new processor instance per file
    public IFileProcessor? Resolve(DetectedFile detected)
    {
        if (!detected.IsKnown || detected.Extension is null)
        {
            _logger.LogWarning("Resolve called with unknown or null extension: IsKnown={IsKnown}, Extension={Extension}", detected.IsKnown, detected.Extension);
            return null;
        }

        var normExt = string.IsNullOrEmpty(detected.Extension) ? detected.Extension : Normalize(detected.Extension);
        _logger.LogDebug("Resolving processor for extension {Extension}", normExt);
        if (!_byExtension.TryGetValue(normExt!, out var def))
        {
            _logger.LogDebug("No format definition found for extension {Extension}", normExt);
            return null;
        }

        _logger.LogInformation("Creating processor for extension {Extension}", normExt);
        try
        {
            var procType = def.Spec?.ProcessorType;
            if (!string.IsNullOrEmpty(procType))
            {
                switch (procType.Trim().ToLowerInvariant())
                {
                    case "generic":
                    case "genericfileprocessor":
                        return new GenericFileProcessor(def, _loggerFactory.CreateLogger<GenericFileProcessor>());
                    default:
                        _logger.LogWarning("Unknown processor type '{ProcessorType}' for extension {Extension}; falling back to generic.", procType, normExt);
                        return new GenericFileProcessor(def, _loggerFactory.CreateLogger<GenericFileProcessor>());
                }
            }

            // default: generic processor
            return new GenericFileProcessor(def, _loggerFactory.CreateLogger<GenericFileProcessor>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating processor for extension {Extension}", normExt);
            throw new InvalidOperationException("Failed to create processor for the requested format.");
        }
    }
}
