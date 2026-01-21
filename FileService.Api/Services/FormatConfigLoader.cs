using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FileService.Api.Services;

public class FormatConfigLoader
{
    private readonly IWebHostEnvironment _env;

    public FormatConfigLoader(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IReadOnlyList<FormatDefinition> Load()
    {
        var cfgPath = Path.Combine(_env.ContentRootPath, "Config", "formats.yaml");
        if (!File.Exists(cfgPath)) return Array.Empty<FormatDefinition>();

        var yaml = File.ReadAllText(cfgPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var defs = deserializer.Deserialize<FormatDefinitions>(yaml);
        if (defs == null) return Array.Empty<FormatDefinition>();
        return defs.Formats;
    }
}
