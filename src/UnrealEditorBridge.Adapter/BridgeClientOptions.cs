namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// <see cref="IBridgeClient"/> 생성 시 사용하는 옵션.
    /// </summary>
    public sealed class BridgeClientOptions
    {
        #region Properties

        /// <summary>Heartbeat 체크 간격. 기본값: 2초.</summary>
        public TimeSpan HeartbeatCheckInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>Heartbeat 타임아웃. 이 시간 동안 미갱신 시 Lost 상태로 전이. 기본값: 5초.</summary>
        public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Lost 상태 최대 유지 시간. 초과 시 Disconnected로 전이. 기본값: 30초.</summary>
        public TimeSpan MaxLostDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>이벤트 폴링 간격 (Named Event 대기 타임아웃). 기본값: 1초.</summary>
        public TimeSpan EventPollInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Snapshot 자동 갱신 여부. 기본값: true.</summary>
        public bool AutoRefreshSnapshot { get; set; } = true;

        /// <summary>Overflow 감지 시 자동 Snapshot 재요청 여부. 기본값: true.</summary>
        public bool AutoRecoverOnOverflow { get; set; } = true;

        #endregion
    }
}
