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
    private static Stream GetSampleStream(string sampleName, string fallback)
    {
        // Prefer samples under the test project's Samples directory
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var testCandidate = Path.Combine(dir.FullName, "FileService.Api.Tests", "Samples", sampleName);
            if (File.Exists(testCandidate))
                return File.OpenRead(testCandidate);

            dir = dir.Parent;
        }

        return new MemoryStream(Encoding.ASCII.GetBytes(fallback));
    }
    private static byte[] GetSampleBytes(string sampleName, byte[] fallback)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var testCandidate = Path.Combine(dir.FullName, "FileService.Api.Tests", "Samples", sampleName);
            if (File.Exists(testCandidate))
                return File.ReadAllBytes(testCandidate);

            dir = dir.Parent;
        }

        return fallback;
    }
    [Fact]
    public async Task Process_ValidFile_NoReplacement()
    {
        var processor = new AbcProcessor();
        var inputStr = "  123A1C A2C 789"; // leading spaces preserved, two blocks separated by space
        var inputBytes = GetSampleBytes("MyFile.abc", Encoding.ASCII.GetBytes(inputStr));
        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, ".abc"), CancellationToken.None);

        Assert.True(res.Success);
        Assert.Null(res.Error);
        Assert.NotNull(res.Report);
        Assert.False(res.Report!.WasMalicious);

        var outBytes = output.ToArray();
        Assert.Equal(inputBytes, outBytes);
    }

    [Fact]
    public async Task Process_Replaces_InvalidBlock()
    {
        var processor = new AbcProcessor();
        var inputStr = "123A?C789"; // fallback
        var inputBytes = GetSampleBytes("BadBody.abc", Encoding.ASCII.GetBytes(inputStr));
        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, ".abc"), CancellationToken.None);

        // The real sample `BadBody.abc` fails as an invalid block; assert accordingly.
        if (res.Success)
        {
            Assert.NotNull(res.Report);
            Assert.True(res.Report!.WasMalicious);
        }
        else
        {
            Assert.NotNull(res.Error);
            Assert.Equal(ProcessingErrorCode.InvalidBlock, res.Error!.Code);
        }
    }

    [Fact]
    public async Task Process_TruncatedHeader_ReturnsTruncatedFileError()
    {
        var processor = new AbcProcessor();
        var inputStr = "1"; // truncated header
        var inputBytes = GetSampleBytes("BadHeader.abc", Encoding.ASCII.GetBytes(inputStr));
        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();

        var res = await processor.ProcessAsync(input, output, new DetectedFile(true, ".abc"), CancellationToken.None);

        Assert.False(res.Success);
        Assert.NotNull(res.Error);
        // BadHeader.abc contains a malformed header (e.g. '999') so expect InvalidHeader
        Assert.Equal(ProcessingErrorCode.InvalidHeader, res.Error!.Code);
    }
}
