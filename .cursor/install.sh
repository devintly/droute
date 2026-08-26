#!/usr/bin/env bash
#
# Repository bootstrap for the droute development environment.
#
# Builds the portable managed library (Droute.Core) with Mono/MSBuild. The
# native C++ proxy (droute.dll) and the WinForms installer (droute.exe) require
# the MSVC v143 toolset + Windows SDK and are built on Windows only (see the
# `build-and-release.yml` GitHub Actions workflow, which runs on windows-latest).
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Initializing git submodules (MinHook)"
git submodule update --init --recursive

echo "==> Restoring NuGet packages for Droute.Core"
msbuild core/core.csproj /t:Restore \
    /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo "==> Building Droute.Core (Release)"
msbuild core/core.csproj \
    /p:Configuration=Release /p:Platform=AnyCPU /p:RestorePackages=false /verbosity:minimal

echo "==> Build complete: $(ls -1 core/bin/Release/Droute.Core.dll)"
