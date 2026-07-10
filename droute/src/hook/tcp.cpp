#include "pch.h"
#include "src/hook/tcp.hpp"
#include "src/hook/hooks.hpp"
#include "src/core/config.hpp"
#include "src/core/logger.hpp"
#include "src/core/utils.hpp"
#include "src/net/socks5.hpp"

namespace droute {

    int ConnectToProxy(SOCKET s) {
        int rc = Hooks::Real_connect(s, reinterpret_cast<const sockaddr*>(&g_proxyAddr), sizeof(g_proxyAddr));
        if (rc == 0)
            return 0;

        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK)
            return SOCKET_ERROR;

        if (!WaitForWrite(s, static_cast<int>(g_cfg.connectTimeout))) {
            WSASetLastError(WSAETIMEDOUT);
            return SOCKET_ERROR;
        }

        int soErr = 0;
        int soLen = sizeof(soErr);
        if (getsockopt(s, SOL_SOCKET, SO_ERROR, reinterpret_cast<char*>(&soErr), &soLen) != 0)
            return SOCKET_ERROR;
        if (soErr != 0) {
            WSASetLastError(soErr);
            return SOCKET_ERROR;
        }

        return 0;
    }

    int Socks5ProxyConnect(SOCKET s, const sockaddr_in& target) {
        if (!Socks5Handshake(s, g_cfg.user.c_str(), g_cfg.password.c_str())) {
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }
        if (!Socks5RequestConnect(s, target)) {
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }
        return 0;
    }

    int ConnectViaProxy(SOCKET s, const sockaddr_in* target) {
        if (ConnectToProxy(s) != 0) {
            return SOCKET_ERROR;
        }
        if (Socks5ProxyConnect(s, *target) != 0) {
            return SOCKET_ERROR;
        }
        return 0;
    }

    int WSAAPI Mine_connect(SOCKET s, const sockaddr* name, int namelen) {
        if (namelen < static_cast<int>(sizeof(sockaddr_in))) {
            return Hooks::Real_connect(s, name, namelen);
        }

        const sockaddr_in* addr = reinterpret_cast<const sockaddr_in*>(name);
        if (addr->sin_family != AF_INET) {
            return Hooks::Real_connect(s, name, namelen);
        }

        if (addr->sin_addr.s_addr == 0) {
            return Hooks::Real_connect(s, name, namelen);
        }

        if (IsLocalAddr(*addr) || IsSameAddr(*addr, g_proxyAddr)) {
            return Hooks::Real_connect(s, name, namelen);
        }
        if (IsUdpSocket(s)) {
            return Hooks::Real_connect(s, name, namelen);
        }

        bool wasNonBlocking = false;
        {
            std::shared_lock<std::shared_mutex> lock(g_stateMutex);
            wasNonBlocking = g_nonBlockingSockets.count(s) != 0;
        }

        const std::string target = AddrToString(*addr);
        const uint64_t startedAt = GetTickCount64();

        int result = ConnectViaProxy(s, addr);
        if (result != 0) {
            int err = WSAGetLastError();
            LOG_WARN("connect -> %s failed: wsa_error=%d elapsed_ms=%llu",
                     target.c_str(), err, GetTickCount64() - startedAt);
            return SOCKET_ERROR;
        }

        const uint64_t elapsed = GetTickCount64() - startedAt;
        if (elapsed >= 1000) {
            LOG_WARN("connect -> %s slow mode=%s elapsed_ms=%llu", target.c_str(),
                     wasNonBlocking ? "nonblocking" : "blocking", elapsed);
        } else {
            LOG_DEBUG("connect -> %s via proxy mode=%s elapsed_ms=%llu", target.c_str(),
                      wasNonBlocking ? "nonblocking" : "blocking", elapsed);
        }

        if (wasNonBlocking) {
            WSASetLastError(WSAEWOULDBLOCK);
            return SOCKET_ERROR;
        }
        return 0;
    }

}
