namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// Event Ring Buffer 내 개별 슬롯의 필드 오프셋을 정의한다.
    /// C++ BridgeHeaderLayout의 Slot 영역과 동일한 레이아웃을 유지해야 한다.
    /// </summary>
    public static class EventSlotLayout
    {
        #region Const Fields

        /// <summary>SequenceNumber 필드 오프셋 (uint64, 8 bytes). 슬롯 시작 기준 상대 오프셋.</summary>
        public const int SequenceNumberOffset = 0x00;

        /// <summary>EventType 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int EventTypeOffset = 0x08;

        /// <summary>PayloadSize 필드 오프셋 (uint32, 4 bytes).</summary>
        public const int PayloadSizeOffset = 0x0C;

        /// <summary>PayloadCrc32 필드 오프셋 (uint32, 4 bytes). CRC32 체크섬.</summary>
        public const int PayloadCrc32Offset = 0x10;

        /// <summary>Payload 시작 오프셋. JSON 데이터 (UTF-8).</summary>
        public const int PayloadOffset = 0x18;

        /// <summary>슬롯 내 고정 헤더 크기 (바이트).</summary>
        public const int RecordHeaderSize = 24;

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 슬롯 크기에서 페이로드 최대 크기를 계산한다.
        /// </summary>
        /// <param name="slotSize">슬롯 전체 크기 (바이트).</param>
        /// <returns>페이로드 최대 크기.</returns>
        public static int GetMaxPayloadSize(int slotSize) => slotSize - RecordHeaderSize;

        /// <summary>
        /// Ring Buffer 내에서 특정 슬롯의 절대 오프셋을 계산한다.
        /// </summary>
        /// <param name="ringBufferOffset">Ring Buffer 시작 오프셋 (Header로부터).</param>
        /// <param name="slotIndex">슬롯 인덱스 (0-based).</param>
        /// <param name="slotSize">슬롯 크기 (바이트).</param>
        /// <returns>MMF 내 절대 오프셋.</returns>
        public static long GetSlotAbsoluteOffset(int ringBufferOffset, int slotIndex, int slotSize)
            => ringBufferOffset + (long)slotIndex * slotSize;

        #endregion
    }
}
