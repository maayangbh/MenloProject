using System.Collections.Generic;

namespace FileService.Api.Services;

public class FormatDefinition
{
    public string Id { get; set; } = null!;
    public string? Extension { get; set; }
    public string? ContentType { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public int BlockLength { get; set; }
    public string? BlockPattern { get; set; }
    public int MaxBlockLength { get; set; }
    public string ValidRegex { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string ErrorToken { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class FormatDefinitions
{
    public List<FormatDefinition> Formats { get; set; } = new();
}
