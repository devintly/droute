# Discord Droute (Native Proxy for Discord)

**Droute** is a tool that adds SOCKS5 proxy support directly into the Discord Windows client. It fixes the lack of built-in proxy settings in Discord, so you don't have to mess with TUN interfaces or full-system VPNs.

The project is inspired by the [force-proxy](https://github.com/runetfreedom/force-proxy) concept and uses the [MinHook](https://github.com/tsudakageyu/minhook) library.

> [!WARNING]
> **Droute is not a standalone tool for bypassing censorship or blocks.** 
> It requires an existing SOCKS5 proxy to function. You must provide your own proxy server or run a local client like **FlClash, v2rayN, Nekobox, or Amnezia**. Droute only handles the routing part inside Discord.

<img width="451" height="415" alt="image" src="https://github.com/user-attachments/assets/11509cf4-1a11-4744-92e6-d340c49de6cd" />

---

## Features

- **Full Proxying:** Routes all Discord traffic through your proxy, completely ignoring system proxy settings, TUN interfaces, or VPNs.
- **Voice Chats & Streams:** Proxies both TCP (chat, media) and UDP traffic, so voice calls and screen shares work perfectly.
- **Isolated:** Works entirely within Discord's memory. It doesn't create system services or change Windows network settings.
- **Survives Updates:** The patch automatically reapplies itself whenever Discord updates.
- **Multi-Client Support:** Works with Discord Stable, Canary, and PTB.

---

## Installation

1. Download the latest release from the [releases page](https://github.com/snowluwu/droute/releases/latest).
2. Open `droute.exe`, enter your SOCKS5 proxy details, and choose your Discord build.
3. Click apply to install the patch.

---

## How It Works

### Clean Integration
Droute doesn't modify Discord's actual executable files. Instead, it hooks into the process using DLL Hijacking and .NET config files.

### Intercepting Traffic
The tool places `version.dll` and `droute.dll` into the Discord folder.
- When Discord starts, it loads the local `version.dll` instead of the system one.
- This local `version.dll` forwards all standard requests to the real system library while loading `droute.dll` in the background.
- Using **MinHook**, `droute.dll` hooks into Discord's low-level network functions and redirects traffic to your proxy.

### Handling Discord Updates
When Discord updates, it creates a folder with the new version number. Droute hooks into this update process to stay active:
- It drops a `.config` file for the .NET application into the folder with `Update.exe` (Squirrel Updater).
- When `Update.exe` runs, it automatically loads `Droute.UpdaterHook.dll`.
- This library hooks the process creation function. As soon as Squirrel Updater creates the new version directory, the hook patches it before the new Discord client even launches.

### Settings & Logs
- **Configuration:** All settings are stored in the Windows Registry at `HKCU/Software/droute` and can be tweaked via `regedit`.
- **Logs:** The main module writes logs to `%Temp%\droute.log`, while the updater logs are saved to `droute.log` in the Discord root folder.
