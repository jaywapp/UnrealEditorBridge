using System.Buffers.Binary;

namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// 바이트 배열로부터 Header 데이터를 파싱하는 유틸리티.
    /// Little-Endian 바이트 순서를 사용한다.
    /// </summary>
    public static class HeaderParser
    {
        #region Functions

        /// <summary>
        /// 바이트 버퍼에서 Header 전체를 파싱하여 <see cref="HeaderData"/>를 생성한다.
        /// </summary>
        /// <param name="buffer">최소 <see cref="ProtocolConstants.HeaderSize"/> 바이트 이상의 버퍼.</param>
        /// <returns>파싱된 Header 데이터.</returns>
        /// <exception cref="ArgumentException">버퍼가 Header 크기보다 작은 경우.</exception>
        public static HeaderData Parse(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < ProtocolConstants.HeaderSize)
                throw new ArgumentException(
                    $"Buffer size ({buffer.Length}) is smaller than header size ({ProtocolConstants.HeaderSize}).");

            return new HeaderData
            {
                Magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.MagicOffset..]),
                ProtocolVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.ProtocolVersionOffset..]),
                WriterPid = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.WriterPidOffset..]),
                Heartbeat = BinaryPrimitives.ReadInt64LittleEndian(buffer[HeaderLayout.HeartbeatOffset..]),
                SnapshotVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.SnapshotVersionOffset..]),
                SnapshotSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.SnapshotSizeOffset..]),
                SnapshotCrc32 = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.SnapshotCrc32Offset..]),
                EventWriteIndex = BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.EventWriteIndexOffset..]),
                EventSequenceNumber = BinaryPrimitives.ReadUInt64LittleEndian(buffer[HeaderLayout.EventSequenceNumberOffset..])
            };
        }

        /// <summary>
        /// 바이트 버퍼에서 Heartbeat 값만 빠르게 읽는다.
        /// 전체 Header 파싱 없이 Heartbeat만 확인할 때 사용한다.
        /// </summary>
        /// <param name="buffer">최소 0x18 바이트 이상의 버퍼.</param>
        /// <returns>Heartbeat UTC Ticks 값.</returns>
        public static long ReadHeartbeat(ReadOnlySpan<byte> buffer)
            => BinaryPrimitives.ReadInt64LittleEndian(buffer[HeaderLayout.HeartbeatOffset..]);

        /// <summary>
        /// 바이트 버퍼에서 SnapshotVersion 값만 빠르게 읽는다.
        /// </summary>
        public static uint ReadSnapshotVersion(ReadOnlySpan<byte> buffer)
            => BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.SnapshotVersionOffset..]);

        /// <summary>
        /// 바이트 버퍼에서 EventWriteIndex 값만 빠르게 읽는다.
        /// </summary>
        public static uint ReadEventWriteIndex(ReadOnlySpan<byte> buffer)
            => BinaryPrimitives.ReadUInt32LittleEndian(buffer[HeaderLayout.EventWriteIndexOffset..]);

        /// <summary>
        /// 바이트 버퍼에서 EventSequenceNumber 값만 빠르게 읽는다.
        /// </summary>
        public static ulong ReadEventSequenceNumber(ReadOnlySpan<byte> buffer)
            => BinaryPrimitives.ReadUInt64LittleEndian(buffer[HeaderLayout.EventSequenceNumberOffset..]);

        #endregion
    }
}
