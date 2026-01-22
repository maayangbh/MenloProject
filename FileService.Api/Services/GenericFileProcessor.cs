using FileService.Api.Dtos;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FileService.Api.Services;

/// <summary>
/// Generic file processor implementation driven by <see cref="FormatDefinition"/>.
///
/// Purpose: perform streaming validation and sanitization of files that match
/// a configured format. The processor reads the input stream, validates blocks
/// using either a regex or block pattern, writes valid bytes to the output,
/// and replaces invalid blocks with a configured replacement.
///
/// Note: a new instance of this class is created per file (see <see cref="FileProcessorRegistry"/>).
/// </summary>
public class GenericFileProcessor : IFileProcessor
{
    private readonly FormatDefinition _def;
    private readonly Regex? _validBlockRegex;
    private readonly string _prefix;
    private readonly string _suffix;
    private readonly string _errorBlockReplacement;
    private readonly ILogger<GenericFileProcessor> _logger;

    /// <summary>
    /// Construct a file processor for the provided <paramref name="def"/>.
    /// The constructor reads processing parameters from <see cref="FormatDefinition.Spec"/>.
    /// </summary>
    public GenericFileProcessor(FormatDefinition def, ILogger<GenericFileProcessor> logger)
    {
        _def = def;

        _logger = logger;

        // Read format parameters from nested Spec (fall back to sensible defaults)
        var spec = def.Spec;
        _prefix = spec?.Prefix ?? string.Empty;
        _suffix = spec?.Suffix ?? string.Empty;
        var validPattern = spec?.ValidBlockRegex;
        _validBlockRegex = string.IsNullOrEmpty(validPattern) ? null : new Regex(validPattern, RegexOptions.Compiled);
        
        _errorBlockReplacement = spec?.ErrorBlockReplacement ?? string.Empty;

        _logger.LogInformation("GenericFileProcessor created for extension {Extension}", def.Extension);
    }


    /// <summary>
    /// Process the input stream and write sanitized output to <paramref name="output"/>.
    /// Returns a <see cref="ProcessResult"/> that indicates success or a processing error.
    /// </summary>
    public async Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct)
    {
        int replaced = 0;

        _logger.LogInformation("Processing started for extension {Extension}. IsKnown={IsKnown}", detected.Extension, detected.IsKnown);

        var one = new byte[1];

        // 1) consume and preserve leading whitespace
        int b;
        while (true)
        {
            b = await ReadByteOrEofAsync(input, one, ct);
            if (b == -1) return Fail(ProcessingErrorCode.EmptyFile, "Empty or whitespace-only.");
            if (!IsWs((byte)b)) break;

            one[0] = (byte)b;
            await output.WriteAsync(one.AsMemory(0, 1), ct);
        }

        // check prefix (allow empty prefix)
        var prefixBytes = System.Text.Encoding.ASCII.GetBytes(_prefix);
        int pending = -2; // -2 = no pending byte, otherwise holds a byte value to be consumed by body
        if (b != -1)
        {
                if (prefixBytes.Length > 0)
                {
                    if ((byte)b != prefixBytes[0])
                        return Fail(ProcessingErrorCode.InvalidHeader, $"First bytes must be '{_prefix}'.");

                    await output.WriteAsync(new byte[] { (byte)b }, ct);
                    for (int i = 1; i < prefixBytes.Length; i++)
                    {
                        int nb = await ReadByteOrEofAsync(input, one, ct);
                        if (nb == -1) return Fail(ProcessingErrorCode.TruncatedFile, "Truncated header.");
                        if ((byte)nb != prefixBytes[i])
                            return Fail(ProcessingErrorCode.InvalidHeader, $"Header must be '{_prefix}'.");

                        one[0] = (byte)nb;
                        await output.WriteAsync(one.AsMemory(0, 1), ct);
                    }
                }
            else
            {
                // no prefix: feed this byte into the body processing loop
                pending = b;
            }
        }

        // process body blocks until suffix
        var suffixBytes = System.Text.Encoding.ASCII.GetBytes(_suffix);
        // No fixed block length support anymore - blocks are determined by regex (validRegex or blockPattern)
        byte[]? initialBlockPrefix = null;
        while (true)
        {
            // skip and preserve whitespace between blocks
            int next = pending != -2 ? pending : await ReadByteOrEofAsync(input, one, ct);
            // clear pending after consuming
            pending = -2;

            if (next == -1)
            {
                // If no suffix is configured, EOF marks successful end of body
                if (suffixBytes.Length == 0)
                {
                    var report = new SanitizationReportDto(
                        WasMalicious: replaced > 0,
                        ReplacedBlocks: replaced,
                        Notes: replaced > 0 ? $"Replaced {replaced} invalid blocks." : "No invalid blocks found."
                    );
                    _logger.LogInformation("Processing finished for extension {Extension}. ReplacedBlocks={ReplacedBlocks}", detected.Extension, replaced);
                    return new ProcessResult(true, report, null);
                }

                return Fail(ProcessingErrorCode.InvalidFooter, $"Missing footer '{_suffix}'.");
            }

            while (next != -1 && IsWs((byte)next))
            {
                one[0] = (byte)next;
                await output.WriteAsync(one.AsMemory(0,1), ct);
                next = await ReadByteOrEofAsync(input, one, ct);
            }
            if (next == -1) return Fail(ProcessingErrorCode.InvalidFooter, $"Missing footer '{_suffix}'.");

            // check for suffix start (only if suffix is configured)
            if (suffixBytes.Length > 0 && (byte)next == suffixBytes[0])
            {
                // peek remaining suffix bytes
                var peek = new List<byte>();
                peek.Add((byte)next);
                bool truncated = false;
                for (int i = 1; i < suffixBytes.Length; i++)
                {
                    int p = await ReadByteOrEofAsync(input, one, ct);
                    if (p == -1) { truncated = true; break; }
                    peek.Add((byte)p);
                }
                if (truncated) return Fail(ProcessingErrorCode.TruncatedFile, $"Truncated footer (expected '{_suffix}').");

                    if (peek.SequenceEqual(suffixBytes))
                    {
                        // write footer
                        await output.WriteAsync(peek.ToArray(), ct);

                        // after footer only whitespace allowed
                        while (true)
                        {
                            int tail = await ReadByteOrEofAsync(input, one, ct);
                            if (tail == -1)
                            {
                                var report = new SanitizationReportDto(
                                    WasMalicious: replaced > 0,
                                    ReplacedBlocks: replaced,
                                    Notes: replaced > 0 ? $"Replaced {replaced} invalid blocks." : "No invalid blocks found."
                                );
                                _logger.LogInformation("Processing finished for extension {Extension}. ReplacedBlocks={ReplacedBlocks}", detected.Extension, replaced);
                                return new ProcessResult(true, report, null);
                            }

                            if (!IsWs((byte)tail))
                                return Fail(ProcessingErrorCode.TrailingData, $"Extra non-whitespace data after footer '{_suffix}'.");

                            one[0] = (byte)tail;
                            await output.WriteAsync(one.AsMemory(0,1), ct);
                        }
                    }

                // if not suffix, treat peeked bytes as start of block stream
                var toWrite = peek.ToArray();
                // We'll feed these bytes into the block accumulator by initializing blockBytes below with them.
                // Set `next` to first byte of the peek so processing continues using the variable-length logic.
                next = toWrite[0];
                // create an initial prefix array for block accumulation
                initialBlockPrefix = toWrite;
            }

            // variable-length block processing using either blockPattern or validRegex
            var blockBytes = new List<byte>();
            if (initialBlockPrefix != null)
            {
                blockBytes.AddRange(initialBlockPrefix);
                initialBlockPrefix = null;
            }
            else
            {
                blockBytes.Add((byte)next);
            }

            while (true)
            {
                var blockStr = System.Text.Encoding.ASCII.GetString(blockBytes.ToArray());

                // choose appropriate regex for full-match validation
                var regexToUse = _validBlockRegex ?? new Regex(".*", RegexOptions.Compiled);
                var m = regexToUse.Match(blockStr);
                if (m.Success && m.Length == blockStr.Length)
                {
                    await output.WriteAsync(blockBytes.ToArray().AsMemory(0, blockBytes.Count), ct);
                    break;
                }

                

                int nb = await ReadByteOrEofAsync(input, one, ct);
                if (nb == -1)
                    return Fail(ProcessingErrorCode.TruncatedFile, "Truncated block.");

                // if we encounter suffix start while accumulating block, treat the accumulated bytes
                // as an invalid block: write the replacement, increment counter, then re-process
                // the suffix start byte in the outer loop by saving it to `pending`.
                if (suffixBytes.Length > 0 && (byte)nb == suffixBytes[0])
                {
                    var repl = System.Text.Encoding.ASCII.GetBytes(_errorBlockReplacement);
                    await output.WriteAsync(repl.AsMemory(0, repl.Length), ct);
                    replaced++;
                    _logger.LogDebug("Replaced invalid block for extension {Extension}. TotalReplaced={Replaced}", detected.Extension, replaced);
                    pending = nb;
                    break;
                }

                blockBytes.Add((byte)nb);
            }
        }
    }

    /// <summary>
    /// Helper to create a failed <see cref="ProcessResult"/> with a <see cref="ProcessingError"/>.
    /// </summary>
    private static ProcessResult Fail(ProcessingErrorCode code, string message)
        => new(false, null, new ProcessingError(code, message));

    /// <summary>Returns true when the given byte is an ASCII whitespace character.</summary>
    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t';

    /// <summary>
    /// Read a single byte from <paramref name="s"/> returning -1 on EOF.
    /// This helper centralizes the EOF representation used by the processor.
    /// </summary>
    private static async Task<int> ReadByteOrEofAsync(Stream s, byte[] one, CancellationToken ct)
    {
        int n = await s.ReadAsync(one.AsMemory(0, 1), ct);
        return n == 0 ? -1 : one[0];
    }
}
