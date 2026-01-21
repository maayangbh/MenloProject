using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class FileFormatDetectorTests
{
    [Fact]
    public void Detect_AbcExtension_ReturnsKnownFormat()
    {
        var det = new FileFormatDetector();
        var result = det.Detect("something.ABC", null);

        Assert.True(result.IsKnown);
        Assert.Equal("ABC", result.FormatId);
        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [Fact]
    public void Detect_UnknownExtension_ReturnsUnknown()
    {
        var det = new FileFormatDetector();
        var result = det.Detect("file.xyz", "text/plain");

        Assert.False(result.IsKnown);
        Assert.Null(result.FormatId);
        Assert.Equal("text/plain", result.ContentType);
    }
}
