using FileService.Api.Dtos;

namespace FileService.Api.Services;

public class AbcProcessor : IFileProcessor
{
    public string FormatId => "ABC";

    public async Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct)
    {
        int replaced = 0;

        // 1) Consume and preserve leading whitespace (optional; you can also reject/ignore it)
        // We'll write it through unchanged.
        int b;
        while (true)
        {
            b = await ReadByteOrEofAsync(input, ct);
            if (b == -1) return Fail("Invalid ABC file: empty or whitespace-only.");
            if (!IsWs((byte)b)) break;

            await output.WriteAsync(new[] { (byte)b }, ct);
        }

        // 2) Now b is first non-ws byte. Header must be '1','2','3' consecutively.
        if (b != '1') return Fail("Invalid ABC file: first 3 bytes must be '123'.");

        int b2 = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated header (expected '123').", ct);
        int b3 = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated header (expected '123').", ct);

        if (b2 != '2' || b3 != '3')
            return Fail("Invalid ABC file: first 3 bytes must be '123'.");

        // Write header
        await output.WriteAsync(new byte[] { (byte)'1', (byte)'2', (byte)'3' }, ct);

        // 3) Process body until we hit footer '789'
        // Body rule: whitespace between blocks allowed; otherwise must see blocks A?C.
        // Footer detection: when we see '7', we check next two bytes are '8' and '9',
        // then only allow trailing whitespace until EOF.
        while (true)
        {
            // Skip (and preserve) whitespace between blocks
            int next = await ReadByteOrEofAsync(input, ct);
            if (next == -1) return Fail("Invalid ABC file: missing footer '789'.");

            while (next != -1 && IsWs((byte)next))
            {
                await output.WriteAsync(new[] { (byte)next }, ct);
                next = await ReadByteOrEofAsync(input, ct);
            }
            if (next == -1) return Fail("Invalid ABC file: missing footer '789'.");

            // Footer?
            if (next == '7')
            {
                int f2 = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated footer (expected '789').", ct);
                int f3 = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated footer (expected '789').", ct);

                if (f2 != '8' || f3 != '9')
                    return Fail("Invalid ABC file: last 3 bytes must be '789'.");

                // Write footer
                await output.WriteAsync(new byte[] { (byte)'7', (byte)'8', (byte)'9' }, ct);

                // After footer: only whitespace allowed until EOF (preserve it)
                while (true)
                {
                    int tail = await ReadByteOrEofAsync(input, ct);
                    if (tail == -1)
                    {
                        var report = new SanitizationReportDto(
                            WasMalicious: replaced > 0,
                            ReplacedBlocks: replaced,
                            Notes: replaced > 0
                                ? "Replaced invalid ABC blocks (A*C where * not 1-9) with A255C."
                                : "No invalid ABC blocks found."
                        );
                        return new ProcessResult(true, report, null);
                    }

                    if (!IsWs((byte)tail))
                        return Fail("Invalid ABC file: extra non-whitespace data after footer '789'.");

                    await output.WriteAsync(new[] { (byte)tail }, ct);
                }
            }

            // Otherwise must be the start of a block: 'A'
            if (next != 'A')
                return Fail($"Invalid ABC file: unexpected byte '{(char)next}' in body. Expected 'A' or footer '789'.");

            int mid = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated block (expected A*C).", ct);
            int last = await ReadRequiredByteAsync(input, "Invalid ABC file: truncated block (expected A*C).", ct);

            // No whitespace inside blocks
            if (IsWs((byte)mid) || IsWs((byte)last))
                return Fail("Invalid ABC file: whitespace is not allowed inside an A*C block.");

            if (last != 'C')
                return Fail("Invalid ABC file: malformed block (expected 'C' as third byte in A*C).");

            // sanitize if mid not '1'..'9'
            if (mid >= '1' && mid <= '9')
            {
                await output.WriteAsync(new byte[] { (byte)'A', (byte)mid, (byte)'C' }, ct);
            }
            else
            {
                await output.WriteAsync(new byte[] { (byte)'A', (byte)'2', (byte)'5', (byte)'5', (byte)'C' }, ct);
                replaced++;
            }
        }
    }

    private static ProcessResult Fail(string message)
        => new(false, null, new ProcessingError(ProcessingErrorCode.InvalidFormat, message));
    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t';

    private static async Task<int> ReadByteOrEofAsync(Stream s, CancellationToken ct)
    {
        var buf = new byte[1];
        int n = await s.ReadAsync(buf, 0, 1, ct);
        return n == 0 ? -1 : buf[0];
    }

    private static async Task<int> ReadRequiredByteAsync(Stream s, string errorMessage, CancellationToken ct)
    {
        int b = await ReadByteOrEofAsync(s, ct);
        if (b == -1) throw new InvalidDataException(errorMessage);
        return b;
    }
}
