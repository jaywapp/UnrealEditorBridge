// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

/** OS 자원(MMF, Mutex, Events)을 생성/관리한다. */
class FBridgeMmfManager
{
public:
    ~FBridgeMmfManager();

    /** MMF 및 동기화 객체를 생성한다. */
    bool Create(const FString& ProjectName, uint32 ProcessId, uint32 InTotalSize);

    /** 모든 OS 자원을 해제한다. */
    void Destroy();

    uint8* GetBasePointer() const { return MappedView; }
    uint32 GetTotalSize() const { return TotalSize; }
    FString GetMmfName() const { return MmfName; }

    /** Mutex를 획득한다. */
    bool AcquireMutex(uint32 TimeoutMs = 100);

    /** Mutex를 해제한다. */
    void ReleaseMutex();

    /** Snapshot 이벤트를 시그널한다. */
    void SignalSnapshotEvent();

    /** Stream 이벤트를 시그널한다. */
    void SignalStreamEvent();

private:
    void* MmfHandle = nullptr;
    void* MutexHandle = nullptr;
    void* SnapshotEventHandle = nullptr;
    void* StreamEventHandle = nullptr;
    uint8* MappedView = nullptr;
    uint32 TotalSize = 0;
    FString MmfName;
};
