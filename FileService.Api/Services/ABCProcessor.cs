using FileService.Api.Dtos;

namespace FileService.Api.Services;

public class AbcProcessor : IFileProcessor
{
    public string FormatId => "ABC";

    public ProcessResult Process(byte[] input, DetectedFile detected)
    {
        // Validate structure
        if (!TryGetContentBounds(input, out int start, out int end, out var boundsErr))
            return Fail(boundsErr);

        if (end - start + 1 < 6)
            return Fail("Invalid ABC file: too short to contain header '123' and footer '789'.");

        // Validate header: first 3 bytes == 123
        if (input[start] != (byte)'1' || input[start + 1] != (byte)'2' || input[start + 2] != (byte)'3')
            return Fail("Invalid ABC file: first 3 bytes must be '123'.");

        // Validate footer: last 3 bytes == 789
        if (input[end - 2] != (byte)'7' || input[end - 1] != (byte)'8' || input[end] != (byte)'9')
            return Fail("Invalid ABC file: last 3 bytes must be '789'.");

        var output = new List<byte>(input.Length + input.Length / 10); // extra space for replacements. Assume ~10% bad blocks.
        int replaced = 0;


        // Sanitization pass
        // Copy leading whitespace unchanged
        for (int i = 0; i < start; i++) output.Add(input[i]);

        // Copy header unchanged
        output.Add((byte)'1'); output.Add((byte)'2'); output.Add((byte)'3');

        int iBody = start + 3;
        int bodyEnd = end - 3; // inclusive index of last byte before footer begins

        while (iBody <= bodyEnd)
        {
            // 1) Skip whitespace between blocks
            while (iBody <= bodyEnd && IsWs(input[iBody]))
            {
                output.Add(input[iBody]); // keep formatting
                iBody++;
            }

            if (iBody > bodyEnd)
                break; // body ended after whitespace

            // 2) Now we must see a full block starting at iBody
            if (input[iBody] != (byte)'A')
                return Fail($"Invalid ABC file: unexpected byte '{(char)input[iBody]}' in body. Expected start of block 'A'.");

            // Need exactly two more bytes for A ? C
            if (iBody + 2 > bodyEnd)
                return Fail("Invalid ABC file: truncated A*C block.");

            byte mid = input[iBody + 1];
            byte last = input[iBody + 2];

            // Assume: no whitespace allowed inside block
            if (IsWs(mid) || IsWs(last))
                return Fail("Invalid ABC file: whitespace is not allowed inside an A*C block.");

            if (last != (byte)'C')
                return Fail("Invalid ABC file: malformed block (expected 'C' as third byte in A*C).");

            bool ok = mid >= (byte)'1' && mid <= (byte)'9';
            if (ok)
            {
                output.Add((byte)'A'); output.Add(mid); output.Add((byte)'C');
            }
            else
            {
                output.Add((byte)'A'); output.Add((byte)'2'); output.Add((byte)'5'); output.Add((byte)'5'); output.Add((byte)'C');
                replaced++;
            }

            iBody += 3;
        }

        // Copy footer
        output.Add((byte)'7'); output.Add((byte)'8'); output.Add((byte)'9');

        // Copy trailing whitespace unchanged
        for (int i = end + 1; i < input.Length; i++) output.Add(input[i]);

        var report = new SanitizationReportDto(
            WasMalicious: replaced > 0,
            ReplacedBlocks: replaced,
            Notes: replaced > 0
                ? "Replaced invalid ABC blocks (A*C where * not 1-9) with A255C."
                : "No invalid ABC blocks found."
        );

        return new ProcessResult(
            Success: true,
            SanitizedBytes: output.ToArray(),
            Report: report,
            ErrorMessage: null
        );
    }

    private static ProcessResult Fail(string message) =>
        new(Success: false, SanitizedBytes: null, Report: null, ErrorMessage: message);

    private static bool TryGetContentBounds(byte[] input, out int start, out int end, out string error)
    {
        start = 0;
        while (start < input.Length && IsWs(input[start])) start++;

        end = input.Length - 1;
        while (end >= 0 && IsWs(input[end])) end--;

        if (start >= input.Length || end < 0)
        {
            error = "Invalid ABC file: empty or whitespace-only.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t';
}
