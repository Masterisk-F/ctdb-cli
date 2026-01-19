# ctdb-cli

A Linux command-line tool that interacts with CTDB (CUETools Database) to perform metadata retrieval, parity calculation, verification, repair, and uploading using CUE sheets and audio files.

## Features
1. CTDB parity calculation from CUE + WAV
2. Error repair using CTDB parity data
3. Submitting metadata and parity to CTDB
4. Support for Linux (.NET 8.0) environment

## Setup

This tool depends on libraries from [cuetools.net](https://github.com/gchudov/cuetools.net).

### Build and Install

```bash
./configure
make
sudo make install
```

Options:
- `./configure --prefix=/usr` - Change install prefix (default: `/usr/local`)
- `make DESTDIR=/path/to/dest install` - Specify DESTDIR for packaging

### Development Build

Fetch dependencies and apply patches:
```bash
./setup.sh
```

> **Why Patches Are Needed**
> - **Freedb.csproj**: The original project file uses an old format that is not correctly handled by `dotnet build` on Linux, so it is converted to the SDK-style format.
> - **TagLib**: The original `CUESheet.cs` depends on some properties (like `AudioSampleCount`) that are missing in the current TagLib Sharp, so a fix is applied to compensate for this.

Build:
```bash
# Test build
dotnet build CTDB.CLI/CTDB.CLI.csproj

# Framework-dependent (lightweight)
dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -o publish/dependent

# Self-contained / Single File
# Works even if .NET is not installed on the target machine, but the file size will be larger.
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

#### 4. Repair
Uses CTDB parity data to repair errors in a ripped file.
The repaired file is saved as `original_filename_repaired.wav`.
```bash
ctdb-cli repair test.cue
```

> **Note**: Repair requires sufficient parity data in CTDB.
> If repair is not possible, an error message will be displayed.

#### 5. Submit
Submits the calculated parity and metadata to CTDB.

**Only execute this command if you are highly confident that the data is correct.**

```bash
ctdb-cli submit test.cue --drive "Drive Name" --quality 100
```

| Argument | Required | Description |
|----------|----------|-------------|
| `--drive` | ✓ | Drive name (e.g., "PLEXTOR PX-716A") |
| `--quality` | ✓ | Quality (1-100) |

> **How to find the drive name**
> You can obtain it using the `cd-info` command (libcdio).
> 
> ```bash
> $ cd-info -A --no-cddb
> ...
> Vendor                      : Optiarc 
> Model                       : DVD RW AD-7290H 
> ...
> ```
> In this case, you can specify it as `--drive "Optiarc DVD RW AD-7290H"`.


By default, it operates in **dry-run** mode, which only displays the information to be submitted.
To perform the actual submission, set the `CTDB_CLI_CALLER` environment variable to identify your application name.

```bash
env CTDB_CLI_CALLER="your-app-name" ctdb-cli submit test.cue --drive "drive name" --quality 100
```

