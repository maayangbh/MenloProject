using System;
using System.Threading.Tasks;
using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class FileProcessorRegistryTests
{
    

    [Fact]
    public void Resolve_KnownFormat_ReturnsProcessor()
    {
        var def = new FormatDefinition {Extension = ".abc" };
        var registry = new FileProcessorRegistry(new[] { def }, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileProcessorRegistry>.Instance, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var detected = new DetectedFile(true, ".abc");

        var p = registry.Resolve(detected);

        Assert.NotNull(p);
    }

    [Fact]
    public void Resolve_UnknownFormat_ReturnsNull()
    {
        var def = new FormatDefinition {Extension = ".abc" };
        var registry = new FileProcessorRegistry(new[] { def }, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileProcessorRegistry>.Instance, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var detected = new DetectedFile(false, null);

        var p = registry.Resolve(detected);

        Assert.Null(p);
    }

    [Fact]
    public void Resolve_ProcessorCreationFailure_ThrowsGenericException()
    {
        // Arrange: create a definition that will cause the processor ctor to throw (invalid regex)
        var def = new FormatDefinition
        {
            Extension = ".abc",
            Spec = new FormatDefinition.SpecDefinition
            {
                ValidBlockRegex = "[" // invalid regex -> Regex constructor will throw
            }
        };

        var registry = new FileProcessorRegistry(new[] { def }, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileProcessorRegistry>.Instance, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var detected = new DetectedFile(true, ".abc");

        // Act & Assert: registry should catch the detailed error and rethrow a generic InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve(detected));
        Assert.Equal("Failed to create processor for the requested format.", ex.Message);
    }
}
