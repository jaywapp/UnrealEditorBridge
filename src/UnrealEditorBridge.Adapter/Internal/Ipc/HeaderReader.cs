using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Internal.Ipc
{

    /// <summary>
    /// MMF에서 Header 데이터를 읽는 Reader.
    /// MmfAccessor와 동기화 프리미티브를 조합하여 안전하게 Header를 읽는다.
    /// </summary>
    internal sealed class HeaderReader
    {
        #region Internal Fields

        private readonly MmfAccessor _mmf;
        private readonly byte[] _headerBuffer = new byte[ProtocolConstants.HeaderSize];

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="HeaderReader"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="mmf">열려 있는 MMF accessor.</param>
        public HeaderReader(MmfAccessor mmf)
        {
            _mmf = mmf;
        }

        #endregion

        #region Functions

        /// <summary>
        /// Header 전체를 읽어 <see cref="HeaderData"/>로 파싱한다.
        /// Mutex 보호 밖에서도 호출할 수 있으나, 정확한 스냅샷이 필요하면 Mutex 내에서 호출.
        /// </summary>
        /// <returns>파싱된 Header 데이터.</returns>
        public HeaderData ReadHeader()
        {
            _mmf.ReadBytes(0, _headerBuffer, 0, ProtocolConstants.HeaderSize);
            return HeaderParser.Parse(_headerBuffer);
        }

        /// <summary>
        /// Heartbeat 값만 빠르게 읽는다. 전체 Header 파싱보다 가볍다.
        /// </summary>
        /// <returns>Heartbeat UTC Ticks.</returns>
        public long ReadHeartbeat()
        {
            _mmf.ReadBytes(HeaderLayout.HeartbeatOffset, _headerBuffer, 0, 8);
            return HeaderParser.ReadHeartbeat(_headerBuffer);
        }

        /// <summary>
        /// SnapshotVersion 값만 빠르게 읽는다.
        /// </summary>
        public uint ReadSnapshotVersion()
        {
            _mmf.ReadBytes(HeaderLayout.SnapshotVersionOffset, _headerBuffer, 0, 4);
            return HeaderParser.ReadSnapshotVersion(_headerBuffer);
        }

        /// <summary>
        /// EventWriteIndex 값만 빠르게 읽는다.
        /// </summary>
        public uint ReadEventWriteIndex()
        {
            _mmf.ReadBytes(HeaderLayout.EventWriteIndexOffset, _headerBuffer, 0, 4);
            return HeaderParser.ReadEventWriteIndex(_headerBuffer);
        }

        /// <summary>
        /// EventSequenceNumber 값만 빠르게 읽는다.
        /// </summary>
        public ulong ReadEventSequenceNumber()
        {
            _mmf.ReadBytes(HeaderLayout.EventSequenceNumberOffset, _headerBuffer, 0, 8);
            return HeaderParser.ReadEventSequenceNumber(_headerBuffer);
        }

        #endregion
    }
}
