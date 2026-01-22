using FileService.Api.Dtos;

namespace FileService.Api.Services;

public class AbcProcessor : IFileProcessor
{

    public async Task<ProcessResult> ProcessAsync(Stream input, Stream output, DetectedFile detected, CancellationToken ct)
    {
        int replaced = 0;

        var one = new byte[1];

        // 1) Consume and preserve leading whitespace (optional; you can also reject/ignore it)
        // We'll write it through unchanged.
        int b;
        while (true)
        {
            b = await ReadByteOrEofAsync(input, one, ct);
            if (b == -1) return Fail(ProcessingErrorCode.EmptyFile, "Empty or whitespace-only.");
            if (!IsWs((byte)b)) break;

            one[0] = (byte)b;
            await output.WriteAsync(one.AsMemory(0, 1), ct);
        }

        // 2) Now b is first non-ws byte. Header must be '1','2','3' consecutively.
        if (b != '1') return Fail(ProcessingErrorCode.InvalidHeader, "First 3 bytes must be '123'.");

        int b2 = await ReadByteOrEofAsync(input, one, ct);
        int b3 = await ReadByteOrEofAsync(input, one, ct);

        if (b2 == -1 || b3 == -1)
            return Fail(ProcessingErrorCode.TruncatedFile, "Truncated header (expected '123').");

        if (b2 != '2' || b3 != '3')
            return Fail(ProcessingErrorCode.InvalidHeader, "First 3 bytes must be '123'.");

        // Write header
        await output.WriteAsync(new byte[] { (byte)'1', (byte)'2', (byte)'3' }, ct);

        // 3) Process body until we hit footer '789'
        // Body rule: whitespace between blocks allowed; otherwise must see blocks A?C.
        // Footer detection: when we see '7', we check next two bytes are '8' and '9',
        // then only allow trailing whitespace until EOF.
        while (true)
        {
            // Skip (and preserve) whitespace between blocks
            int next = await ReadByteOrEofAsync(input, one, ct);
            if (next == -1) return Fail(ProcessingErrorCode.InvalidFooter, "Missing footer '789'.");

            while (next != -1 && IsWs((byte)next))
            {
                one[0] = (byte)next;
                await output.WriteAsync(one.AsMemory(0, 1), ct);
                next = await ReadByteOrEofAsync(input, one, ct);
            }
            if (next == -1) return Fail(ProcessingErrorCode.InvalidFooter, "Missing footer '789'.");

            // Footer?
            if (next == '7')
            {
                int f2 = await ReadByteOrEofAsync(input, one, ct);
                int f3 = await ReadByteOrEofAsync(input, one, ct);

                if (f2 == -1 || f3 == -1)
                    return Fail(ProcessingErrorCode.TruncatedFile, "Truncated footer (expected '789').");

                if (f2 != '8' || f3 != '9')
                    return Fail(ProcessingErrorCode.InvalidFooter, "Last 3 bytes must be '789'.");

                // Write footer
                await output.WriteAsync(new byte[] { (byte)'7', (byte)'8', (byte)'9' }, ct);

                // After footer: only whitespace allowed until EOF (preserve it)
                while (true)
                {
                    int tail = await ReadByteOrEofAsync(input, one, ct);
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
                        return Fail(ProcessingErrorCode.TrailingData, "Extra non-whitespace data after footer '789'.");

                    one[0] = (byte)tail;
                    await output.WriteAsync(one.AsMemory(0, 1), ct);
                }
            }

            // Otherwise must be the start of a block: 'A'
            if (next != 'A')
                return Fail(ProcessingErrorCode.InvalidBlock, $"Unexpected byte '{(char)next}' in body. Expected 'A' or footer '789'.");

            int mid = await ReadByteOrEofAsync(input, one, ct);
            int last = await ReadByteOrEofAsync(input, one, ct);

            if (mid == -1 || last == -1)
                return Fail(ProcessingErrorCode.TruncatedFile, "Truncated block (expected A*C).");

            // No whitespace inside blocks
            if (IsWs((byte)mid) || IsWs((byte)last))
                return Fail(ProcessingErrorCode.InvalidBlock, "Whitespace is not allowed inside an A*C block.");
            if (last != 'C')
                return Fail(ProcessingErrorCode.InvalidBlock, "Malformed block (expected 'C' as third byte in A*C).");

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

    private static ProcessResult Fail(ProcessingErrorCode code,string message)
        => new(false, null, new ProcessingError(code, message));
    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t';

    private static async Task<int> ReadByteOrEofAsync(Stream s, byte[] one, CancellationToken ct)
    {
        int n = await s.ReadAsync(one.AsMemory(0, 1), ct);
        return n == 0 ? -1 : one[0];
    }
}
