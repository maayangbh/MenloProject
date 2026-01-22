using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class DetectedFileTests
{
    [Fact]
    public void Record_StoresValues()
    {
        var d = new DetectedFile(true, ".abc");
        Assert.True(d.IsKnown);
        Assert.Equal(".abc", d.Extension);
    }
}
