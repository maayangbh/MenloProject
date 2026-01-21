# File Sanitization Microservice (ABC Format)

This project implements a RESTful microservice in ASP.NET Core for validating and sanitizing uploaded files.  
The service detects the file format, validates its structure, sanitizes malicious content according to format-specific rules, and returns a clean version of the file to the client.

The architecture is designed to be **extensible** and **stream-based**, so additional file formats can be supported without changing the API contract.

---

## Features

- REST API for file upload and sanitization  
- Streaming processing (no full file buffering in memory)  
- Format detection based on file extension  
- Pluggable processor architecture (supports multiple formats)  
- Structured error responses
- Designed for large files and scalability  
- Clean API contract:  
  - `200 OK` → sanitized file  
  - `4xx` → structured JSON error  

---

## Supported File Format: ABC

The ABC format is a synthetic file format defined for this exercise to demonstrate structured parsing, validation, and sanitization logic.
# File Sanitization Microservice (Multi-Format)

This project implements a RESTful microservice in ASP.NET Core for validating and sanitizing uploaded files.
The service detects the file format, validates its structure, sanitizes malicious content according to format-specific rules, and returns a clean version of the file to the client.

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
  - Tests run automatically after build

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

## Build, Run, and Test (VS Code)

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

## Testing

### Valid file
```powershell
curl.exe -v -o out_ok.abc http://localhost:5037/sanitize -F "file=@MyFile.abc"
```

### Malicious file
```powershell
curl.exe -v -o out_sanitized.abc http://localhost:5037/sanitize -F "file=@MyVirus.abc"
```

### Invalid file
```powershell
curl.exe -v http://localhost:5037/sanitize -F "file=@BadHeader.abc"
```

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
FilesEndpoints
  |
  v
FileFormatDetector ──► DetectedFile
  |
  v
FileProcessorRegistry ──► IFileProcessor
  |
  v
GenericFileProcessor/AbcProcessor (stream-based)
  |
  v
Sanitized output stream
```

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
- Clean separation of concerns
- Extensible processor architecture
  
---

## Future Extensions
- Support additional file formats (just add to `formats.yaml`)
- Hot-reload format config
- More advanced block validation
- CI integration

---

## Scalability & Design Considerations
- Streaming input/output (no full buffering)

- Single-pass parsing and sanitization

- Minimal memory allocation per byte

- Temp file strategy for safe error handling

- Clean separation of concerns

- Extensible processor architecture
  
---

## Running the Service
powershell
```
dotnet run
```

Service will start on:

```
http://localhost:5037
```


## Testing
### Valid file

```
curl.exe -v -o out_ok.abc http://localhost:5037/sanitize -F "file=@MyFile.abc"
```

### Malicious file
```
curl.exe -v -o out_sanitized.abc http://localhost:5037/sanitize -F "file=@MyVirus.abc"
```

### Invalid file
```
curl.exe -v http://localhost:5037/sanitize -F "file=@BadHeader.abc"
```

---

## Future Extensions
- Support additional file formats

