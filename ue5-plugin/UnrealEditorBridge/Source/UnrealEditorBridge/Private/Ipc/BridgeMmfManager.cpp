// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeMmfManager.h"
#include "ProtocolConstants.h"

#include "Windows/AllowWindowsPlatformTypes.h"
#include <Windows.h>
#include "Windows/HideWindowsPlatformTypes.h"

FBridgeMmfManager::~FBridgeMmfManager()
{
    Destroy();
}

bool FBridgeMmfManager::Create(const FString& ProjectName, uint32 ProcessId, uint32 InTotalSize)
{
    TotalSize = InTotalSize;
    MmfName = BridgeProtocol::BuildMmfName(ProjectName, ProcessId);

    const FString MutexName = BridgeProtocol::BuildMutexName(MmfName);
    const FString SnapshotEvtName = BridgeProtocol::BuildSnapshotEventName(MmfName);
    const FString StreamEvtName = BridgeProtocol::BuildStreamEventName(MmfName);

    // MMF 생성
    MmfHandle = CreateFileMappingW(
        INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE,
        0, TotalSize, *MmfName);
    if (!MmfHandle)
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] MMF 생성 실패: %s"), *MmfName);
        return false;
    }

    // 메모리 매핑
    MappedView = static_cast<uint8*>(
        MapViewOfFile(MmfHandle, FILE_MAP_ALL_ACCESS, 0, 0, TotalSize));
    if (!MappedView)
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] MapViewOfFile 실패"));
        Destroy();
        return false;
    }

    // 초기화 (0으로 클리어)
    FMemory::Memzero(MappedView, TotalSize);

    // Mutex 생성
    MutexHandle = CreateMutexW(nullptr, 0, *MutexName);
    if (!MutexHandle)
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] Mutex 생성 실패: %s"), *MutexName);
        Destroy();
        return false;
    }

    // Snapshot 이벤트 생성 (auto-reset)
    SnapshotEventHandle = CreateEventW(nullptr, 0, 0, *SnapshotEvtName);
    if (!SnapshotEventHandle)
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] Snapshot Event 생성 실패"));
        Destroy();
        return false;
    }

    // Stream 이벤트 생성 (auto-reset)
    StreamEventHandle = CreateEventW(nullptr, 0, 0, *StreamEvtName);
    if (!StreamEventHandle)
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] Stream Event 생성 실패"));
        Destroy();
        return false;
    }

    UE_LOG(LogTemp, Log, TEXT("[UEB] IPC 자원 생성 완료: %s (크기: %u)"), *MmfName, TotalSize);
    return true;
}

void FBridgeMmfManager::Destroy()
{
    if (MappedView)
    {
        UnmapViewOfFile(MappedView);
        MappedView = nullptr;
    }
    if (MmfHandle)
    {
        CloseHandle(MmfHandle);
        MmfHandle = nullptr;
    }
    if (MutexHandle)
    {
        CloseHandle(MutexHandle);
        MutexHandle = nullptr;
    }
    if (SnapshotEventHandle)
    {
        CloseHandle(SnapshotEventHandle);
        SnapshotEventHandle = nullptr;
    }
    if (StreamEventHandle)
    {
        CloseHandle(StreamEventHandle);
        StreamEventHandle = nullptr;
    }
    TotalSize = 0;
}

bool FBridgeMmfManager::AcquireMutex(uint32 TimeoutMs)
{
    if (!MutexHandle) return false;
    DWORD Result = WaitForSingleObject(MutexHandle, TimeoutMs);
    return (Result == WAIT_OBJECT_0 || Result == WAIT_ABANDONED);
}

void FBridgeMmfManager::ReleaseMutex()
{
    if (MutexHandle)
    {
        ::ReleaseMutex(MutexHandle);
    }
}

void FBridgeMmfManager::SignalSnapshotEvent()
{
    if (SnapshotEventHandle)
    {
        SetEvent(SnapshotEventHandle);
    }
}

void FBridgeMmfManager::SignalStreamEvent()
{
    if (StreamEventHandle)
    {
        SetEvent(StreamEventHandle);
    }
}
