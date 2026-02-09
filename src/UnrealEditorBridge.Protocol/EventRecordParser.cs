using System.Buffers.Binary;
using System.Text;

namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// Event Ring Buffer의 개별 슬롯 데이터를 파싱하는 유틸리티.
    /// C++ BridgeIpcWriter가 기록하는 슬롯 레이아웃에 맞춰 파싱한다.
    /// </summary>
    public static class EventRecordParser
    {
        #region Functions

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 SequenceNumber를 읽는다.
        /// </summary>
        public static ulong ReadSequenceNumber(ReadOnlySpan<byte> slotData)
            => BinaryPrimitives.ReadUInt64LittleEndian(slotData[EventSlotLayout.SequenceNumberOffset..]);

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 EventType을 읽는다.
        /// </summary>
        public static AssetEventType ReadEventType(ReadOnlySpan<byte> slotData)
            => (AssetEventType)BinaryPrimitives.ReadUInt32LittleEndian(slotData[EventSlotLayout.EventTypeOffset..]);

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 PayloadSize를 읽는다.
        /// </summary>
        public static uint ReadPayloadSize(ReadOnlySpan<byte> slotData)
            => BinaryPrimitives.ReadUInt32LittleEndian(slotData[EventSlotLayout.PayloadSizeOffset..]);

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 PayloadCrc32를 읽는다.
        /// </summary>
        public static uint ReadPayloadCrc32(ReadOnlySpan<byte> slotData)
            => BinaryPrimitives.ReadUInt32LittleEndian(slotData[EventSlotLayout.PayloadCrc32Offset..]);

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 JSON 페이로드 문자열을 읽는다.
        /// </summary>
        /// <param name="slotData">슬롯 바이트 데이터.</param>
        /// <returns>JSON 페이로드 문자열. 페이로드 크기가 0이면 빈 문자열.</returns>
        public static string ReadPayloadJson(ReadOnlySpan<byte> slotData)
        {
            var payloadSize = (int)ReadPayloadSize(slotData);
            if (payloadSize <= 0)
                return string.Empty;

            var maxPayload = slotData.Length - EventSlotLayout.PayloadOffset;
            if (payloadSize > maxPayload)
                payloadSize = maxPayload;

            return Encoding.UTF8.GetString(slotData.Slice(EventSlotLayout.PayloadOffset, payloadSize));
        }

        /// <summary>
        /// 슬롯이 유효한 이벤트 데이터를 포함하는지 확인한다.
        /// SequenceNumber가 0이 아니고 EventType이 None이 아니면 유효하다.
        /// </summary>
        public static bool IsValidSlot(ReadOnlySpan<byte> slotData)
        {
            var seq = ReadSequenceNumber(slotData);
            var eventType = ReadEventType(slotData);
            return seq > 0 && eventType != AssetEventType.None;
        }

        #endregion
    }
}
