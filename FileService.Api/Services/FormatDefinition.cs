using System.Collections.Generic;

namespace FileService.Api.Services;

/*
 * FormatDefinition and related types
 *
 * Purpose: models the format configuration loaded from Config/formats.yaml.
 * - `Id` is a stable identifier for the format (not used for runtime resolution
 *   after switching to extension-based resolution but still present in config).
 * - `Extension` determines which filenames map to this definition (e.g. ".abc").
 * - `Spec` contains per-format processing options used by processors.
 */
public class FormatDefinition
{

    /// <summary>Optional human-readable description of the format.</summary>
    public string? Description { get; set; }

    /// <summary>File extension associated with this format (e.g. ".abc").</summary>
    public string? Extension { get; set; }

    /// <summary>Nested spec providing processor configuration for this format.</summary>
    public SpecDefinition? Spec { get; set; }

    public class SpecDefinition
    {
        /// <summary>Optional prefix bytes for files of this format.</summary>
        public string? Prefix { get; set; }

        /// <summary>Optional suffix bytes for files of this format.</summary>
        public string? Suffix { get; set; }

        /// <summary>Optional regular expression used to validate blocks.</summary>
        public string? ValidBlockRegex { get; set; }

        /// <summary>Replacement text used when a block fails validation.</summary>
        public string? ErrorBlockReplacement { get; set; }
    }
}

/// <summary>Container for multiple <see cref="FormatDefinition"/> entries deserialized from YAML.</summary>
public class FormatDefinitions
{
    public List<FormatDefinition> Formats { get; set; } = new();
}
