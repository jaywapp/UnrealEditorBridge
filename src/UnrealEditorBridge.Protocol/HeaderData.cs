namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// MMF Header 영역을 구조화된 형태로 읽어 담는 데이터 클래스.
    /// C++ BridgeIpcWriter가 기록하는 필드만 포함한다.
    /// </summary>
    public sealed class HeaderData
    {
        #region Properties

        /// <summary>매직 넘버. 유효한 값은 <see cref="ProtocolConstants.Magic"/>.</summary>
        public uint Magic { get; init; }

        /// <summary>프로토콜 버전 (Major * 1000 + Minor).</summary>
        public uint ProtocolVersion { get; init; }

        /// <summary>Writer(Editor) 프로세스 ID.</summary>
        public uint WriterPid { get; init; }

        /// <summary>마지막 Writer 활동 시각 (UTC Ticks).</summary>
        public long Heartbeat { get; init; }

        /// <summary>Snapshot 버전 카운터.</summary>
        public uint SnapshotVersion { get; init; }

        /// <summary>현재 Snapshot 데이터 실제 크기 (바이트).</summary>
        public uint SnapshotSize { get; init; }

        /// <summary>Snapshot 데이터의 CRC32 체크섬.</summary>
        public uint SnapshotCrc32 { get; init; }

        /// <summary>다음 이벤트 기록 위치 (0-based, wrap-around).</summary>
        public uint EventWriteIndex { get; init; }

        /// <summary>전역 이벤트 시퀀스 번호 (monotonic 증가).</summary>
        public ulong EventSequenceNumber { get; init; }

        #endregion

        #region Functions

        /// <summary>
        /// Magic 필드가 유효한지 확인한다.
        /// </summary>
        public bool IsValidMagic() => Magic == ProtocolConstants.Magic;

        /// <summary>
        /// 현재 Adapter가 지원하는 Major 버전과 호환되는지 확인한다.
        /// </summary>
        public bool IsMajorCompatible()
            => ProtocolConstants.IsMajorCompatible(ProtocolConstants.ProtocolVersion, ProtocolVersion);

        /// <summary>
        /// Heartbeat이 지정 시간 이내인지 확인한다.
        /// </summary>
        /// <param name="timeout">타임아웃 시간.</param>
        /// <returns>Heartbeat이 타임아웃 이내이면 true.</returns>
        public bool IsHeartbeatAlive(TimeSpan timeout)
        {
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Heartbeat);
            return elapsed <= timeout;
        }

        #endregion
    }
}
