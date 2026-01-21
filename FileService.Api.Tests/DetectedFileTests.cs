using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class DetectedFileTests
{
    [Fact]
    public void Record_StoresValues()
    {
        var d = new DetectedFile(true, "ABC", "application/octet-stream");
        Assert.True(d.IsKnown);
        Assert.Equal("ABC", d.FormatId);
        Assert.Equal("application/octet-stream", d.ContentType);
    }
}
