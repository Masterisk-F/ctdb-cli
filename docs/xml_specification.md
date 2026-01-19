# ctdb-cli Output XML Specification

Output format when specifying the `--xml` option in `ctdb-cli`.

## Common Specifications
- **Character Encoding**: UTF-8
- **Output**: XML data is output to standard output (stdout). Progress and error logs are output to standard error (stderr).
- **Namespaces**: The root element `ctdb` belongs to `http://db.cuetools.net/ns/mmd-1.0#`.
- **Error Handling**: Even if an error occurs during execution, `null` is not returned. Instead, XML is output with the error details included in the `status` attribute of each command's result element (e.g., `verify_result`).

## 1. lookup command
Outputs the XML returned from the CTDB `lookup2.php` endpoint as is.

**Example:**
```xml
<ctdb xmlns="http://db.cuetools.net/ns/mmd-1.0#">
  <entry id="..." crc32="..." ... />
  <metadata source="..." artist="..." album="...">
    ...
  </metadata>
</ctdb>
```

## 2. calc command
Outputs parity calculation results.

**Structure:**
- `calc_result`: Root of the calculation result
    - `@status`: Status (`success` or `failure`)
    - `@message`: Error message (only on `failure`)
    - `@toc_id`: TOC ID
    - `@ctdb_crc`: Disc-wide CRC (8-digit hexadecimal)
    - `track`: Information for each track
        - `@number`: Track number
        - `@crc`: AccurateRip CRC
        - `@crc32`: CRC32

## 3. verify command
Outputs detailed verification results.

**Structure:**
- `verify_result`: Root of the verification result
    - `@toc`: Target TOC ID
    - `@status`: Verification status (`found`, `not_found`, or `failure`)
    - `@message`: Error message (only on `failure`)
    - `@confidence`: Overall confidence
    - `@total_entries`: Number of entries found in the DB
    - `entry`: Comparison results with each DB entry
        - `@id`: Entry index
        - `@conf`: Confidence of that entry
        - `@crc`: CRC of that entry
        - `@offset`: Offset
        - `@status`: Verification status for that entry (string from CUETools)
        - `@has_errors`: Whether errors exist
        - `@can_recover`: Whether it's recoverable
        - `track`: Comparison results for each track
            - `@number`: Track number
            - `@local_crc`: CRC of the local file
            - `@remote_crc`: CRC on the DB side
            - `@matched`: Whether they matched (true/false)
        - `repair`: Repair information (only if recoverable)
            - `@correctable_errors`: Number of correctable errors
            - `@affected_sectors`: Affected sectors

## 4. submit command
Outputs submission content and results.

**Structure:**
- `submit_result`: Root of the submission result
    - `@status`: Overall status (`submitted`, `dry_run`, or `failure`)
    - `@message`: Error message (only on `failure`)
    - `submitted_metadata`: Metadata that was submitted
        - `@artist`, `@title`, `@barcode`, `@drive`, `@quality`
    - `response`: Parsed response from the API
        - `@status`: Submission status (`submitted`, `dry_run`, or `failure`)
        - `@message`: Message
        - `@parity_needed`: Whether parity file upload is required
    - `raw_response`: Re-serialized XML from the CTDB `submit2.php` response (may not exactly match the original response)

## 5. repair command
Outputs the results of the repair process.

**Structure:**
- `repair_result`: Root of the repair result
    - `@status`: Status (`repaired`, `clean`, `unrecoverable`, or `failure`)
    - `@message`: Error message (only on `failure`)
    - `@output_path`: Output file path
    - `@samples_written`: Number of samples written
    - `entry`: List of DB entries. Same structure as `verify_result`-`entry`, but with the following element added:
        - `@used_for_repair`: Whether that entry was actually used for repair (true/false)
