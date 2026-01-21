
# File Sanitization Microservice

This project implements a RESTful microservice in ASP.NET Core for validating and sanitizing uploaded files. The service detects the file format, validates its structure, sanitizes malicious content according to format-specific rules, and returns a clean version of the file to the client.

The architecture is **extensible** and **stream-based**. Additional file formats and rules can be added by editing `FileService.Api/Config/formats.yaml`—no code changes required for most formats.

---

## Features

- REST API for file upload and sanitization
- Streaming processing (no full file buffering in memory)
- Format detection based on file extension (configurable in `formats.yaml`)
- Pluggable processor architecture (supports multiple formats, including regex/variable-length blocks)
- Structured error responses
- Designed for large files and scalability
- Clean API contract:
  - `200 OK` → sanitized file
  - `4xx` → structured JSON error
- Automated tests run with every build

---


## Supported File Formats

Formats are defined in `FileService.Api/Config/formats.yaml`. Each entry specifies:

- id: Format name (e.g. ABC, XYZ, XVAR)
- extension: File extension (e.g. .abc)
- prefix/suffix: Header/footer bytes (optional)
- blockLength: Fixed block size (optional)
- blockPattern: Regex for variable-length blocks (optional)
- validRegex: Regex for block validation (optional)
- replacement: Replacement for invalid blocks
- errorToken: (optional)
- notes: Description

### Example: ABC Format

```yaml
  - id: ABC
    contentType: text/plain
    extension: .abc
    prefix: "123"
    suffix: "789"
    blockLength: 3
    validRegex: "^A[1-9]C$"
    replacement: "A255C"
    errorToken: ""
    notes: "Built-in ABC rules"
```

### Example: Variable-length Block Format

```yaml
  - id: XVAR
    contentType: text/plain
    extension: .xvar
    prefix: "321"
    suffix: "987"
    blockLength: 0
    blockPattern: "^X\\d{2,}Y$"
    maxBlockLength: 4096
    validRegex: ""
    replacement: "X0Y"
    errorToken: ""
    notes: "Variable-length numeric blocks: XnnY where n is 2 or more digits"
```

---


## Build, Run, and Test

### Local (VS Code or CLI)

1. Open the folder in VS Code.
2. Build and run the service:
  ```powershell
  dotnet build
  dotnet run --project FileService.Api/FileService.Api.csproj
  ```
  Or use the VS Code Run/Debug panel.
3. The service will start at:
  ```
  http://localhost:5037
  ```
4. Tests run automatically after every build. To run manually:
  ```powershell
  dotnet test
  ```

### Docker

1. Build the Docker image:
  ```powershell
  docker build -t fileservice-api .
  ```
2. Run the container (exposing port 5037):
  ```powershell
  docker run -p 5037:5037 fileservice-api
  ```
3. The service will be available at:
  ```
  http://localhost:5037
  ```

**Important:**
If you add or update the file format configuration (formats.yaml), make sure it is copied to the output directory during build. In `FileService.Api/FileService.Api.csproj`, add:

```xml
  <ItemGroup>
   <None Update="Config\formats.yaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
   </None>
  </ItemGroup>
```

This ensures the config file is available in the Docker image. If you get errors about unknown or unsupported file extensions, check that formats.yaml is present in the published output and in the container at `/app/Config/formats.yaml`.

You can override the port or environment variables as needed using Docker's `-e` and `-p` options.

---

## API Endpoint

### `POST /sanitize`

Uploads a file, sanitizes it, and returns the sanitized file.

**Content-Type:**  
`multipart/form-data`

**Form field name:**  
`file`

Example using `curl` (PowerShell):

```powershell
curl.exe -v -o sanitized.abc http://localhost:5037/sanitize -F "file=@MyFile.abc"
```

---
### Responses

**Success Response**

  Status: 200 OK

  Content-Type: application/octet-stream

  Content-Disposition: attachment; filename="MyFile.sanitized.abc"

  Body: sanitized file bytes

**Error Response**

  Status: 400 Bad Request (or 413 Payload Too Large)

  Content-Type: application/problem+json

  Body: RFC-7807 ProblemDetails JSON

**Example:**

```json
{
  "type": "about:blank",
  "title": "Invalid ABC header",
  "status": 400,
  "detail": "First 3 bytes must be '123'.",
  "code": "InvalidHeader"
}
```

---


## Manual Testing with Sample Files

You can manually test the API using sample files or your own files. The sample files provided in `FileService.Api/Samples/` are for demonstration and local testing only—they are not accessible to remote clients.

To test, copy a sample file to your local machine or use your own file, then run:

### Valid file
```powershell
curl.exe -v -o out_ok.abc http://localhost:5037/sanitize -F "file=@C:/path/to/MyFile.abc"
```

### Malicious file
```powershell
curl.exe -v -o out_sanitized.abc http://localhost:5037/sanitize -F "file=@C:/path/to/MyVirus.abc"
```

### Invalid file
```powershell
curl.exe -v http://localhost:5037/sanitize -F "file=@C:/path/to/BadHeader.abc"
```

Replace `C:/path/to/` with the actual path to your file. The `-o out_ok.abc` or `-o out_sanitized.abc` option saves the sanitized output to a new file on your machine.

---

## Automated Tests

Automated tests are included and run with every build. These tests use test data and code in the `FileService.Api.Tests/` project and do not require manual file uploads.

To run all automated tests:
```powershell
dotnet test
```

### Example: Automated Unit Test

Here is an example of a unit test from `GenericFileProcessorTests.cs` that checks a private method using reflection:

```csharp
[Fact]
public void IsWs_ReturnsTrueForWhitespace()
{
  var type = typeof(GenericFileProcessor);
  var method = type.GetMethod("IsWs", BindingFlags.NonPublic | BindingFlags.Static);
  Assert.NotNull(method);
  Assert.True((method!.Invoke(null, new object[] { (byte)' ' }) as bool?) == true);
  Assert.True((method!.Invoke(null, new object[] { (byte)'\n' }) as bool?) == true);
  Assert.True((method!.Invoke(null, new object[] { (byte)'\r' }) as bool?) == true);
  Assert.True((method!.Invoke(null, new object[] { (byte)'\t' }) as bool?) == true);
  Assert.False((method!.Invoke(null, new object[] { (byte)'A' }) as bool?) == true);
}
```
This test verifies that the internal whitespace-checking logic works as expected.

---

## Error Semantics
Invalid input files are treated as client errors and return HTTP 400.

Typical error categories:

- Empty file
- Invalid header (prefix missing or malformed)
- Invalid footer (suffix missing or malformed)
- Truncated file
- Invalid or malformed block
- Unexpected byte in body
- Extra data after footer

All errors are returned as structured ProblemDetails.

---

## Architecture Overview

```
Client
  |
  | POST /sanitize (multipart/form-data)
  |
  v
FilesEndpoints (API layer)
  |
  v
FileFormatDetector (detects format from config)
  |
  v
FileProcessorRegistry (maps format to processor)
  |
  v
┌───────────────────────────────┐
│  GenericFileProcessor         │
│  (config-driven, supports     │
│   fixed/variable/regex blocks)│
│  or custom processor (e.g.    │
│   AbcProcessor)               │
└───────────────────────────────┘
  |
  v
Sanitized output stream
```

This architecture is fully extensible: new formats and rules can be added via `formats.yaml` without code changes for most cases. The processor layer supports both config-driven and custom logic.

---

## Key Components

- **FilesEndpoints.cs**: Defines the /sanitize API endpoint, handles file upload, manages temp file lifecycle, converts processor results into HTTP responses, returns ProblemDetails on failure
- **FileFormatDetector**: Detects the file format based on file extension, returns a DetectedFile object, designed to support additional formats in the future
- **FileProcessorRegistry**: Maps format IDs (e.g., "ABC") to processors, enables pluggable format support
- **IFileProcessor**: Interface for all file processors
- **GenericFileProcessor/AbcProcessor**: Implements format rules, fully streaming, sanitizes malicious blocks, validates header/body/footer, never throws on invalid file content, returns structured ProcessResult failures
- **ProcessResult**: Represents the result of file processing
- **SanitizationReportDto**: Returned on successful sanitization

---

## Scalability & Design Considerations
- Streaming input/output (no full buffering)
- Single-pass parsing and sanitization
- Minimal memory allocation per byte
- Temp file strategy for safe error handling
- Extensible processor architecture
  
---

## Future Extensions

The project can be further developed in several directions:

- **Support additional file formats** (just add to `formats.yaml`)
- **Hot-reload format config**: Allow the service to reload `formats.yaml` without restarting
- **Plugin system**: Allow custom processors to be loaded as plugins for advanced or proprietary formats

---

## Scalability & Design Considerations
- Streaming input/output (no full buffering)

- Single-pass parsing and sanitization

- Minimal memory allocation per byte

- Temp file strategy for safe error handling

- Extensible processor architecture
  
---


