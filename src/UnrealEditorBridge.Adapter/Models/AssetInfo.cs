namespace UnrealEditorBridge.Adapter.Models
{

    /// <summary>
    /// 개별 에셋의 메타데이터 정보.
    /// Snapshot JSON의 assets 배열 항목을 역직렬화한 결과이다.
    /// </summary>
    public sealed class AssetInfo
    {
        #region Properties

        /// <summary>에셋의 전체 오브젝트 경로 (예: "/Game/Characters/Hero/SK_Hero.SK_Hero").</summary>
        public string ObjectPath { get; init; } = string.Empty;

        /// <summary>패키지 디렉토리 경로 (예: "/Game/Characters/Hero").</summary>
        public string PackagePath { get; init; } = string.Empty;

        /// <summary>에셋 이름 (예: "SK_Hero").</summary>
        public string AssetName { get; init; } = string.Empty;

        /// <summary>에셋 클래스 경로 (예: "/Script/Engine.SkeletalMesh").</summary>
        public string ClassName { get; init; } = string.Empty;

        /// <summary>에셋 태그 (키-값 쌍).</summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

        /// <summary>에셋 의존성 정보.</summary>
        public AssetDependencyInfo Dependencies { get; init; } = new();

        #endregion
    }
}
