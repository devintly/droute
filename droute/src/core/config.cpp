#include "pch.h"
#include "src/core/config.hpp"
#include "src/core/utils.hpp"

#include <cstdlib>

namespace droute {

    Config g_cfg;
    sockaddr_in g_proxyAddr;

    static constexpr wchar_t kIniSection[] = L"droute";
    static constexpr wchar_t kIniFileName[] = L"droute.ini";

    static std::string WideToAnsi(const wchar_t* s) {
        if (!s || !*s)
            return {};

        int len = WideCharToMultiByte(CP_ACP, 0, s, -1, nullptr, 0, nullptr, nullptr);
        if (len <= 1)
            return {};

        std::string out(static_cast<size_t>(len - 1), '\0');
        WideCharToMultiByte(CP_ACP, 0, s, -1, &out[0], len, nullptr, nullptr);
        return out;
    }

    static bool TryGetIniPath(wchar_t* out, DWORD size) {
        if (!out || size == 0)
            return false;

        HMODULE module = nullptr;
        if (!GetModuleHandleExA(
                GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCSTR>(&TryGetIniPath),
                &module) || !module) {
            return false;
        }

        DWORD len = GetModuleFileNameW(module, out, size);
        if (len == 0 || len >= size)
            return false;

        wchar_t* slash = wcsrchr(out, L'\\');
        if (!slash)
            slash = wcsrchr(out, L'/');
        if (!slash)
            return false;

        const size_t destChars = static_cast<size_t>(size - (slash + 1 - out));
        return wcscpy_s(slash + 1, destChars, kIniFileName) == 0;
    }

    static bool FileExistsW(const wchar_t* path) {
        DWORD attrs = GetFileAttributesW(path);
        return attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY);
    }

    bool Config::Load() {
        wchar_t iniPath[MAX_PATH] = {};
        const bool hasIni = TryGetIniPath(iniPath, MAX_PATH) && FileExistsW(iniPath);

        HKEY hKey = nullptr;
        LONG registryError = ERROR_SUCCESS;
        bool hasRegistry = false;
        if (!hasIni) {
            registryError = RegOpenKeyExA(HKEY_CURRENT_USER, "Software\\droute", 0, KEY_READ, &hKey);
            hasRegistry = registryError == ERROR_SUCCESS;
        }

        const char* sourceName = hasIni ? "ini" : (hasRegistry ? "registry" : "defaults");

        auto ReadString = [&](const char* name, std::string& out) -> bool {
            if (hasIni) {
                wchar_t wname[64] = {};
                if (MultiByteToWideChar(CP_ACP, 0, name, -1, wname, 64) <= 0)
                    return false;

                wchar_t buf[512] = {};
                const wchar_t sentinel[] = L"\x01";
                GetPrivateProfileStringW(kIniSection, wname, sentinel, buf, 512, iniPath);
                if (wcscmp(buf, sentinel) == 0)
                    return false;

                out = WideToAnsi(buf);
                return true;
            }

            if (!hasRegistry)
                return false;

            DWORD type = 0;
            DWORD size = 0;
            if (RegQueryValueExA(hKey, name, nullptr, &type, nullptr, &size) != ERROR_SUCCESS || type != REG_SZ || size == 0)
                return false;

            std::vector<char> buf(size + 1, '\0');
            if (RegQueryValueExA(hKey, name, nullptr, &type, reinterpret_cast<LPBYTE>(buf.data()), &size) != ERROR_SUCCESS)
                return false;

            out.assign(buf.data(), strnlen_s(buf.data(), size));
            return true;
        };

        auto ReadDword = [&](const char* name, uint32_t& out) -> bool {
            if (hasIni) {
                wchar_t wname[64] = {};
                if (MultiByteToWideChar(CP_ACP, 0, name, -1, wname, 64) <= 0)
                    return false;

                wchar_t buf[32] = {};
                DWORD n = GetPrivateProfileStringW(kIniSection, wname, L"", buf, 32, iniPath);
                if (n == 0)
                    return false;

                wchar_t* end = nullptr;
                unsigned long val = wcstoul(buf, &end, 10);
                if (end == buf)
                    return false;

                out = static_cast<uint32_t>(val);
                return true;
            }

            if (!hasRegistry)
                return false;

            DWORD type = 0, val = 0, size = sizeof(val);
            if (RegQueryValueExA(hKey, name, nullptr, &type, (LPBYTE)&val, &size) == ERROR_SUCCESS && type == REG_DWORD) {
                out = val;
                return true;
            }
            return false;
        };

        uint32_t tmp;
        uint32_t invalidLogLevelValue = 0;
        bool invalidLogLevel = false;
        if (ReadDword("LogLevel", tmp)) {
            if (tmp <= static_cast<uint32_t>(LogLevel::Off)) {
                logLevel = static_cast<LogLevel>(tmp);
            } else {
                invalidLogLevel = true;
                invalidLogLevelValue = tmp;
            }
        }

        Logger::SetLevel(logLevel);
        Logger::Init();

        if (invalidLogLevel)
            LOG_WARN("invalid LogLevel=%u, using default %s", invalidLogLevelValue, LevelToString(logLevel));

        if (!hasIni && !hasRegistry)
            LOG_WARN("registry key not found (error=%ld), using defaults", registryError);

        ReadString("Host", host);
        if (host.empty()) {
            host = "127.0.0.1";
            LOG_WARN("proxy host is empty, using default %s", host.c_str());
        }
        uint32_t portTmp = port;
        if (ReadDword("Port", portTmp)) {
            if (portTmp > 0 && portTmp <= UINT16_MAX) {
                port = static_cast<uint16_t>(portTmp);
            } else {
                LOG_WARN("invalid proxy port %u, using default %u", portTmp, port);
            }
        }
        ReadString("User", user);
        ReadString("Password", password);

        if (ReadDword("ConnectTimeout", tmp)) {
            if (tmp > 0 && tmp <= INT_MAX) connectTimeout = tmp;
            else LOG_WARN("invalid ConnectTimeout=%u, using default %u", tmp, connectTimeout);
        }
        if (ReadDword("ReconnectInterval", tmp)) {
            if (tmp > 0) reconnectInterval = tmp;
            else LOG_WARN("invalid ReconnectInterval=0, using default %u", tmp, reconnectInterval);
        }

        if (hKey)
            RegCloseKey(hKey);

        memset(&g_proxyAddr, 0, sizeof(g_proxyAddr));
        g_proxyAddr.sin_family = AF_INET;
        g_proxyAddr.sin_port = htons(static_cast<uint16_t>(port));

        if (inet_pton(AF_INET, host.c_str(), &g_proxyAddr.sin_addr) == 1) {
        } else {
            addrinfo hints = {};
            hints.ai_family = AF_INET;
            hints.ai_socktype = SOCK_STREAM;
            addrinfo* result = nullptr;
            const int gaiError = getaddrinfo(host.c_str(), nullptr, &hints, &result);
            if (gaiError == 0 && result) {
                g_proxyAddr.sin_addr = reinterpret_cast<sockaddr_in*>(result->ai_addr)->sin_addr;
                freeaddrinfo(result);
            } else {
                LOG_ERROR("failed to resolve '%s': %d; using 127.0.0.1", host.c_str(), gaiError);
                inet_pton(AF_INET, "127.0.0.1", &g_proxyAddr.sin_addr);
            }
        }

        const char* authState = user.empty() && password.empty()
            ? "none"
            : (!user.empty() && !password.empty() ? "set" : "incomplete");
        if (strcmp(authState, "incomplete") == 0)
            LOG_WARN("proxy credentials are incomplete; authentication is disabled");

        LOG_INFO("config source=%s proxy=%s auth=%s connect_timeout_ms=%u reconnect_interval_ms=%u log_level=%s",
                 sourceName,
                 AddrToString(g_proxyAddr).c_str(),
                 authState, connectTimeout, reconnectInterval, LevelToString(logLevel));
        return hasIni || hasRegistry;
    }

}
