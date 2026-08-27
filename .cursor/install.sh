#!/usr/bin/env bash
#
# Repository bootstrap for the droute development environment (Linux Cloud Agent).
#
# Installs the Mono/MSBuild toolchain and builds the portable managed library
# (Droute.Core). The native C++ proxy (droute.dll) and the WinForms installer
# (droute.exe) require the MSVC v143 toolset + Windows SDK and are built on
# Windows only -- see .github/workflows/build-and-release.yml, which runs on
# windows-latest. Those targets cannot be produced on this Linux VM.
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# --- 1. Toolchain (idempotent) --------------------------------------------
# The droute managed projects target legacy .NET Framework (v4.5 / v4.8) with
# WinForms, which the modern .NET SDK cannot build on Linux. Ubuntu's own mono
# package ships only the deprecated xbuild (no NuGet PackageReference / Restore
# support), so MSBuild is installed from the official Mono repository.
if ! command -v msbuild >/dev/null 2>&1; then
    echo "==> Installing Mono + MSBuild from the official Mono repository"
    sudo apt-get update -qq
    sudo apt-get install -y -qq ca-certificates gnupg
    sudo gpg --homedir /tmp --no-default-keyring \
        --keyring /usr/share/keyrings/mono-official-archive-keyring.gpg \
        --keyserver hkp://keyserver.ubuntu.com:80 \
        --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
    echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/ubuntu stable-focal main" \
        | sudo tee /etc/apt/sources.list.d/mono-official-stable.list >/dev/null
    sudo apt-get update -qq
    sudo apt-get install -y -qq mono-complete msbuild
else
    echo "==> Mono/MSBuild already present: $(msbuild --version | tail -1)"
fi

# --- 2. Submodules ---------------------------------------------------------
echo "==> Initializing git submodules (MinHook)"
git submodule update --init --recursive

# --- 3. Build the portable managed library ---------------------------------
echo "==> Restoring NuGet packages for Droute.Core"
msbuild core/core.csproj /t:Restore \
    /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo "==> Building Droute.Core (Release)"
msbuild core/core.csproj \
    /p:Configuration=Release /p:Platform=AnyCPU /p:RestorePackages=false /verbosity:minimal

echo "==> Build complete: $(ls -1 core/bin/Release/Droute.Core.dll)"
