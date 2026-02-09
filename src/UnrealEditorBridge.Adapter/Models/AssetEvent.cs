using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Models
{

    /// <summary>
    /// Event Ring Buffer에서 읽은 개별 에셋 이벤트.
    /// </summary>
    public sealed class AssetEvent
    {
        #region Properties

        /// <summary>전역 시퀀스 번호 (monotonic 증가).</summary>
        public ulong SequenceNumber { get; init; }

        /// <summary>이벤트 발생 시각 (UTC).</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>이벤트 타입.</summary>
        public AssetEventType EventType { get; init; }

        /// <summary>대상 에셋의 오브젝트 경로.</summary>
        public string ObjectPath { get; init; } = string.Empty;

        /// <summary>에셋 이름. 이벤트 타입에 따라 null일 수 있다.</summary>
        public string? AssetName { get; init; }

        /// <summary>에셋 클래스명. 이벤트 타입에 따라 null일 수 있다.</summary>
        public string? ClassName { get; init; }

        /// <summary>이전 오브젝트 경로. Rename/Move 이벤트에서만 유효하다.</summary>
        public string? OldObjectPath { get; init; }

        /// <summary>이전 에셋 이름. Rename 이벤트에서만 유효하다.</summary>
        public string? OldAssetName { get; init; }

        /// <summary>원본 JSON 페이로드 문자열.</summary>
        public string RawPayloadJson { get; init; } = string.Empty;

        #endregion
    }
}
