#include "pch.h"
#include "src/exports.hpp"
#include "src/core/config.hpp"
#include "src/core/logger.hpp"
#include "src/core/utils.hpp"
#include "src/hook/hooks.hpp"
#include "src/hook/udp.hpp"
#include "src/hook/process.hpp"

namespace droute {

    static std::atomic<bool> g_running{ false };
    static std::thread g_worker;

    void ReconnectWorker() {
        while (g_running.load(std::memory_order_relaxed)) {
            Sleep(g_cfg.reconnectInterval);

            std::vector<SOCKET> pending;
            {
                std::shared_lock<std::shared_mutex> lock(g_stateMutex);
                pending.assign(g_pendingUdp.begin(), g_pendingUdp.end());
            }

            for (SOCKET s : pending) {
                UdpAssociation temp;
                bool ok = TryUdpAssociate(temp);

                std::unique_lock<std::shared_mutex> lock(g_stateMutex);
                auto it = g_udpMap.find(s);
                if (it == g_udpMap.end()) {
                    if (temp.ctrlSocket != INVALID_SOCKET) {
                        Hooks::Real_closesocket(temp.ctrlSocket);
                    }
                    continue;
                }

                if (ok) {
                    it->second.ctrlSocket = temp.ctrlSocket;
                    it->second.relayAddr = temp.relayAddr;
                    it->second.status = UdpAssociation::Status::Associated;
                    g_pendingUdp.erase(s);
                    LOG_INFO("udp %llu reassociated", (ULONG_PTR)s);
                } else {
                    LOG_TRACE("udp %llu retry failed", (ULONG_PTR)s);
                }
            }
        }
        LOG_TRACE("reconnect worker exiting");
    }

    static LONG g_initDone = 0;

    void DoInit() {
        if (InterlockedCompareExchange(&g_initDone, 1, 0) != 0) {
            return;
        }

        WSADATA wsa;
        if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
            OutputDebugStringA("droute: WSAStartup failed\n");
            return;
        }

        Logger::Init();
        g_cfg.Load();
        Logger::SetLevel(g_cfg.logLevel);

        char exePath[MAX_PATH];
        GetModuleFileNameA(NULL, exePath, MAX_PATH);
        LOG_INFO("loaded into process: %s", exePath);

        wchar_t buf[MAX_PATH];
        GetModuleFileNameW(NULL, buf, MAX_PATH);
        g_ourDir = buf;
        size_t pos = g_ourDir.rfind(L'\\');
        if (pos != std::wstring::npos)
            g_ourDir = g_ourDir.substr(0, pos);

        if (!Hooks::Install()) {
            LOG_ERROR("hook installation failed");
            return;
        }

        g_running = true;
        g_worker = std::thread(ReconnectWorker);
    }

}

extern "C" __declspec(dllexport) BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    switch (reason) {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        droute::DoInit();
        break;

    case DLL_PROCESS_DETACH:
        droute::g_running = false;
        if (droute::g_worker.joinable()) {
            droute::g_worker.join();
        }
        droute::Hooks::Remove();
        droute::Logger::Shutdown();
        WSACleanup();
        break;
    }
    return TRUE;
}
