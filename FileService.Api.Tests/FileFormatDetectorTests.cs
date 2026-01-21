using FileService.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Xunit;

namespace FileService.Api.Tests;

public class FileFormatDetectorTests
{
    private class TestEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FileService.Api.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }

    private static FileFormatDetector CreateDetector()
    {
        // Search upward for the FileService.Api project folder (contains Config/formats.yaml)
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? found = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FileService.Api");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Config", "formats.yaml")))
            {
                found = candidate;
                break;
            }

            if (File.Exists(Path.Combine(dir.FullName, "Config", "formats.yaml")))
            {
                found = dir.FullName;
                break;
            }

            dir = dir.Parent;
        }

        if (found == null)
            throw new DirectoryNotFoundException("Could not locate FileService.Api project folder containing Config/formats.yaml");

        var env = new TestEnv { ContentRootPath = Path.GetFullPath(found) };
        env.WebRootPath = Path.Combine(env.ContentRootPath, "wwwroot");
        env.ContentRootFileProvider = new PhysicalFileProvider(env.ContentRootPath);
        env.WebRootFileProvider = Directory.Exists(env.WebRootPath)
            ? new PhysicalFileProvider(env.WebRootPath)
            : new NullFileProvider();
        var loader = new FormatConfigLoader(env);
        return new FileFormatDetector(loader);
    }

    [Fact]
    public void Detect_AbcExtension_ReturnsKnownFormat()
    {
        var det = CreateDetector();
        var result = det.Detect("something.ABC", null);

        Assert.True(result.IsKnown);
        Assert.Equal("ABC", result.FormatId);
        Assert.Equal("text/plain", result.ContentType);
    }

    [Fact]
    public void Detect_UnknownExtension_ReturnsUnknown()
    {
        var det = CreateDetector();
        var result = det.Detect("file.unknownext", "text/plain");

        Assert.False(result.IsKnown);
        Assert.Null(result.FormatId);
        Assert.Equal("text/plain", result.ContentType);
    }
}
