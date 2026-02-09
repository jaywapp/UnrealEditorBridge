namespace UnrealEditorBridge.Adapter.Models
{

    /// <summary>
    /// 에셋의 의존성(참조) 정보.
    /// 하드 레퍼런스와 소프트 레퍼런스 경로 목록을 포함한다.
    /// </summary>
    public sealed class AssetDependencyInfo
    {
        #region Properties

        /// <summary>하드 레퍼런스 경로 배열.</summary>
        public IReadOnlyList<string> Hard { get; init; } = Array.Empty<string>();

        /// <summary>소프트 레퍼런스 경로 배열.</summary>
        public IReadOnlyList<string> Soft { get; init; } = Array.Empty<string>();

        #endregion
    }
}
