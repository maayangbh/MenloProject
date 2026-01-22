
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

Formats are defined in `FileService.Api/Config/formats.yaml`. The project uses a compact schema where each format has a plain `extension` (without a leading dot), an optional `description`, and a nested `spec` block containing validation/sanitization rules.

Top-level fields:
- `description`: Short human-readable description (optional)
- `extension`: File extension without leading dot (e.g. `abc`)
- `spec`: Nested object with format-specific rules

Spec fields:
- `prefix`: Header bytes (string) required at file start (optional)
- `suffix`: Footer bytes (string) required at file end (optional)
- `validBlockRegex`: Regex that must fully match a block to be considered valid
- `errorBlockReplacement`: Replacement text for invalid blocks
- `blockPattern`: Alternative variable-block regex (optional)
# File Sanitization Microservice

This ASP.NET Core microservice validates and sanitizes uploaded files according to format definitions in `FileService.Api/Config/formats.yaml`. The service is streaming-first and extensible: most new formats can be added by editing `formats.yaml` with minimal code changes.

Target framework: .NET 8 (project builds for `net8.0`).

---

## Features

- REST API for file upload and sanitization
- Streaming processing (no full file buffering in memory)
- Format detection based on extension (configurable in `Config/formats.yaml`)
- Pluggable processor architecture (config-driven or custom processors)
- Structured error responses
- Automated tests included and runnable via `dotnet test`

---

## Build, Run, and Test

### Local (CLI / VS Code)

1. Build and run the service from the solution root or project folder:

```powershell
dotnet build
dotnet run --project FileService.Api/FileService.Api.csproj
```

2. By default the development `launchSettings.json` maps the app to `http://localhost:5037` (this can vary if you override `applicationUrl` or use a different profile).

3. Run tests:

```powershell
dotnet test
```

### Docker

1. Build the Docker image from the repository root:

```powershell
docker build -t fileservice-api .
```

2. Run the container exposing the default port:

```powershell
docker run -p 5037:5037 fileservice-api
```

3. The service will be accessible at `http://localhost:5037` unless you override the port mapping.

**Important:** ensure `Config/formats.yaml` is copied to the published output. In `FileService.Api/FileService.Api.csproj` include:

```xml
<ItemGroup>
  <None Update="Config\formats.yaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## API Endpoint

POST `/sanitize` — uploads a file and returns the sanitized bytes on success.


Example (PowerShell):

```powershell
curl.exe -v -o sanitized.abc http://localhost:5037/sanitize -F "file=@MyFile.abc"
```

Responses:
- `200 OK` — sanitized file (Content-Type: `application/octet-stream`)
- `4xx` — ProblemDetails JSON describing the error

---

## Manual Testing with Samples

Sample files are in `FileService.Api/Samples/` for local testing. To test with a sample file:

```powershell
curl.exe -v -o out_ok.abc http://localhost:5037/sanitize -F "file=@FileService.Api/Samples/MyFile.abc"
```

---

## Automated Tests

Tests are in the `FileService.Api.Tests/` project. From the solution root:

```powershell
dotnet test
```

---

## Error Semantics

Invalid input files are treated as client errors (HTTP 400). Typical categories:

- Empty file
- Invalid header (missing or malformed `prefix`)
- Invalid footer (missing or malformed `suffix`)
- Truncated file
- Invalid or malformed block
- Unexpected bytes in body
- Extra data after footer

All errors are returned as structured `ProblemDetails`.

---

## Architecture Overview

This section explains the request flow, responsibilities of the main components, and key runtime considerations (streaming, error handling, and configuration).

High-level request flow:

1. Client uploads a file to `POST /sanitize` (multipart/form-data).
2. `FilesEndpoints` receives the request, validates basic metadata, and streams the uploaded file to a temporary location or directly into a processor stream.
3. `FileFormatDetector` determines the candidate format (primarily by extension and the loaded `formats.yaml`).
4. `FileProcessorRegistry` resolves the appropriate processor implementation for the detected format.
5. A processor instance (usually `GenericFileProcessor` or a format-specific processor) performs a single-pass, streaming parse/validation/sanitization using the format `spec`.
6. The processor emits sanitized bytes to the response stream (or writes a sanitized temp file) and produces a `ProcessResult`/report on success or failure.
7. `FilesEndpoints` converts the processor result into an HTTP response: sanitized bytes on `200 OK`, or structured `ProblemDetails` for errors.

Key components and responsibilities:

- **FilesEndpoints**: HTTP layer; implementation in [FileService.Api/EndPoints/FilesEndpoints.cs](FileService.Api/EndPoints/FilesEndpoints.cs) — handles multipart parsing, temporary file lifecycle, streaming response, and converting `ProcessResult` into `ProblemDetails` or file attachments.
- **FileFormatDetector**: Format detection; implementation in [FileService.Api/Services/FileFormatDetector.cs](FileService.Api/Services/FileFormatDetector.cs) — uses `Config/formats.yaml` entries and basic heuristics to map an input file to a `FormatDefinition`.
- **FormatConfigLoader**: Config loader; implementation in [FileService.Api/Services/FormatConfigLoader.cs](FileService.Api/Services/FormatConfigLoader.cs) — loads/parses `Config/formats.yaml` at startup (or on demand) and exposes `FormatDefinition` objects.
- **FileProcessorRegistry**: Resolution layer; implementation in [FileService.Api/Services/FileProcessorRegistry.cs](FileService.Api/Services/FileProcessorRegistry.cs) — holds the mapping of format id/extension → processor factory and creates per-file processor instances.
- **GenericFileProcessor / Custom Processors**: Core sanitization logic; implementation in [FileService.Api/Services/GenericFileProcessor.cs](FileService.Api/Services/GenericFileProcessor.cs) (and any custom processors) — performs streaming validation, block-level regex checks, replacements, header/footer validation, and produces `ProcessResult`.

Runtime and design considerations:

- Streaming-first: processors operate on streams so large files are processed without full buffering in memory.
- Single-pass parsing: processors read the input once and write sanitized output as they go to minimize allocations and latency.
- Temp-file strategy: `FilesEndpoints` may use temporary files to simplify error handling and to avoid partial response emission on failure.
- Thread-safety & DI: `FileProcessorRegistry` and `FormatConfigLoader` are registered as singletons; processors are instantiated per-file to avoid shared mutable state.
- Configuration: keep `Config/formats.yaml` in the published output (see project file `CopyToOutputDirectory` settings) so runtime detection works inside containers.

Example request lifecycle (concise):

1. `POST /sanitize` with `file=@MyFile.abc`
2. Endpoint streams upload → `FileFormatDetector` returns `.abc` → registry resolves `ABC` processor
3. Processor validates header (`prefix`), iterates blocks, applies `validBlockRegex` or `blockPattern`, replaces invalid blocks, validates footer (`suffix`), streams sanitized bytes
4. Endpoint returns `200 OK` with `application/octet-stream` and an attachment filename, or `400` with `ProblemDetails` describing the failure

---



