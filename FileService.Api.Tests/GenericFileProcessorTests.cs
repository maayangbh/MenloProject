using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using FileService.Api.Services;
using Xunit;

namespace FileService.Api.Tests;

public class GenericFileProcessorTests
{
    [Fact]
    public void IsWs_ReturnsTrueForWhitespace()
    {
        var type = typeof(GenericFileProcessor);
        var method = type.GetMethod("IsWs", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True((method!.Invoke(null, new object[] { (byte)' ' }) as bool?) == true);
        Assert.True((method!.Invoke(null, new object[] { (byte)'\n' }) as bool?) == true);
        Assert.True((method!.Invoke(null, new object[] { (byte)'\r' }) as bool?) == true);
        Assert.True((method!.Invoke(null, new object[] { (byte)'\t' }) as bool?) == true);
        Assert.False((method!.Invoke(null, new object[] { (byte)'A' }) as bool?) == true);
    }

    [Fact]
    public void Fail_ReturnsExpectedError()
    {
        var type = typeof(GenericFileProcessor);
        var method = type.GetMethod("Fail", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { ProcessingErrorCode.InvalidBlock, "bad block" });
        Assert.NotNull(result);
        var errorProp = result!.GetType().GetProperty("Error");
        Assert.NotNull(errorProp);
        var error = errorProp!.GetValue(result);
        Assert.NotNull(error);
        var codeProp = error!.GetType().GetProperty("Code");
        var detailProp = error!.GetType().GetProperty("Detail");
        Assert.Equal(ProcessingErrorCode.InvalidBlock, codeProp!.GetValue(error));
        Assert.Equal("bad block", detailProp!.GetValue(error));
    }
}
