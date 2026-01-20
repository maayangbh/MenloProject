# 🧼 File Sanitization Microservice (ABC Format)

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

### ABC Format Specification

An ABC file must follow this structure:

[HEADER]

[BODY]

[FOOTER]

**Header**

- The first 3 bytes must be: 
123

**Body**

- The body consists of zero or more blocks.
- Each block must be exactly 3 bytes: 
A*C

where `*` is a single byte between `'1'` and `'9'`.

- Assuming: Whitespace (`' '`, `'\n'`, `'\r'`, `'\t'`) is allowed **between** blocks,  
  but **not inside** a block.

**Footer**

- The last 3 bytes must be:

789

---

## Malicious Block Rules

If a block contains a byte other than `'1'`–`'9'`, between 'A' and 'C', the file is considered malicious.

Example malicious block:

AFC

Such blocks are sanitized by replacing them with:

A255C

---

## Examples

### Benign File

123
A1CA3CA7C
A2C
A5C
789

➡ Output: identical to input.

---

### Malicious File

123
A1CA3CAFC
789

➡ Sanitized Output:

123
A1CA3CA255C
789

---

## API Endpoint

### `POST /sanitize`

Uploads a file, sanitizes it, and returns the sanitized file.

---

### Request

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

json
Copy code
{

  "type": "about:blank",
  
  "title": "Invalid ABC header",
  
  "status": 400,
  
  "detail": "First 3 bytes must be '123'.",
  
  "code": "InvalidHeader"
  
}

---

## Error Semantics
Invalid input files are treated as client errors and return HTTP 400.

Typical error categories:

- Empty file

- Invalid header (123 missing or malformed)

- Invalid footer (789 missing or malformed)

- Truncated file

- Invalid or malformed block

- Unexpected byte in body

- Extra data after footer

All errors are returned as structured ProblemDetails.

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
AbcProcessor (stream-based)
  |
  v
Sanitized output stream
```


## Key Components

**FilesEndpoints.cs**

- Defines the /sanitize API endpoint

- Handles file upload

- Manages temp file lifecycle

- Converts processor results into HTTP responses

- Returns ProblemDetails on failure

**FileFormatDetector**
- Detects the file format based on file extension

- Returns a DetectedFile object

- Designed to support additional formats in the future

**FileProcessorRegistry**

- Maps format IDs (e.g., "ABC") to processors

- Enables pluggable format support

**IFileProcessor**

- Interface for all file processors.

**AbcProcessor**

- Implements the ABC format rules

- Fully streaming implementation

- Sanitizes malicious blocks

- Validates header, body, and footer

- Never throws on invalid file content

- Returns structured ProcessResult failures

**ProcessResult**

- Represents the result of file processing.

**SanitizationReportDto**
- Returned on successful sanitization.

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


