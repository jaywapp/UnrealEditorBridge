// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

/** Discovery MMF에 인스턴스를 등록/해제한다. */
class FBridgeDiscoveryRegistrar
{
public:
    ~FBridgeDiscoveryRegistrar();

    /** Discovery MMF에 현재 인스턴스를 등록한다. */
    bool Register(const FString& ProjectName, uint32 ProcessId, const FString& MmfName);

    /** 등록을 해제한다. */
    void Unregister();

    /** 등록된 엔트리의 Heartbeat를 갱신한다. */
    void UpdateHeartbeat();

    /** 비활성 인스턴스를 정리한다. */
    void CleanupStaleEntries(int64 MaxAgeTicks);

private:
    void* DiscoveryMmfHandle = nullptr;
    void* DiscoveryMutexHandle = nullptr;
    uint8* DiscoveryView = nullptr;
    int32 RegisteredEntryIndex = -1;
};
