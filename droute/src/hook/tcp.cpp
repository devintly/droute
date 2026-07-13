#include "pch.h"
#include "src/hook/tcp.hpp"
#include "src/hook/hooks.hpp"
#include "src/core/config.hpp"
#include "src/core/logger.hpp"
#include "src/core/utils.hpp"
#include "src/net/socks5.hpp"

namespace droute {

    int ConnectToProxy(SOCKET s, uint64_t deadline) {
        int rc = Hooks::Real_connect(s, reinterpret_cast<const sockaddr*>(&g_proxyAddr), sizeof(g_proxyAddr));
        if (rc == 0)
            return 0;

        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK && err != WSAEINPROGRESS)
            return SOCKET_ERROR;

        if (!WaitForConnect(s, RemainingTimeout(deadline))) {
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

    int Socks5ProxyConnect(SOCKET s, const sockaddr_in& target, uint64_t deadline) {
        if (!Socks5Handshake(s, g_cfg.user.c_str(), g_cfg.password.c_str(), deadline)) {
            return SOCKET_ERROR;
        }
        if (!Socks5RequestConnect(s, target, deadline)) {
            return SOCKET_ERROR;
        }
        return 0;
    }

    int ConnectViaProxy(SOCKET s, const sockaddr_in* target, uint64_t deadline) {
        if (ConnectToProxy(s, deadline) != 0) {
            return SOCKET_ERROR;
        }
        if (Socks5ProxyConnect(s, *target, deadline) != 0) {
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
        const uint64_t deadline = MakeDeadline(g_cfg.connectTimeout);

        bool changedToNonBlocking = false;
        if (!wasNonBlocking) {
            u_long mode = 1;
            if (Hooks::Real_ioctlsocket(s, FIONBIO, &mode) != 0)
                return SOCKET_ERROR;
            changedToNonBlocking = true;
        }

        int result = ConnectViaProxy(s, addr, deadline);
        int resultError = result == 0 ? 0 : WSAGetLastError();

        if (changedToNonBlocking) {
            u_long mode = 0;
            if (Hooks::Real_ioctlsocket(s, FIONBIO, &mode) != 0 && result == 0) {
                result = SOCKET_ERROR;
                resultError = WSAGetLastError();
            }
        }

        if (result != 0) {
            WSASetLastError(resultError);
            LOG_WARN("connect -> %s failed: wsa_error=%d elapsed_ms=%llu",
                     target.c_str(), resultError, GetTickCount64() - startedAt);
            WSASetLastError(resultError);
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
