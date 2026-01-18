# ctdb-cli

A Linux command-line tool that interacts with CTDB (CUETools Database) to perform metadata retrieval, parity calculation, verification, and uploading using CUE sheets and audio files.

## Features
- AccurateRip / CTDB parity calculation from CUE + WAV
- Uploading metadata and parity to CTDB
- Support for Linux (.NET 8.0) environment

## Setup

This tool depends on libraries from [cuetools.net](https://github.com/gchudov/cuetools.net).
To ensure build compatibility on Linux, dependencies are fetched and patched via a setup script rather than using submodules.

### 1. Fetch Dependencies and Apply Patches

Run the following script to fetch the cuetools.net source code and apply patches for Linux build compatibility.

```bash
./setup.sh
```

> **Why Patches Are Needed**
> - **Freedb.csproj**: The original project file uses an old format that is not correctly handled by `dotnet build` on Linux, so it is converted to the SDK-style format.
> - **TagLib**: The original `CUESheet.cs` depends on some properties (like `AudioSampleCount`) that are missing in the current TagLib Sharp, so a fix is applied to compensate for this.

### 2. Build

#### Test Build

```bash
dotnet build CTDB.CLI/CTDB.CLI.csproj
```

#### Distribution Build (Framework-dependent)
Lightweight version.

```bash
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -o publish/dependent
```

#### Distribution Build (Self-contained / Single File)
Works even if .NET is not installed on the target machine, but the file size will be larger.

```bash
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/standalone
```

## Usage

Use `dotnet run` or the built binary.

```bash
ctdb-cli <command> <cue_file>
```

```bash
dotnet run --project CTDB.CLI/CTDB.CLI.csproj -- <command> <cue_file>
```


### Commands

#### 1. Lookup
Searches for metadata from CTDB based on the CUE sheet and displays Raw XML.
```bash
ctdb-cli lookup test.cue
```

#### 2. Calc
Reads local audio files and calculates AccurateRip CRC and CTDB CRC.
```bash
ctdb-cli calc test.cue
```

#### 3. Verify
Matches data on CTDB with local files to verify the accuracy of the rip.
```bash
ctdb-cli verify test.cue
```

#### 4. Upload
Sends calculated parity and metadata to CTDB.
**Please execute this only when you are highly confident that the data is correct.**
```bash
ctdb-cli upload test.cue
```
