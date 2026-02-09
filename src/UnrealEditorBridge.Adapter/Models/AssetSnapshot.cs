namespace UnrealEditorBridge.Adapter.Models
{

    /// <summary>
    /// 에셋 전체 상태 스냅샷. Snapshot 영역의 JSON 데이터를 역직렬화한 결과이다.
    /// 불변(immutable) 객체로 스레드 안전하게 공유할 수 있다.
    /// </summary>
    public sealed class AssetSnapshot
    {
        #region Properties

        /// <summary>Snapshot 생성 시각 (UTC).</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>에셋 총 개수.</summary>
        public int AssetCount { get; init; }

        /// <summary>에셋 목록.</summary>
        public IReadOnlyList<AssetInfo> Assets { get; init; } = Array.Empty<AssetInfo>();

        /// <summary>Snapshot 버전 카운터 (Header의 SnapshotVersion).</summary>
        public uint Version { get; init; }

        #endregion
    }
}
