using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Adapter.Events
{

    /// <summary>
    /// 에셋 이벤트가 수신되었을 때의 이벤트 인자.
    /// </summary>
    public sealed class AssetEventReceivedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>수신된 에셋 이벤트.</summary>
        public AssetEvent Event { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="AssetEventReceivedEventArgs"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public AssetEventReceivedEventArgs(AssetEvent evt)
        {
            Event = evt;
        }

        #endregion
    }
}
