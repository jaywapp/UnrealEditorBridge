using UnrealEditorBridge.Adapter.Internal.Serialization;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Internal.Ipc
{

    /// <summary>
    /// Event Ring Buffer에서 이벤트를 읽는 Reader.
    /// 로컬 ReadIndex를 추적하고, 새 이벤트가 있으면 순차적으로 읽어온다.
    /// </summary>
    internal sealed class EventRingReader
    {
        #region Internal Fields

        private readonly MmfAccessor _mmf;
        private ulong _lastReadSequence;
        private uint _localReadIndex;

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="EventRingReader"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public EventRingReader(MmfAccessor mmf)
        {
            _mmf = mmf;
        }

        #endregion

        #region Functions

        /// <summary>
        /// 현재 Writer 위치와 동기화하여 읽기 위치를 초기화한다.
        /// 연결 시점에 호출하여 기존 이벤트를 건너뛴다.
        /// </summary>
        /// <param name="header">현재 Header 데이터.</param>
        public void Initialize(HeaderData header)
        {
            _localReadIndex = header.EventWriteIndex;
            _lastReadSequence = header.EventSequenceNumber;
        }

        /// <summary>
        /// Ring Buffer에서 새 이벤트를 모두 읽어 반환한다.
        /// Mutex 보호 하에 바이트를 복사하고 Mutex 밖에서 역직렬화해야 한다.
        /// </summary>
        /// <param name="header">현재 Header 데이터.</param>
        /// <param name="overflowDetected">오버플로가 감지되었는지 여부.</param>
        /// <param name="missedCount">누락된 이벤트 수 (오버플로 시).</param>
        /// <returns>새 이벤트 목록.</returns>
        public List<AssetEvent> ReadNewEvents(
            HeaderData header,
            out bool overflowDetected,
            out ulong missedCount)
        {
            overflowDetected = false;
            missedCount = 0;

            var events = new List<AssetEvent>();
            var writeIndex = header.EventWriteIndex;
            var slotCount = (uint)ProtocolConstants.DefaultEventSlotCount;
            var slotSize = ProtocolConstants.DefaultEventSlotSize;
            var ringOffset = ProtocolConstants.DefaultEventRingOffset;

            if (_localReadIndex == writeIndex)
                return events; // 새 이벤트 없음

            // 읽어야 할 슬롯 수 계산
            var slotsToRead = (writeIndex >= _localReadIndex)
                ? writeIndex - _localReadIndex
                : slotCount - _localReadIndex + writeIndex;

            // 오버플로 감지: 슬롯 수가 전체 Ring Buffer를 초과하면
            if (slotsToRead > slotCount)
            {
                overflowDetected = true;
                missedCount = slotsToRead - slotCount;
                _localReadIndex = writeIndex; // 리셋
                _lastReadSequence = header.EventSequenceNumber;
                return events;
            }

            for (uint i = 0; i < slotsToRead; i++)
            {
                var slotIndex = (int)((_localReadIndex + i) % slotCount);
                var absoluteOffset = EventSlotLayout.GetSlotAbsoluteOffset(ringOffset, slotIndex, slotSize);
                var slotData = _mmf.ReadBytes(absoluteOffset, slotSize);

                if (!EventRecordParser.IsValidSlot(slotData))
                    continue;

                var seqNum = EventRecordParser.ReadSequenceNumber(slotData);

                // SequenceNumber 연속성 검증
                if (_lastReadSequence > 0 && seqNum != _lastReadSequence + 1)
                {
                    // Gap 감지 = 오버플로
                    overflowDetected = true;
                    missedCount = seqNum - _lastReadSequence - 1;
                    _localReadIndex = writeIndex;
                    _lastReadSequence = header.EventSequenceNumber;
                    return events;
                }

                var evt = JsonEventDeserializer.Deserialize(slotData);
                events.Add(evt);
                _lastReadSequence = seqNum;
            }

            _localReadIndex = writeIndex;
            return events;
        }

        /// <summary>
        /// 읽기 위치를 현재 Writer 위치로 강제 리셋한다.
        /// 오버플로 복구 시 Snapshot 재요청과 함께 호출한다.
        /// </summary>
        public void Reset(HeaderData header)
        {
            _localReadIndex = header.EventWriteIndex;
            _lastReadSequence = header.EventSequenceNumber;
        }

        #endregion
    }
}
