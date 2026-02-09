using UnrealEditorBridge.Adapter;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Wpf.Services
{

    /// <summary>
    /// ViewModel이 Adapter를 소비하기 위한 서비스 인터페이스.
    /// 백그라운드 스레드 이벤트를 UI 스레드로 마샬링한다.
    /// </summary>
    public interface IBridgeService : IDisposable
    {
        #region Properties

        /// <summary>현재 연결 상태.</summary>
        ConnectionState State { get; }

        /// <summary>가장 최근 수신한 에셋 스냅샷.</summary>
        AssetSnapshot? CurrentSnapshot { get; }

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 MMF 이름으로 Editor에 연결한다.
        /// </summary>
        /// <param name="mmfName">MMF 이름.</param>
        /// <returns>비동기 작업.</returns>
        Task ConnectAsync(string mmfName);

        /// <summary>
        /// 연결을 종료한다.
        /// </summary>
        /// <returns>비동기 작업.</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Snapshot을 강제로 다시 읽는다.
        /// </summary>
        /// <returns>갱신된 스냅샷.</returns>
        Task<AssetSnapshot> RefreshSnapshotAsync();

        #endregion

        #region Events

        /// <summary>새 Snapshot이 수신되었을 때 발생한다.</summary>
        event EventHandler<SnapshotReceivedEventArgs>? SnapshotReceived;

        /// <summary>에셋 이벤트가 수신되었을 때 발생한다.</summary>
        event EventHandler<AssetEventReceivedEventArgs>? EventReceived;

        /// <summary>연결 상태가 변경되었을 때 발생한다.</summary>
        event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>Ring Buffer 오버플로가 감지되었을 때 발생한다.</summary>
        event EventHandler<EventOverflowEventArgs>? EventOverflow;

        #endregion
    }
}
