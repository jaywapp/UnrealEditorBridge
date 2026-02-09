using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Adapter.Events
{

    /// <summary>
    /// 새 Snapshot이 수신되었을 때의 이벤트 인자.
    /// </summary>
    public sealed class SnapshotReceivedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>수신된 에셋 스냅샷.</summary>
        public AssetSnapshot Snapshot { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="SnapshotReceivedEventArgs"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="snapshot">수신된 스냅샷.</param>
        public SnapshotReceivedEventArgs(AssetSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        #endregion
    }
}
