using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// UnrealEditorBridge의 핵심 Public API.
    /// MMF 기반 IPC를 통해 Unreal Editor와 통신하는 클라이언트 인터페이스이다.
    /// 모든 이벤트는 백그라운드 스레드에서 발행되므로 UI 스레드 마샬링은 소비자 책임이다.
    /// </summary>
    public interface IBridgeClient : IDisposable
    {
        #region Properties

        /// <summary>현재 연결 상태.</summary>
        ConnectionState State { get; }

        /// <summary>
        /// 가장 최근 수신한 에셋 스냅샷. 연결 전이면 null.
        /// 스레드 안전하게 접근 가능 (volatile 참조 교체).
        /// </summary>
        AssetSnapshot? CurrentSnapshot { get; }

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 Editor 인스턴스에 연결한다.
        /// MMF를 열고, Header를 검증하고, 초기 Snapshot을 읽고, 백그라운드 모니터링을 시작한다.
        /// </summary>
        /// <param name="mmfName">MMF 이름 (예: "UEB_MyGame_12345").</param>
        /// <param name="ct">취소 토큰.</param>
        /// <exception cref="InvalidOperationException">이미 연결된 상태에서 호출한 경우.</exception>
        /// <exception cref="InvalidOperationException">Major 버전 불일치 시.</exception>
        Task ConnectAsync(string mmfName, CancellationToken ct = default);

        /// <summary>
        /// 연결을 종료하고 모든 자원을 정리한다.
        /// 백그라운드 스레드를 중지하고 IPC 핸들을 해제한다.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 강제로 Snapshot을 다시 읽는다.
        /// </summary>
        /// <param name="ct">취소 토큰.</param>
        /// <returns>갱신된 스냅샷.</returns>
        Task<AssetSnapshot> RefreshSnapshotAsync(CancellationToken ct = default);

        #endregion

        #region Events

        /// <summary>새 Snapshot이 수신되었을 때 발생한다. 백그라운드 스레드에서 호출된다.</summary>
        event EventHandler<SnapshotReceivedEventArgs>? SnapshotReceived;

        /// <summary>에셋 이벤트가 수신되었을 때 발생한다. 백그라운드 스레드에서 호출된다.</summary>
        event EventHandler<AssetEventReceivedEventArgs>? EventReceived;

        /// <summary>연결 상태가 변경되었을 때 발생한다. 백그라운드 스레드에서 호출된다.</summary>
        event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>Ring Buffer 오버플로가 감지되었을 때 발생한다.</summary>
        event EventHandler<EventOverflowEventArgs>? EventOverflow;

        #endregion
    }
}
