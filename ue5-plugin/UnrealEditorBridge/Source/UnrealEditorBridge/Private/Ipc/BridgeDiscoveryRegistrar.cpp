// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeDiscoveryRegistrar.h"
#include "ProtocolConstants.h"
#include "HeaderLayout.h"

#include "Windows/AllowWindowsPlatformTypes.h"
#include <Windows.h>
#include "Windows/HideWindowsPlatformTypes.h"

FBridgeDiscoveryRegistrar::~FBridgeDiscoveryRegistrar()
{
    Unregister();

    if (DiscoveryView)
    {
        UnmapViewOfFile(DiscoveryView);
        DiscoveryView = nullptr;
    }
    if (DiscoveryMmfHandle)
    {
        CloseHandle(DiscoveryMmfHandle);
        DiscoveryMmfHandle = nullptr;
    }
    if (DiscoveryMutexHandle)
    {
        CloseHandle(DiscoveryMutexHandle);
        DiscoveryMutexHandle = nullptr;
    }
}

bool FBridgeDiscoveryRegistrar::Register(
    const FString& ProjectName, uint32 ProcessId, const FString& MmfName)
{
    const FString DiscMmfName = BridgeProtocol::DiscoveryMmfName;
    const FString DiscMutexName = DiscMmfName + TEXT("_Mtx");

    // Discovery MMF 열기 또는 생성
    DiscoveryMmfHandle = CreateFileMappingW(
        INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE,
        0, BridgeProtocol::DiscoveryMmfSize, *DiscMmfName);
    if (!DiscoveryMmfHandle) return false;

    DiscoveryView = static_cast<uint8*>(
        MapViewOfFile(DiscoveryMmfHandle, FILE_MAP_ALL_ACCESS,
            0, 0, BridgeProtocol::DiscoveryMmfSize));
    if (!DiscoveryView) return false;

    DiscoveryMutexHandle = CreateMutexW(nullptr, 0, *DiscMutexName);
    if (!DiscoveryMutexHandle) return false;

    // Mutex 획득
    DWORD WaitResult = WaitForSingleObject(DiscoveryMutexHandle, 1000);
    if (WaitResult != WAIT_OBJECT_0 && WaitResult != WAIT_ABANDONED)
        return false;

    // Header 초기화 (Magic 확인)
    uint32 CurrentMagic = 0;
    FMemory::Memcpy(&CurrentMagic, DiscoveryView + BridgeHeaderLayout::DiscMagicOffset, sizeof(uint32));
    if (CurrentMagic != BridgeProtocol::DiscoveryMagic)
    {
        // 최초 생성 - Header 초기화
        FMemory::Memzero(DiscoveryView, BridgeProtocol::DiscoveryMmfSize);
        uint32 Magic = BridgeProtocol::DiscoveryMagic;
        FMemory::Memcpy(DiscoveryView + BridgeHeaderLayout::DiscMagicOffset, &Magic, sizeof(uint32));
    }

    // 빈 슬롯 찾기
    uint32 EntryCount = 0;
    FMemory::Memcpy(&EntryCount, DiscoveryView + BridgeHeaderLayout::DiscEntryCountOffset, sizeof(uint32));

    int32 FreeIndex = -1;
    for (uint32 i = 0; i < BridgeProtocol::DiscoveryMaxEntries; i++)
    {
        uint32 Offset = BridgeHeaderLayout::GetEntryAbsoluteOffset(i);
        uint32 EntryPid = 0;
        FMemory::Memcpy(&EntryPid, DiscoveryView + Offset + BridgeHeaderLayout::EntryPidOffset, sizeof(uint32));
        if (EntryPid == 0)
        {
            FreeIndex = static_cast<int32>(i);
            break;
        }
    }

    if (FreeIndex < 0)
    {
        ::ReleaseMutex(DiscoveryMutexHandle);
        UE_LOG(LogTemp, Warning, TEXT("[UEB] Discovery MMF 슬롯 부족"));
        return false;
    }

    // 엔트리 기록
    uint32 EntryOffset = BridgeHeaderLayout::GetEntryAbsoluteOffset(FreeIndex);
    uint8* Entry = DiscoveryView + EntryOffset;
    FMemory::Memzero(Entry, BridgeProtocol::DiscoveryEntrySize);

    // PID
    FMemory::Memcpy(Entry + BridgeHeaderLayout::EntryPidOffset, &ProcessId, sizeof(uint32));

    // Timestamps
    int64 NowTicks = FDateTime::UtcNow().GetTicks();
    FMemory::Memcpy(Entry + BridgeHeaderLayout::EntryRegisteredAtOffset, &NowTicks, sizeof(int64));
    FMemory::Memcpy(Entry + BridgeHeaderLayout::EntryHeartbeatOffset, &NowTicks, sizeof(int64));

    // ProjectName (UTF-8)
    FTCHARToUTF8 ProjUtf8(*ProjectName);
    uint32 ProjLen = FMath::Min(
        static_cast<uint32>(ProjUtf8.Length()),
        BridgeHeaderLayout::EntryProjectNameSize - 1);
    FMemory::Memcpy(Entry + BridgeHeaderLayout::EntryProjectNameOffset, ProjUtf8.Get(), ProjLen);

    // MmfName (UTF-8)
    FTCHARToUTF8 MmfUtf8(*MmfName);
    uint32 MmfLen = FMath::Min(
        static_cast<uint32>(MmfUtf8.Length()),
        BridgeHeaderLayout::EntryMmfNameSize - 1);
    FMemory::Memcpy(Entry + BridgeHeaderLayout::EntryMmfNameOffset, MmfUtf8.Get(), MmfLen);

    // EntryCount 갱신
    if (static_cast<uint32>(FreeIndex) >= EntryCount)
    {
        EntryCount = FreeIndex + 1;
        FMemory::Memcpy(DiscoveryView + BridgeHeaderLayout::DiscEntryCountOffset,
            &EntryCount, sizeof(uint32));
    }

    RegisteredEntryIndex = FreeIndex;
    ::ReleaseMutex(DiscoveryMutexHandle);

    UE_LOG(LogTemp, Log, TEXT("[UEB] Discovery 등록 완료 (슬롯: %d, PID: %u)"), FreeIndex, ProcessId);
    return true;
}

void FBridgeDiscoveryRegistrar::Unregister()
{
    if (RegisteredEntryIndex < 0 || !DiscoveryView || !DiscoveryMutexHandle)
        return;

    DWORD WaitResult = WaitForSingleObject(DiscoveryMutexHandle, 500);
    if (WaitResult == WAIT_OBJECT_0 || WaitResult == WAIT_ABANDONED)
    {
        uint32 Offset = BridgeHeaderLayout::GetEntryAbsoluteOffset(RegisteredEntryIndex);
        FMemory::Memzero(DiscoveryView + Offset, BridgeProtocol::DiscoveryEntrySize);
        ::ReleaseMutex(DiscoveryMutexHandle);
    }

    RegisteredEntryIndex = -1;
    UE_LOG(LogTemp, Log, TEXT("[UEB] Discovery 등록 해제 완료"));
}

void FBridgeDiscoveryRegistrar::UpdateHeartbeat()
{
    if (RegisteredEntryIndex < 0 || !DiscoveryView) return;

    uint32 Offset = BridgeHeaderLayout::GetEntryAbsoluteOffset(RegisteredEntryIndex);
    int64 NowTicks = FDateTime::UtcNow().GetTicks();
    FMemory::Memcpy(
        DiscoveryView + Offset + BridgeHeaderLayout::EntryHeartbeatOffset,
        &NowTicks, sizeof(int64));
}

void FBridgeDiscoveryRegistrar::CleanupStaleEntries(int64 MaxAgeTicks)
{
    if (!DiscoveryView || !DiscoveryMutexHandle) return;

    DWORD WaitResult = WaitForSingleObject(DiscoveryMutexHandle, 100);
    if (WaitResult != WAIT_OBJECT_0 && WaitResult != WAIT_ABANDONED) return;

    int64 NowTicks = FDateTime::UtcNow().GetTicks();

    uint32 EntryCount = 0;
    FMemory::Memcpy(&EntryCount, DiscoveryView + BridgeHeaderLayout::DiscEntryCountOffset, sizeof(uint32));

    for (uint32 i = 0; i < EntryCount && i < BridgeProtocol::DiscoveryMaxEntries; i++)
    {
        if (static_cast<int32>(i) == RegisteredEntryIndex) continue;

        uint32 Offset = BridgeHeaderLayout::GetEntryAbsoluteOffset(i);
        uint32 EntryPid = 0;
        FMemory::Memcpy(&EntryPid, DiscoveryView + Offset + BridgeHeaderLayout::EntryPidOffset, sizeof(uint32));
        if (EntryPid == 0) continue;

        int64 Heartbeat = 0;
        FMemory::Memcpy(&Heartbeat, DiscoveryView + Offset + BridgeHeaderLayout::EntryHeartbeatOffset, sizeof(int64));

        if ((NowTicks - Heartbeat) > MaxAgeTicks)
        {
            FMemory::Memzero(DiscoveryView + Offset, BridgeProtocol::DiscoveryEntrySize);
        }
    }

    ::ReleaseMutex(DiscoveryMutexHandle);
}
