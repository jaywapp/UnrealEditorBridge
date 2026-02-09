namespace UnrealEditorBridge.Adapter.Events
{

    /// <summary>
    /// Ring Buffer 오버플로가 감지되었을 때의 이벤트 인자.
    /// </summary>
    public sealed class EventOverflowEventArgs : EventArgs
    {
        #region Properties

        /// <summary>누락된 이벤트 수 (추정).</summary>
        public ulong MissedCount { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="EventOverflowEventArgs"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public EventOverflowEventArgs(ulong missedCount)
        {
            MissedCount = missedCount;
        }

        #endregion
    }
}
