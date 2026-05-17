# Discord Droute (Native Proxy for Discord)

**Droute** is a tool for integrating a SOCKS5 proxy into the Discord client for Windows. It resolves the lack of built-in proxy settings in Discord and eliminates the need to configure TUN interfaces or a VPN.

The project is inspired by the [force-proxy](https://github.com/runetfreedom/force-proxy) concept and uses the [MinHook](https://github.com/tsudakageyu/minhook) hooking library.

---

## Features

- **Full Proxying:** Forces all Discord traffic through a specified server, ignoring system proxies, TUN interfaces, and VPNs.
- **Voice Chat and Stream Support:** Proxies both TCP (chat, media) and UDP traffic, ensuring the functionality of voice channels and streams.
- **Process-Level Isolation:** Operates locally within Discord's memory without creating services or altering Windows network settings.
- **Update Resilience:** The patch automatically migrates whenever Discord updates.
- **Multi-Client Support:** Fully compatible with Stable, Canary, and PTB builds.

---

## Installation

1. Download the latest version from the [releases page](https://github.com/snowluwu/droute/releases/latest).
2. Run `droute.exe`, enter your SOCKS5 proxy details, and select your Discord build.
3. Click the apply button to install the patch.

---

## Architecture and Technical Details

### Non-Invasive Integration
Droute does not modify Discord's executable code. Memory injection is achieved using *DLL Hijacking* and .NET configuration files.

### Traffic Interception Mechanism
The libraries `version.dll` and `droute.dll` are placed into the Discord directory.
- Upon startup, Discord loads the local `version.dll` instead of the system one.
- The local `version.dll` forwards legitimate calls to the original system library while simultaneously loading `droute.dll`.
- Using **MinHook**, `droute.dll` intercepts Discord's low-level network calls and routes them to the proxy.

### Update Persistence Mechanism
When Discord updates, it moves to a directory with a new version number. Droute intercepts this process:
- A `.config` file for the .NET application is added to the folder containing `Update.exe` (Squirrel Updater).
- When `Update.exe` runs, it automatically loads the `Droute.UpdaterHook.dll` library.
- This library hooks the process creation function. As soon as Squirrel Updater downloads an update and launches the new Discord version, the hook copies the patch files into the new application directory before it starts.

### Configuration and Logging
- **Settings Storage:** All configurations are written to the Windows Registry at `HKCU/Software/droute` and can be edited via `regedit`.
- **Diagnostics:** Main module logs are stored in `%Temp%\droute.log`, while update module logs are located in `droute.log` within the Discord root directory.