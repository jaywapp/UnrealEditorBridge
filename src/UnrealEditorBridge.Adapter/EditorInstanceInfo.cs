namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// Discovery MMF에서 읽은 개별 Editor 인스턴스 정보.
    /// </summary>
    public sealed class EditorInstanceInfo
    {
        #region Properties

        /// <summary>Editor 프로세스 ID.</summary>
        public uint ProcessId { get; init; }

        /// <summary>Unreal 프로젝트 이름.</summary>
        public string ProjectName { get; init; } = string.Empty;

        /// <summary>이 인스턴스의 MMF 이름 (연결 시 사용).</summary>
        public string MmfName { get; init; } = string.Empty;

        /// <summary>인스턴스 등록 시각 (UTC).</summary>
        public DateTime RegisteredAt { get; init; }

        /// <summary>마지막 Heartbeat 시각 (UTC).</summary>
        public DateTime LastHeartbeat { get; init; }

        /// <summary>
        /// 인스턴스가 활성 상태인지 확인한다. LastHeartbeat이 5초 이내이면 활성.
        /// </summary>
        public bool IsAlive => (DateTime.UtcNow - LastHeartbeat).TotalSeconds <= 5.0;

        #endregion

        #region Functions

        /// <summary>사용자 표시용 문자열을 반환한다.</summary>
        public override string ToString() => $"{ProjectName} (PID: {ProcessId})";

        #endregion
    }
}
