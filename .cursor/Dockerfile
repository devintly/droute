FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive

# Base tooling. build-essential/cmake are kept so the MinHook submodule and any
# future native experiments can be compiled locally.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
        git \
        sudo \
        build-essential \
        cmake \
    && rm -rf /var/lib/apt/lists/*

# Mono + MSBuild from the official Mono repository. The droute managed projects
# target legacy .NET Framework (v4.5 / v4.8) with WinForms, which the modern
# .NET SDK cannot build on Linux. Ubuntu's own mono package ships only the
# deprecated xbuild (no NuGet PackageReference / Restore support), so the
# official repo is required for a working `msbuild`.
RUN gpg --homedir /tmp --no-default-keyring \
        --keyring /usr/share/keyrings/mono-official-archive-keyring.gpg \
        --keyserver hkp://keyserver.ubuntu.com:80 \
        --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
    && echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/ubuntu stable-focal main" \
        > /etc/apt/sources.list.d/mono-official-stable.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends mono-complete msbuild \
    && rm -rf /var/lib/apt/lists/*

# Ubuntu 24.04 ships a default non-root `ubuntu` user (uid 1000); grant it
# passwordless sudo so the agent can install extra packages if needed.
RUN echo "ubuntu ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/ubuntu \
    && chmod 0440 /etc/sudoers.d/ubuntu

USER ubuntu
