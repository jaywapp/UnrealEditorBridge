namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// Editor 연결 상태를 나타내는 열거형.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>연결되지 않은 초기 상태.</summary>
        Disconnected,

        /// <summary>연결 시도 중.</summary>
        Connecting,

        /// <summary>정상 연결 상태.</summary>
        Connected,

        /// <summary>Heartbeat 미수신. Editor 응답 없음 의심.</summary>
        Lost,

        /// <summary>프로토콜 버전 불일치로 연결 불가.</summary>
        VersionMismatch,

        /// <summary>오류로 인한 연결 실패.</summary>
        Error
    }
}
