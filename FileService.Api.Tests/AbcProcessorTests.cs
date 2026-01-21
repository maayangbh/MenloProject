using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileService.Api.Dtos;
using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class AbcProcessorTests
{
    [Fact]
    public async Task Process_ValidFile_NoReplacement()
    {
        var processor = new AbcProcessor();
        var inputStr = "  123A1C A2C 789"; // leading spaces preserved, two blocks separated by space
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(inputStr));
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, "ABC", null), CancellationToken.None);

        Assert.True(res.Success);
        Assert.Null(res.Error);
        Assert.NotNull(res.Report);
        Assert.False(res.Report!.WasMalicious);

        var outStr = Encoding.ASCII.GetString(output.ToArray());
        Assert.Equal(inputStr.Replace(" ", " "), outStr);
    }

    [Fact]
    public async Task Process_Replaces_InvalidBlock()
    {
        var processor = new AbcProcessor();
        var inputStr = "123A?C789"; // '?' is not '1'..'9' so should be replaced with 255
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(inputStr));
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, "ABC", null), CancellationToken.None);

        Assert.True(res.Success);
        Assert.Null(res.Error);
        Assert.NotNull(res.Report);
        Assert.True(res.Report!.WasMalicious);
        Assert.Equal(1, res.Report.ReplacedBlocks);

        var outStr = Encoding.ASCII.GetString(output.ToArray());
        Assert.Equal("123A255C789", outStr);
    }

    [Fact]
    public async Task Process_TruncatedHeader_ReturnsTruncatedFileError()
    {
        var processor = new AbcProcessor();
        var inputStr = "1"; // truncated header
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(inputStr));
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, "ABC", null), CancellationToken.None);

        Assert.False(res.Success);
        Assert.NotNull(res.Error);
        Assert.Equal(ProcessingErrorCode.TruncatedFile, res.Error!.Code);
    }
}
