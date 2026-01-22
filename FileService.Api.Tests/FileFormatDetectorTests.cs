using FileService.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
        return new FileFormatDetector(loader, NullLogger<FileFormatDetector>.Instance);
    }

    private static string FindSamplePath(string sampleName, string fallbackFileName)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var testCandidate = Path.Combine(dir.FullName, "FileService.Api.Tests", "Samples", sampleName);
            if (File.Exists(testCandidate))
                return testCandidate;

            dir = dir.Parent;
        }

        return fallbackFileName;
    }

    [Fact]
    public void Detect_AbcExtension_ReturnsKnownFormat()
    {
        var det = CreateDetector();
        var sample = FindSamplePath("MyFile.abc", "something.ABC");
        var result = det.Detect(sample);

        Assert.True(result.IsKnown);
        Assert.Equal(".abc", result.Extension);
        // ContentType removed from DetectedFile; the detector always treats files as application/octet-stream
    }

    [Fact]
    public void Detect_UnknownExtension_ReturnsUnknown()
    {
        var det = CreateDetector();
        var sample = FindSamplePath("UnknownFormat.xyz", "file.unknownext");
        var result = det.Detect(sample);

        Assert.False(result.IsKnown);
        Assert.Equal(".xyz", result.Extension);
        // ContentType removed from DetectedFile; the detector always treats files as application/octet-stream
    }

    [Fact]
    public void Detect_FileNameWithMultipleDots_ReturnsAbc()
    {
        var det = CreateDetector();
        var sample = "MyFile.Benign.abc";
        var result = det.Detect(sample);

        Assert.True(result.IsKnown);
        Assert.Equal(".abc", result.Extension);
    }
}
