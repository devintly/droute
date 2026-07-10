#include "pch.h"
#include "src/hook/udp.hpp"
#include "src/hook/hooks.hpp"
#include "src/core/config.hpp"
#include "src/core/logger.hpp"
#include "src/core/utils.hpp"
#include "src/net/socks5.hpp"

namespace droute {

    bool TryUdpAssociate(UdpAssociation& out) {
        out = {};
        out.status = UdpAssociation::Status::Idle;

        SOCKET ctrl = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (ctrl == INVALID_SOCKET) {
            return false;
        }

        u_long mode = 1;
        Hooks::Real_ioctlsocket(ctrl, FIONBIO, &mode);

        int rc = Hooks::Real_connect(ctrl, reinterpret_cast<const sockaddr*>(&g_proxyAddr), sizeof(g_proxyAddr));
        if (rc == SOCKET_ERROR) {
            int err = WSAGetLastError();
            if (err == WSAEWOULDBLOCK) {
                if (!WaitForWrite(ctrl, static_cast<int>(g_cfg.connectTimeout))) {
                    Hooks::Real_closesocket(ctrl);
                    return false;
                }
                int soErr = 0; int soLen = sizeof(soErr);
                getsockopt(ctrl, SOL_SOCKET, SO_ERROR, reinterpret_cast<char*>(&soErr), &soLen);
                if (soErr != 0) {
                    Hooks::Real_closesocket(ctrl);
                    return false;
                }
            } else {
                Hooks::Real_closesocket(ctrl);
                return false;
            }
        }

        if (!Socks5Handshake(ctrl, g_cfg.user.c_str(), g_cfg.password.c_str())) {
            Hooks::Real_closesocket(ctrl);
            return false;
        }

        sockaddr_in relay;
        if (!Socks5RequestUdpAssociate(ctrl, relay)) {
            Hooks::Real_closesocket(ctrl);
            return false;
        }

        out.ctrlSocket = ctrl;
        out.relayAddr = relay;
        out.status = UdpAssociation::Status::Associated;
        return true;
    }

    static bool ActivateUdpRelay(SOCKET s, std::unique_lock<std::shared_mutex>& lock,
                                 std::map<SOCKET, UdpAssociation>::iterator& it) {
        lock.unlock();
        UdpAssociation temp;
        bool ok = TryUdpAssociate(temp);
        lock.lock();
        it = g_udpMap.find(s);
        if (it == g_udpMap.end()) {
            if (temp.ctrlSocket != INVALID_SOCKET) {
                Hooks::Real_closesocket(temp.ctrlSocket);
            }
            return false;
        }
        if (ok) {
            it->second.ctrlSocket = temp.ctrlSocket;
            it->second.relayAddr = temp.relayAddr;
            it->second.status = UdpAssociation::Status::Associated;
            g_pendingUdp.erase(s);
        } else {
            it->second.status = UdpAssociation::Status::PendingAssociate;
            g_pendingUdp.insert(s);
        }
        return ok;
    }

    int WSAAPI Mine_bind(SOCKET s, const sockaddr* addr, int namelen) {
        bool isUdp = IsUdpSocket(s);

        if (isUdp) {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            if (g_udpMap.find(s) == g_udpMap.end()) {
                UdpAssociation assoc;
                assoc.status = UdpAssociation::Status::Idle;
                g_udpMap.emplace(s, std::move(assoc));
                LOG_DEBUG("udp socket %llu tracked", (ULONG_PTR)s);
            }
        }

        int rc = Hooks::Real_bind(s, addr, namelen);
        if (rc != 0) return rc;

        if (isUdp) {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            auto it = g_udpMap.find(s);
            if (it == g_udpMap.end()) {
                return 0;
            }
            if (it->second.status == UdpAssociation::Status::Idle) {
                if (!ActivateUdpRelay(s, lock, it)) {
                    LOG_WARN("udp %llu associate deferred", (ULONG_PTR)s);
                } else {
                    LOG_INFO("udp %llu associated, relay=%s", (ULONG_PTR)s, AddrToString(it->second.relayAddr).c_str());
                }
            }
        }

        return 0;
    }

    int WSAAPI Mine_sendto(SOCKET s, const char* buf, int len, int flags, const sockaddr* to, int tolen) {
        if (!to || tolen < static_cast<int>(sizeof(sockaddr_in))) {
            return Hooks::Real_sendto(s, buf, len, flags, to, tolen);
        }
        const sockaddr_in* dst = reinterpret_cast<const sockaddr_in*>(to);
        if (IsMulticast(*dst)) {
            return Hooks::Real_sendto(s, buf, len, flags, to, tolen);
        }

        std::unique_lock<std::shared_mutex> lock(g_stateMutex);
        auto it = g_udpMap.find(s);
        if (it == g_udpMap.end()) {
            return Hooks::Real_sendto(s, buf, len, flags, to, tolen);
        }

        if (it->second.status == UdpAssociation::Status::Idle) {
            if (!ActivateUdpRelay(s, lock, it)) {
                WSASetLastError(WSAEWOULDBLOCK);
                return SOCKET_ERROR;
            }
        }

        if (it->second.status == UdpAssociation::Status::PendingAssociate) {
            WSASetLastError(WSAEWOULDBLOCK);
            return SOCKET_ERROR;
        }

        sockaddr_in relayAddr = it->second.relayAddr;
        lock.unlock();

        auto packet = Socks5WrapUdp(*dst, buf, len);
        int result = Hooks::Real_sendto(s, reinterpret_cast<const char*>(packet.data()), static_cast<int>(packet.size()),
                                        flags, reinterpret_cast<const sockaddr*>(&relayAddr), sizeof(relayAddr));
        return result == static_cast<int>(packet.size()) ? len : result;
    }

    int WSAAPI Mine_WSASendTo(SOCKET s, LPWSABUF lpBuffers, DWORD dwBufferCount, LPDWORD lpNumberOfBytesSent,
                              DWORD dwFlags, const sockaddr* lpTo, int iTolen,
                              LPWSAOVERLAPPED lpOverlapped, LPWSAOVERLAPPED_COMPLETION_ROUTINE lpCompletionRoutine) {
        if (!lpTo || iTolen < static_cast<int>(sizeof(sockaddr_in)) || dwBufferCount == 0 || !lpBuffers || !lpBuffers->buf || lpBuffers->len == 0) {
            return Hooks::Real_WSASendTo(s, lpBuffers, dwBufferCount, lpNumberOfBytesSent, dwFlags,
                                         lpTo, iTolen, lpOverlapped, lpCompletionRoutine);
        }
        const sockaddr_in* dst = reinterpret_cast<const sockaddr_in*>(lpTo);
        if (IsMulticast(*dst)) {
            return Hooks::Real_WSASendTo(s, lpBuffers, dwBufferCount, lpNumberOfBytesSent, dwFlags,
                                         lpTo, iTolen, lpOverlapped, lpCompletionRoutine);
        }

        std::unique_lock<std::shared_mutex> lock(g_stateMutex);
        auto it = g_udpMap.find(s);
        if (it == g_udpMap.end()) {
            return Hooks::Real_WSASendTo(s, lpBuffers, dwBufferCount, lpNumberOfBytesSent, dwFlags,
                                         lpTo, iTolen, lpOverlapped, lpCompletionRoutine);
        }

        if (it->second.status == UdpAssociation::Status::Idle) {
            if (!ActivateUdpRelay(s, lock, it)) {
                WSASetLastError(WSAEWOULDBLOCK);
                return SOCKET_ERROR;
            }
        }

        if (it->second.status == UdpAssociation::Status::PendingAssociate) {
            WSASetLastError(WSAEWOULDBLOCK);
            return SOCKET_ERROR;
        }

        auto packet = Socks5WrapUdp(*dst, lpBuffers->buf, static_cast<int>(lpBuffers->len));
        WSABUF wb = { static_cast<ULONG>(packet.size()), reinterpret_cast<char*>(packet.data()) };
        return Hooks::Real_WSASendTo(s, &wb, 1, lpNumberOfBytesSent, 0,
                                      reinterpret_cast<const sockaddr*>(&it->second.relayAddr), sizeof(it->second.relayAddr),
                                      lpOverlapped, lpCompletionRoutine);
    }

    int WSAAPI Mine_recvfrom(SOCKET s, char* buf, int len, int flags, sockaddr* from, int* fromlen) {
        int received = Hooks::Real_recvfrom(s, buf, len, flags, from, fromlen);
        if (received == SOCKET_ERROR) {
            return received;
        }

        std::unique_lock<std::shared_mutex> lock(g_stateMutex);
        auto it = g_udpMap.find(s);
        if (it == g_udpMap.end() || it->second.status != UdpAssociation::Status::Associated) {
            return received;
        }

        if (received == 0) {
            return 0;
        }

        if (received < SOCKS5_UDP_HEADER_SIZE) {
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }

        sockaddr_in src;
        const void* payload;
        int payloadLen;
        if (!Socks5UnwrapUdp(buf, received, src, payload, payloadLen)) {
            LOG_WARN("udp %llu recv unwrap failed", (ULONG_PTR)s);
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }

        if (from && fromlen) {
            int copyLen = min(*fromlen, static_cast<int>(sizeof(sockaddr_in)));
            memcpy(from, &src, copyLen);
            *fromlen = sizeof(sockaddr_in);
        }
        memmove(buf, payload, payloadLen);
        return payloadLen;
    }

    int WSAAPI Mine_WSARecvFrom(SOCKET s, LPWSABUF lpBuffers, DWORD dwBufferCount, LPDWORD lpNumberOfBytesRecvd,
                                LPDWORD lpFlags, sockaddr* lpFrom, LPINT lpFromlen,
                                LPWSAOVERLAPPED lpOverlapped, LPWSAOVERLAPPED_COMPLETION_ROUTINE lpCompletionRoutine) {
        DWORD localRecvd = 0;
        if (!lpNumberOfBytesRecvd) lpNumberOfBytesRecvd = &localRecvd;

        int status = Hooks::Real_WSARecvFrom(s, lpBuffers, dwBufferCount, lpNumberOfBytesRecvd, lpFlags,
                                               lpFrom, lpFromlen, lpOverlapped, lpCompletionRoutine);
        if (status != 0) {
            return status;
        }

        std::unique_lock<std::shared_mutex> lock(g_stateMutex);
        auto it = g_udpMap.find(s);
        if (it == g_udpMap.end() || it->second.status != UdpAssociation::Status::Associated) {
            return status;
        }

        if (*lpNumberOfBytesRecvd == 0) {
            return 0;
        }

        if (*lpNumberOfBytesRecvd < SOCKS5_UDP_HEADER_SIZE) {
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }

        sockaddr_in src;
        const void* payload;
        int payloadLen;
        if (!Socks5UnwrapUdp(lpBuffers->buf, static_cast<int>(*lpNumberOfBytesRecvd), src, payload, payloadLen)) {
            LOG_WARN("udp %llu recv unwrap failed", (ULONG_PTR)s);
            WSASetLastError(WSAECONNRESET);
            return SOCKET_ERROR;
        }

        if (lpFrom && lpFromlen) {
            int copyLen = min(*lpFromlen, static_cast<int>(sizeof(sockaddr_in)));
            memcpy(lpFrom, &src, copyLen);
            *lpFromlen = sizeof(sockaddr_in);
        }
        memmove(lpBuffers->buf, payload, payloadLen);
        *lpNumberOfBytesRecvd = static_cast<DWORD>(payloadLen);
        return 0;
    }

    int WSAAPI Mine_closesocket(SOCKET s) {
        {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            auto it = g_udpMap.find(s);
            if (it != g_udpMap.end()) {
                if (it->second.ctrlSocket != INVALID_SOCKET) {
                    Hooks::Real_closesocket(it->second.ctrlSocket);
                }
                g_udpMap.erase(it);
                g_pendingUdp.erase(s);
                LOG_DEBUG("udp socket %llu cleaned", (ULONG_PTR)s);
            }
            g_nonBlockingSockets.erase(s);
        }
        return Hooks::Real_closesocket(s);
    }

    int WSAAPI Mine_WSAEventSelect(SOCKET s, WSAEVENT hEventObject, long lNetworkEvents) {
        {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            if (hEventObject && lNetworkEvents) {
                g_nonBlockingSockets.insert(s);
            } else {
                g_nonBlockingSockets.erase(s);
            }
        }
        return Hooks::Real_WSAEventSelect(s, hEventObject, lNetworkEvents);
    }

    int WSAAPI Mine_WSAAsyncSelect(SOCKET s, HWND hWnd, unsigned int wMsg, long lEvent) {
        {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            if (lEvent) {
                g_nonBlockingSockets.insert(s);
            } else {
                g_nonBlockingSockets.erase(s);
            }
        }
        return Hooks::Real_WSAAsyncSelect(s, hWnd, wMsg, lEvent);
    }

    int WSAAPI Mine_ioctlsocket(SOCKET s, long cmd, u_long* argp) {
        if (cmd == FIONBIO) {
            std::unique_lock<std::shared_mutex> lock(g_stateMutex);
            if (*argp) {
                g_nonBlockingSockets.insert(s);
            } else {
                g_nonBlockingSockets.erase(s);
            }
        }
        return Hooks::Real_ioctlsocket(s, cmd, argp);
    }

}
