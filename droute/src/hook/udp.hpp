#pragma once

#include "src/hook/hooks.hpp"

namespace droute {

    bool TryUdpAssociate(UdpAssociation& out);
    void MarkUdpAssociationPending(SOCKET s, SOCKET expectedControlSocket = INVALID_SOCKET);

}
