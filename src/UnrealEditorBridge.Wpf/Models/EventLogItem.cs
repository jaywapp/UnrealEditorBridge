using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Wpf.Models
{

    /// <summary>
    /// 이벤트 로그 뷰에 표시되는 개별 로그 항목.
    /// </summary>
    public sealed class EventLogItem
    {
        #region Properties

        /// <summary>이벤트 시퀀스 번호.</summary>
        public ulong SequenceNumber { get; init; }

        /// <summary>이벤트 발생 시각.</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>이벤트 타입.</summary>
        public AssetEventType EventType { get; init; }

        /// <summary>대상 에셋 오브젝트 경로.</summary>
        public string ObjectPath { get; init; } = string.Empty;

        /// <summary>로그 표시용 설명 문자열.</summary>
        public string Description { get; init; } = string.Empty;

        #endregion
    }
}
