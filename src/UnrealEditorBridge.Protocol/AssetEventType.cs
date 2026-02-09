namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// 에셋 이벤트 타입을 정의하는 열거형.
    /// Event Ring Buffer의 EventType 필드에 기록되는 값과 일치한다.
    /// </summary>
    public enum AssetEventType : uint
    {
        /// <summary>빈 슬롯 (초기 상태).</summary>
        None = 0,

        /// <summary>에셋이 새로 생성되었다.</summary>
        AssetCreated = 1,

        /// <summary>에셋이 삭제되었다.</summary>
        AssetDeleted = 2,

        /// <summary>에셋 이름이 변경되었다.</summary>
        AssetRenamed = 3,

        /// <summary>에셋이 저장되었다.</summary>
        AssetSaved = 4,

        /// <summary>에셋 태그가 변경되었다.</summary>
        AssetTagsChanged = 5,

        /// <summary>에셋이 다른 경로로 이동되었다.</summary>
        AssetMoved = 6,

        /// <summary>에셋이 메모리에 로드되었다.</summary>
        AssetLoaded = 7,

        /// <summary>에셋의 의존성이 변경되었다.</summary>
        AssetDependencyChanged = 8,

        /// <summary>전체 Snapshot이 갱신되었음을 알린다.</summary>
        SnapshotUpdated = 100,

        /// <summary>Editor가 정상 종료됨을 알린다.</summary>
        EditorShutdown = 200,

        /// <summary>향후 확장용 예약 값.</summary>
        Reserved = 0xFFFF
    }
}
