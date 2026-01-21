using FileService.Api.Dtos;
using System.Text.RegularExpressions;

namespace FileService.Api.Services;

public class GenericFileProcessor : IFileProcessor
{
    private readonly FormatDefinition _def;
    private readonly Regex _validRegex;
    private readonly Regex? _blockPatternRegex;
    private readonly int _maxBlockLength;

    public GenericFileProcessor(FormatDefinition def)
    {
        _def = def;
        _validRegex = new Regex(def.ValidRegex, RegexOptions.Compiled);
        _blockPatternRegex = string.IsNullOrEmpty(def.BlockPattern) ? null : new Regex(def.BlockPattern, RegexOptions.Compiled);
        _maxBlockLength = def.MaxBlockLength > 0 ? def.MaxBlockLength : 4096;
    }

    public string FormatId => _def.Id;

    public async Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct)
    {
        int replaced = 0;

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
        var prefixBytes = System.Text.Encoding.ASCII.GetBytes(_def.Prefix);
        int pending = -2; // -2 = no pending byte, otherwise holds a byte value to be consumed by body
        if (b != -1)
        {
            if (prefixBytes.Length > 0)
            {
                if ((byte)b != prefixBytes[0])
                    return Fail(ProcessingErrorCode.InvalidHeader, $"First bytes must be '{_def.Prefix}'.");

                await output.WriteAsync(new byte[] { (byte)b }, ct);
                for (int i = 1; i < prefixBytes.Length; i++)
                {
                    int nb = await ReadByteOrEofAsync(input, one, ct);
                    if (nb == -1) return Fail(ProcessingErrorCode.TruncatedFile, "Truncated header.");
                    if ((byte)nb != prefixBytes[i])
                        return Fail(ProcessingErrorCode.InvalidHeader, $"Header must be '{_def.Prefix}'.");

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
        var suffixBytes = System.Text.Encoding.ASCII.GetBytes(_def.Suffix);
        var buffer = new byte[_def.BlockLength];

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
                    return new ProcessResult(true, report, null);
                }

                return Fail(ProcessingErrorCode.InvalidFooter, $"Missing footer '{_def.Suffix}'.");
            }

            while (next != -1 && IsWs((byte)next))
            {
                one[0] = (byte)next;
                await output.WriteAsync(one.AsMemory(0,1), ct);
                next = await ReadByteOrEofAsync(input, one, ct);
            }
            if (next == -1) return Fail(ProcessingErrorCode.InvalidFooter, $"Missing footer '{_def.Suffix}'.");

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
                if (truncated) return Fail(ProcessingErrorCode.TruncatedFile, $"Truncated footer (expected '{_def.Suffix}').");

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
                            return new ProcessResult(true, report, null);
                        }

                        if (!IsWs((byte)tail))
                            return Fail(ProcessingErrorCode.TrailingData, $"Extra non-whitespace data after footer '{_def.Suffix}'.");

                        one[0] = (byte)tail;
                        await output.WriteAsync(one.AsMemory(0,1), ct);
                    }
                }

                // if not suffix, treat peeked bytes as start of block stream
                var toWrite = peek.ToArray();
                // process first byte as part of block detection below by feeding it back
                using var ms = new MemoryStream(toWrite);
                ms.Position = 0;
                for (int i = 0; i < toWrite.Length; i++)
                {
                    buffer[0] = toWrite[i];
                    // fall through to normal processing below - but for simplicity we'll continue reading block normally
                }
                // continue to block processing (we'll just proceed)
            }

            // if a block pattern is defined, read variable-length block until it matches
            if (_blockPatternRegex != null)
            {
                var blockBytes = new List<byte>();
                blockBytes.Add((byte)next);

                while (true)
                {
                    var blockStr = System.Text.Encoding.ASCII.GetString(blockBytes.ToArray());

                    // full-match check: ensure regex matches entire block
                    var m = _blockPatternRegex.Match(blockStr);
                    if (m.Success && m.Length == blockStr.Length)
                    {
                        // error token check
                        if (!string.IsNullOrEmpty(_def.ErrorToken) && blockStr.Contains(_def.ErrorToken))
                            return Fail(ProcessingErrorCode.InvalidBlock, $"Error token '{_def.ErrorToken}' encountered.");

                        await output.WriteAsync(blockBytes.ToArray().AsMemory(0, blockBytes.Count), ct);
                        break;
                    }

                    if (blockBytes.Count > _maxBlockLength)
                    {
                        // too long without matching -> treat as invalid block and replace
                        var repl = System.Text.Encoding.ASCII.GetBytes(_def.Replacement);
                        await output.WriteAsync(repl.AsMemory(0, repl.Length), ct);
                        replaced++;
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
                        var repl = System.Text.Encoding.ASCII.GetBytes(_def.Replacement);
                        await output.WriteAsync(repl.AsMemory(0, repl.Length), ct);
                        replaced++;
                        pending = nb;
                        break;
                    }

                    blockBytes.Add((byte)nb);
                }
            }
            else
            {
                // otherwise expect a block of fixed length
                buffer[0] = (byte)next;
                int read = 1;
                while (read < buffer.Length)
                {
                    int nb = await ReadByteOrEofAsync(input, one, ct);
                    if (nb == -1) return Fail(ProcessingErrorCode.TruncatedFile, "Truncated block.");
                    buffer[read++] = (byte)nb;
                }

                var blockStr = System.Text.Encoding.ASCII.GetString(buffer);

                // reject whitespace inside fixed-length blocks (consistent with AbcProcessor)
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (IsWs(buffer[i]))
                        return Fail(ProcessingErrorCode.InvalidBlock, "Whitespace is not allowed inside a block.");
                }

                // error token check
                if (!string.IsNullOrEmpty(_def.ErrorToken) && blockStr.Contains(_def.ErrorToken))
                {
                    return Fail(ProcessingErrorCode.InvalidBlock, $"Error token '{_def.ErrorToken}' encountered.");
                }

                if (_validRegex.IsMatch(blockStr))
                {
                    await output.WriteAsync(buffer.AsMemory(0, buffer.Length), ct);
                }
                else
                {
                    var repl = System.Text.Encoding.ASCII.GetBytes(_def.Replacement);
                    await output.WriteAsync(repl.AsMemory(0, repl.Length), ct);
                    replaced++;
                }
            }
        }
    }

    private static ProcessResult Fail(ProcessingErrorCode code, string message)
        => new(false, null, new ProcessingError(code, message));

    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t';

    private static async Task<int> ReadByteOrEofAsync(Stream s, byte[] one, CancellationToken ct)
    {
        int n = await s.ReadAsync(one.AsMemory(0, 1), ct);
        return n == 0 ? -1 : one[0];
    }
}
