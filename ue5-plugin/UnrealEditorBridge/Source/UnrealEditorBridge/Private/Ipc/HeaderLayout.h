// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

/**
 * MMF Header 영역의 필드 오프셋 정의.
 * .NET 측 HeaderLayout.cs와 동일한 레이아웃을 유지해야 한다.
 */
namespace BridgeHeaderLayout
{
    // Header fields (offset within 256-byte header)
    static constexpr uint32 MagicOffset           = 0x00;  // uint32
    static constexpr uint32 ProtocolVersionOffset  = 0x04;  // uint32
    static constexpr uint32 WriterPidOffset        = 0x08;  // uint32
    static constexpr uint32 HeartbeatOffset        = 0x10;  // int64 (ticks)
    static constexpr uint32 SnapshotVersionOffset  = 0x18;  // uint32
    static constexpr uint32 SnapshotSizeOffset     = 0x1C;  // uint32
    static constexpr uint32 SnapshotCrc32Offset    = 0x20;  // uint32
    static constexpr uint32 EventWriteIndexOffset  = 0x28;  // uint32
    static constexpr uint32 EventSequenceOffset    = 0x30;  // uint64

    // Event Slot fields (offset within 2048-byte slot)
    static constexpr uint32 SlotSequenceOffset     = 0x00;  // uint64
    static constexpr uint32 SlotEventTypeOffset    = 0x08;  // uint32
    static constexpr uint32 SlotPayloadSizeOffset  = 0x0C;  // uint32
    static constexpr uint32 SlotPayloadCrc32Offset = 0x10;  // uint32
    static constexpr uint32 SlotPayloadOffset      = 0x18;  // variable

    // Discovery Header fields
    static constexpr uint32 DiscMagicOffset        = 0x00;  // uint32
    static constexpr uint32 DiscEntryCountOffset   = 0x04;  // uint32

    // Discovery Entry fields (offset within 512-byte entry)
    static constexpr uint32 EntryPidOffset         = 0x00;  // uint32
    static constexpr uint32 EntryRegisteredAtOffset = 0x08; // int64
    static constexpr uint32 EntryHeartbeatOffset   = 0x10;  // int64
    static constexpr uint32 EntryProjectNameOffset = 0x20;  // UTF-8, 128 bytes
    static constexpr uint32 EntryProjectNameSize   = 128;
    static constexpr uint32 EntryMmfNameOffset     = 0xA0;  // UTF-8, 256 bytes
    static constexpr uint32 EntryMmfNameSize       = 256;

    /** Get absolute offset of event slot i from MMF base. */
    inline uint32 GetSlotAbsoluteOffset(uint32 SlotIndex)
    {
        return BridgeProtocol::EventRingOffset +
               (SlotIndex * BridgeProtocol::EventSlotSize);
    }

    /** Get absolute offset of discovery entry i from Discovery MMF base. */
    inline uint32 GetEntryAbsoluteOffset(uint32 EntryIndex)
    {
        return BridgeProtocol::DiscoveryHeaderSize +
               (EntryIndex * BridgeProtocol::DiscoveryEntrySize);
    }
}
