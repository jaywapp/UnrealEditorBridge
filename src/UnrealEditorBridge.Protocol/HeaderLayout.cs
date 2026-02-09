namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// MMF Header 영역의 필드 오프셋 및 크기를 정의한다.
    /// 모든 정수 필드는 Little-Endian 바이트 순서를 사용한다.
    /// C++ BridgeHeaderLayout과 동일한 레이아웃을 유지해야 한다.
    /// </summary>
    public static class HeaderLayout
    {
        #region Const Fields

        /// <summary>Magic 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int MagicOffset = 0x00;

        /// <summary>ProtocolVersion 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int ProtocolVersionOffset = 0x04;

        /// <summary>WriterPid 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int WriterPidOffset = 0x08;

        /// <summary>Heartbeat 필드 오프셋 (int64, 8 bytes). UTC Ticks.</summary>
        public const int HeartbeatOffset = 0x10;

        /// <summary>SnapshotVersion 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int SnapshotVersionOffset = 0x18;

        /// <summary>SnapshotSize 필드 오프셋 (uint32, 4 bytes). Snapshot 데이터 실제 크기.</summary>
        public const int SnapshotSizeOffset = 0x1C;

        /// <summary>SnapshotCrc32 필드 오프셋 (uint32, 4 bytes). CRC32 체크섬.</summary>
        public const int SnapshotCrc32Offset = 0x20;

        /// <summary>EventWriteIndex 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int EventWriteIndexOffset = 0x28;

        /// <summary>EventSequenceNumber 필드 오프셋 (uint64, 8 bytes).</summary>
        public const int EventSequenceNumberOffset = 0x30;

        #endregion
    }
}
