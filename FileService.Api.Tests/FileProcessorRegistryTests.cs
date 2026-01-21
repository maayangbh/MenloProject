using System.Threading.Tasks;
using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class FileProcessorRegistryTests
{
    private class DummyProcessor : IFileProcessor
    {
        public string FormatId => "ABC";
        public Task<ProcessResult> ProcessAsync(System.IO.Stream input, System.IO.Stream output, DetectedFile detected, System.Threading.CancellationToken ct)
            => Task.FromResult(new ProcessResult(true, null, null));
    }

    [Fact]
    public void Resolve_KnownFormat_ReturnsProcessor()
    {
        var registry = new FileProcessorRegistry(new[] { new DummyProcessor() });
        var detected = new DetectedFile(true, "ABC", "application/octet-stream");

        var p = registry.Resolve(detected);

        Assert.NotNull(p);
        Assert.Equal("ABC", p!.FormatId);
    }

    [Fact]
    public void Resolve_UnknownFormat_ReturnsNull()
    {
        var registry = new FileProcessorRegistry(new[] { new DummyProcessor() });
        var detected = new DetectedFile(false, null, null);

        var p = registry.Resolve(detected);

        Assert.Null(p);
    }
}
