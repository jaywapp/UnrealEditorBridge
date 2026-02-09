using System.Text;
using UnrealEditorBridge.Adapter.Internal.Serialization;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Internal.Ipc
{

    /// <summary>
    /// MMF의 Snapshot 영역을 읽고 역직렬화한다.
    /// Mutex 보호 하에 바이트를 복사(copy-out)한 후 Mutex 밖에서 역직렬화한다.
    /// </summary>
    internal sealed class SnapshotReader
    {
        #region Internal Fields

        private readonly MmfAccessor _mmf;

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="SnapshotReader"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public SnapshotReader(MmfAccessor mmf)
        {
            _mmf = mmf;
        }

        #endregion

        #region Functions

        /// <summary>
        /// Snapshot 데이터를 읽고 CRC32 검증 후 역직렬화한다.
        /// 호출자는 Mutex를 획득한 상태에서 바이트를 복사하고,
        /// Mutex 해제 후 이 메서드 결과를 사용해야 한다.
        /// </summary>
        /// <param name="header">현재 Header 데이터 (Snapshot 오프셋/크기/체크섬 참조).</param>
        /// <returns>역직렬화된 스냅샷. CRC 불일치 시 null.</returns>
        public AssetSnapshot? ReadSnapshot(HeaderData header)
        {
            if (header.SnapshotSize == 0)
                return null;

            var snapshotBytes = _mmf.ReadBytes(
                ProtocolConstants.DefaultSnapshotOffset, (int)header.SnapshotSize);

            // CRC32 검증
            var computedCrc = Crc32.Compute(snapshotBytes);
            if (computedCrc != header.SnapshotCrc32)
                return null; // Partial read 감지

            var json = Encoding.UTF8.GetString(snapshotBytes);

            // null-terminator 제거
            var nullIdx = json.IndexOf('\0');
            if (nullIdx >= 0)
                json = json[..nullIdx];

            return JsonSnapshotDeserializer.Deserialize(json, header.SnapshotVersion);
        }

        /// <summary>
        /// CRC 검증 포함 재시도 로직으로 Snapshot을 읽는다.
        /// </summary>
        /// <param name="header">Header 데이터.</param>
        /// <param name="mutex">Mutex 핸들. 획득/해제를 내부에서 수행한다.</param>
        /// <param name="maxRetries">최대 재시도 횟수. 기본값 3.</param>
        /// <returns>역직렬화된 스냅샷. 모든 재시도 실패 시 null.</returns>
        public AssetSnapshot? ReadSnapshotWithRetry(HeaderData header, Mutex mutex, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (!mutex.WaitOne(50))
                        continue;

                    try
                    {
                        var snapshot = ReadSnapshot(header);
                        if (snapshot != null)
                            return snapshot;
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Writer가 비정상 종료한 경우 Mutex를 획득한 것으로 처리
                    try
                    {
                        var snapshot = ReadSnapshot(header);
                        mutex.ReleaseMutex();
                        if (snapshot != null)
                            return snapshot;
                    }
                    catch { /* 재시도 */ }
                }
            }

            return null;
        }

        #endregion
    }
}
