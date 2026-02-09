// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeIpcWriter.h"
#include "BridgeMmfManager.h"
#include "ProtocolConstants.h"
#include "HeaderLayout.h"
#include "../Serialization/BridgeJsonSerializer.h"

void FBridgeIpcWriter::Initialize(FBridgeMmfManager* InMmfManager)
{
    MmfManager = InMmfManager;
    EventSequenceNumber = 0;
    EventWriteIndex = 0;
    SnapshotVersion = 0;
}

void FBridgeIpcWriter::WriteHeader()
{
    if (!MmfManager || !MmfManager->GetBasePointer()) return;

    uint8* Base = MmfManager->GetBasePointer();

    // Magic
    uint32 Magic = BridgeProtocol::Magic;
    FMemory::Memcpy(Base + BridgeHeaderLayout::MagicOffset, &Magic, sizeof(uint32));

    // Protocol Version
    uint32 Version = BridgeProtocol::ProtocolVersion;
    FMemory::Memcpy(Base + BridgeHeaderLayout::ProtocolVersionOffset, &Version, sizeof(uint32));

    // Writer PID
    uint32 Pid = FPlatformProcess::GetCurrentProcessId();
    FMemory::Memcpy(Base + BridgeHeaderLayout::WriterPidOffset, &Pid, sizeof(uint32));

    // 초기 Heartbeat
    UpdateHeartbeat();

    UE_LOG(LogTemp, Log, TEXT("[UEB] Header 기록 완료 (Magic: 0x%08X, Version: %u, PID: %u)"),
        Magic, Version, Pid);
}

void FBridgeIpcWriter::WriteSnapshot(const FBridgeSnapshot& Snapshot)
{
    if (!MmfManager || !MmfManager->GetBasePointer()) return;

    // JSON 직렬화
    FString JsonStr = FBridgeJsonSerializer::SerializeSnapshot(Snapshot);
    FTCHARToUTF8 Utf8Converter(*JsonStr);
    const uint8* Utf8Data = reinterpret_cast<const uint8*>(Utf8Converter.Get());
    uint32 Utf8Size = Utf8Converter.Length();

    // 크기 검증
    if (Utf8Size > BridgeProtocol::SnapshotMaxSize)
    {
        UE_LOG(LogTemp, Warning, TEXT("[UEB] Snapshot 크기 초과: %u > %u. 잘림 발생."),
            Utf8Size, BridgeProtocol::SnapshotMaxSize);
        Utf8Size = BridgeProtocol::SnapshotMaxSize;
    }

    // CRC32 계산
    uint32 Crc = CalculateCrc32(Utf8Data, Utf8Size);

    // Mutex 획득 후 기록
    if (MmfManager->AcquireMutex(200))
    {
        uint8* Base = MmfManager->GetBasePointer();

        // Snapshot 영역에 데이터 기록
        FMemory::Memcpy(Base + BridgeProtocol::SnapshotOffset, Utf8Data, Utf8Size);

        // 나머지 영역 클리어
        if (Utf8Size < BridgeProtocol::SnapshotMaxSize)
        {
            FMemory::Memzero(
                Base + BridgeProtocol::SnapshotOffset + Utf8Size,
                BridgeProtocol::SnapshotMaxSize - Utf8Size);
        }

        // Header 갱신
        SnapshotVersion++;
        FMemory::Memcpy(Base + BridgeHeaderLayout::SnapshotVersionOffset, &SnapshotVersion, sizeof(uint32));
        FMemory::Memcpy(Base + BridgeHeaderLayout::SnapshotSizeOffset, &Utf8Size, sizeof(uint32));
        FMemory::Memcpy(Base + BridgeHeaderLayout::SnapshotCrc32Offset, &Crc, sizeof(uint32));

        MmfManager->ReleaseMutex();
        MmfManager->SignalSnapshotEvent();

        UE_LOG(LogTemp, Log, TEXT("[UEB] Snapshot 기록 완료 (버전: %u, 크기: %u, 에셋: %d)"),
            SnapshotVersion, Utf8Size, Snapshot.AssetCount);
    }
    else
    {
        UE_LOG(LogTemp, Warning, TEXT("[UEB] Snapshot 기록 실패: Mutex 획득 타임아웃"));
    }
}

void FBridgeIpcWriter::WriteEvent(const FBridgeAssetEvent& Event)
{
    if (!MmfManager || !MmfManager->GetBasePointer()) return;

    // JSON 직렬화
    FString JsonStr = FBridgeJsonSerializer::SerializeEvent(Event);
    FTCHARToUTF8 Utf8Converter(*JsonStr);
    const uint8* Utf8Data = reinterpret_cast<const uint8*>(Utf8Converter.Get());
    uint32 Utf8Size = Utf8Converter.Length();

    // 슬롯 페이로드 최대 크기 검증
    uint32 MaxPayload = BridgeProtocol::EventSlotSize - BridgeHeaderLayout::SlotPayloadOffset;
    if (Utf8Size > MaxPayload)
    {
        UE_LOG(LogTemp, Warning, TEXT("[UEB] 이벤트 페이로드 초과: %u > %u"), Utf8Size, MaxPayload);
        Utf8Size = MaxPayload;
    }

    // CRC32
    uint32 Crc = CalculateCrc32(Utf8Data, Utf8Size);

    if (MmfManager->AcquireMutex(100))
    {
        uint8* Base = MmfManager->GetBasePointer();
        EventSequenceNumber++;

        // 슬롯 오프셋 계산
        uint32 SlotOffset = BridgeHeaderLayout::GetSlotAbsoluteOffset(EventWriteIndex);
        uint8* SlotBase = Base + SlotOffset;

        // 슬롯 초기화
        FMemory::Memzero(SlotBase, BridgeProtocol::EventSlotSize);

        // 슬롯 데이터 기록
        FMemory::Memcpy(SlotBase + BridgeHeaderLayout::SlotSequenceOffset, &EventSequenceNumber, sizeof(uint64));
        uint32 EvtType = static_cast<uint32>(Event.EventType);
        FMemory::Memcpy(SlotBase + BridgeHeaderLayout::SlotEventTypeOffset, &EvtType, sizeof(uint32));
        FMemory::Memcpy(SlotBase + BridgeHeaderLayout::SlotPayloadSizeOffset, &Utf8Size, sizeof(uint32));
        FMemory::Memcpy(SlotBase + BridgeHeaderLayout::SlotPayloadCrc32Offset, &Crc, sizeof(uint32));
        FMemory::Memcpy(SlotBase + BridgeHeaderLayout::SlotPayloadOffset, Utf8Data, Utf8Size);

        // Header 갱신
        EventWriteIndex = (EventWriteIndex + 1) % BridgeProtocol::EventSlotCount;
        FMemory::Memcpy(Base + BridgeHeaderLayout::EventWriteIndexOffset, &EventWriteIndex, sizeof(uint32));
        FMemory::Memcpy(Base + BridgeHeaderLayout::EventSequenceOffset, &EventSequenceNumber, sizeof(uint64));

        MmfManager->ReleaseMutex();
        MmfManager->SignalStreamEvent();
    }
}

void FBridgeIpcWriter::UpdateHeartbeat()
{
    if (!MmfManager || !MmfManager->GetBasePointer()) return;

    int64 NowTicks = FDateTime::UtcNow().GetTicks();
    FMemory::Memcpy(
        MmfManager->GetBasePointer() + BridgeHeaderLayout::HeartbeatOffset,
        &NowTicks, sizeof(int64));
}

uint32 FBridgeIpcWriter::CalculateCrc32(const uint8* Data, uint32 Size)
{
    return FCrc::MemCrc32(Data, Size);
}
