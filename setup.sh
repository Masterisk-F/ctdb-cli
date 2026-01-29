#!/bin/bash
set -e

CUETOOLS_VERSION="v2.2.6"

# Clone CUETools if not present
if [ ! -d "external/cuetools.net" ]; then
    echo "Cloning cuetools.net ($CUETOOLS_VERSION)..."
    mkdir -p external
    git clone --branch $CUETOOLS_VERSION https://github.com/gchudov/cuetools.net.git external/cuetools.net
    cd external/cuetools.net
    git submodule update --init --recursive
    cd ../..
else
    echo "cuetools.net already exists. Ensuring version $CUETOOLS_VERSION..."
    cd external/cuetools.net
    git fetch origin
    git checkout $CUETOOLS_VERSION
    git submodule update --init --recursive
    cd ../..
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

# UTF.Unknown patch (Automatic charset detection for CUE files)
if patch -p0 -N --dry-run < patches/utf_unknown_encoding.patch >/dev/null 2>&1; then
    patch -p0 < patches/utf_unknown_encoding.patch
    echo "  - UTF.Unknown encoding patch applied."
else
    echo "  - UTF.Unknown encoding patch already applied."
fi

echo "Setup complete. You can now run 'dotnet build'."
