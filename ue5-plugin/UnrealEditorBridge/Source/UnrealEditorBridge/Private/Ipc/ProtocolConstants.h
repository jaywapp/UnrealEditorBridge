// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

/**
 * IPC 프로토콜에서 사용하는 상수 정의.
 * .NET 측 ProtocolConstants.cs와 동일한 값을 유지해야 한다.
 */
namespace BridgeProtocol
{
    // Magic numbers
    static constexpr uint32 Magic = 0x55454221;           // "UEB!"
    static constexpr uint32 DiscoveryMagic = 0x55454244;  // "UEBD"

    // Protocol version (Major*1000 + Minor)
    static constexpr uint32 ProtocolVersion = 1000;

    // Header: 256 bytes
    static constexpr uint32 HeaderSize = 256;

    // Snapshot area
    static constexpr uint32 SnapshotOffset = 256;
    static constexpr uint32 SnapshotMaxSize = 4 * 1024 * 1024;  // 4 MB

    // Event Ring Buffer
    static constexpr uint32 EventRingOffset = SnapshotOffset + SnapshotMaxSize;
    static constexpr uint32 EventSlotSize = 2048;
    static constexpr uint32 EventSlotCount = 6144;
    static constexpr uint32 EventRingSize = EventSlotSize * EventSlotCount;

    // Total MMF size
    static constexpr uint32 TotalMmfSize = EventRingOffset + EventRingSize;

    // Discovery MMF
    static constexpr uint32 DiscoveryHeaderSize = 64;
    static constexpr uint32 DiscoveryEntrySize = 512;
    static constexpr uint32 DiscoveryMaxEntries = 128;
    static constexpr uint32 DiscoveryMmfSize = DiscoveryHeaderSize + (DiscoveryEntrySize * DiscoveryMaxEntries);

    // Discovery MMF name
    static const FString DiscoveryMmfName = TEXT("UEB_Discovery");

    // IPC naming helpers
    inline FString BuildMmfName(const FString& ProjectName, uint32 ProcessId)
    {
        return FString::Printf(TEXT("UEB_%s_%u"), *ProjectName, ProcessId);
    }

    inline FString BuildMutexName(const FString& MmfName)
    {
        return MmfName + TEXT("_Mtx");
    }

    inline FString BuildSnapshotEventName(const FString& MmfName)
    {
        return MmfName + TEXT("_SnapshotEvt");
    }

    inline FString BuildStreamEventName(const FString& MmfName)
    {
        return MmfName + TEXT("_StreamEvt");
    }
}
