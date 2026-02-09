// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "BridgeTypes.h"

class FBridgeMmfManager;

/** MMF에 Header, Snapshot, Event를 기록한다. */
class FBridgeIpcWriter
{
public:
    /** Writer를 초기화한다. */
    void Initialize(FBridgeMmfManager* InMmfManager);

    /** 초기 Header를 기록한다 (Magic, Version, PID 등). */
    void WriteHeader();

    /** Snapshot을 직렬화하여 MMF에 기록한다. */
    void WriteSnapshot(const FBridgeSnapshot& Snapshot);

    /** 이벤트를 직렬화하여 Ring Buffer에 기록한다. */
    void WriteEvent(const FBridgeAssetEvent& Event);

    /** Heartbeat 타임스탬프를 갱신한다. */
    void UpdateHeartbeat();

private:
    FBridgeMmfManager* MmfManager = nullptr;
    uint64 EventSequenceNumber = 0;
    uint32 EventWriteIndex = 0;
    uint32 SnapshotVersion = 0;

    /** CRC32를 계산한다. */
    uint32 CalculateCrc32(const uint8* Data, uint32 Size);
};
