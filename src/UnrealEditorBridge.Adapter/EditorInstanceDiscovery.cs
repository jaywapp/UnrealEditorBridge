using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// Discovery MMF를 읽어 현재 실행 중인 UE Editor 인스턴스를 탐색한다.
    /// 폴링 기반(2초 간격)으로 인스턴스 목록 변경을 감시할 수 있다.
    /// </summary>
    public sealed class EditorInstanceDiscovery : IDisposable
    {
        #region Internal Fields

        private Timer? _watchTimer;
        private List<EditorInstanceInfo> _lastInstances = new();
        private bool _disposed;

        #endregion

        #region Events

        /// <summary>인스턴스 목록이 변경되었을 때 발생한다.</summary>
        public event EventHandler<EventArgs>? InstancesChanged;

        #endregion

        #region Functions

        /// <summary>
        /// Discovery MMF에서 현재 활성 Editor 인스턴스 목록을 읽어 반환한다.
        /// Discovery MMF가 존재하지 않으면 빈 목록을 반환한다.
        /// </summary>
        /// <returns>활성 인스턴스 정보 목록.</returns>
        public IReadOnlyList<EditorInstanceInfo> GetActiveInstances()
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(
                    ProtocolConstants.DiscoveryMmfName, MemoryMappedFileRights.Read);
                using var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                var headerBuf = new byte[ProtocolConstants.DiscoveryHeaderSize];
                view.ReadArray(0, headerBuf, 0, headerBuf.Length);

                var magic = BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.AsSpan(DiscoveryLayout.MagicOffset));
                if (magic != ProtocolConstants.DiscoveryMagic)
                    return Array.Empty<EditorInstanceInfo>();

                var entryCount = BinaryPrimitives.ReadUInt32LittleEndian(
                    headerBuf.AsSpan(DiscoveryLayout.EntryCountOffset));

                var results = new List<EditorInstanceInfo>();
                var entryBuf = new byte[ProtocolConstants.DiscoveryEntrySize];

                for (int i = 0; i < entryCount && i < ProtocolConstants.DiscoveryMaxEntries; i++)
                {
                    var offset = DiscoveryLayout.GetEntryAbsoluteOffset(i);
                    view.ReadArray(offset, entryBuf, 0, entryBuf.Length);

                    var pid = BinaryPrimitives.ReadUInt32LittleEndian(
                        entryBuf.AsSpan(DiscoveryLayout.EntryProcessIdOffset));

                    if (pid == 0) continue; // 빈 엔트리

                    var registeredAt = BinaryPrimitives.ReadInt64LittleEndian(
                        entryBuf.AsSpan(DiscoveryLayout.EntryRegisteredAtOffset));
                    var heartbeat = BinaryPrimitives.ReadInt64LittleEndian(
                        entryBuf.AsSpan(DiscoveryLayout.EntryLastHeartbeatOffset));

                    var projectName = ReadNullTerminatedUtf8(
                        entryBuf.AsSpan(DiscoveryLayout.EntryProjectNameOffset, DiscoveryLayout.EntryProjectNameSize));
                    var mmfName = ReadNullTerminatedUtf8(
                        entryBuf.AsSpan(DiscoveryLayout.EntryMmfNameOffset, DiscoveryLayout.EntryMmfNameSize));

                    results.Add(new EditorInstanceInfo
                    {
                        ProcessId = pid,
                        ProjectName = projectName,
                        MmfName = mmfName,
                        RegisteredAt = new DateTime(registeredAt, DateTimeKind.Utc),
                        LastHeartbeat = new DateTime(heartbeat, DateTimeKind.Utc)
                    });
                }

                return results;
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<EditorInstanceInfo>();
            }
            catch
            {
                return Array.Empty<EditorInstanceInfo>();
            }
        }

        /// <summary>
        /// 인스턴스 목록 변경 감시를 시작한다 (2초 간격 폴링).
        /// </summary>
        public void StartWatching()
        {
            _watchTimer?.Dispose();
            _watchTimer = new Timer(OnWatchTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// 인스턴스 목록 변경 감시를 중지한다.
        /// </summary>
        public void StopWatching()
        {
            _watchTimer?.Dispose();
            _watchTimer = null;
        }

        #endregion

        #region Event Handlers

        private void OnWatchTimerTick(object? state)
        {
            var current = GetActiveInstances();
            if (!AreListsEqual(_lastInstances, current))
            {
                _lastInstances = current.ToList();
                InstancesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Helper Methods

        private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> buffer)
        {
            int nullIndex = buffer.IndexOf((byte)0);
            if (nullIndex <= 0) return string.Empty;
            return Encoding.UTF8.GetString(buffer[..nullIndex]);
        }

        private static bool AreListsEqual(
            IReadOnlyList<EditorInstanceInfo> a,
            IReadOnlyList<EditorInstanceInfo> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].ProcessId != b[i].ProcessId ||
                    a[i].MmfName != b[i].MmfName)
                    return false;
            }
            return true;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _watchTimer?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
