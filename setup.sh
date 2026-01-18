#!/bin/bash
set -e

# Clone CUETools if not present
if [ ! -d "external/cuetools.net" ]; then
    echo "Cloning cuetools.net..."
    mkdir -p external
    git clone https://github.com/gchudov/cuetools.net.git external/cuetools.net
    cd external/cuetools.net
    git submodule update --init --recursive
    cd ../..
else
    echo "cuetools.net already exists. Skipping clone."
fi

# Apply patches
echo "Applying Linux compatibility patches..."

# Freedb patch (SDK style conversion for .NET Core support)
if patch -p0 -N --dry-run < patches/freedb_linux.patch >/dev/null 2>&1; then
    patch -p0 < patches/freedb_linux.patch
    echo "  - Freedb patch applied."
else
    echo "  - Freedb patch already applied."
fi

# TagLib patch (Fix missing properties required by CUESheet.cs)
if patch -p0 -N --dry-run < patches/taglib_linux.patch >/dev/null 2>&1; then
    patch -p0 < patches/taglib_linux.patch
    echo "  - TagLib patch applied."
else
    echo "  - TagLib patch already applied."
fi

echo "Setup complete. You can now run 'dotnet build'."
