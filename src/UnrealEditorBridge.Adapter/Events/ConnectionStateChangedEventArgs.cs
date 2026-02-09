namespace UnrealEditorBridge.Adapter.Events
{

    /// <summary>
    /// 연결 상태가 변경되었을 때의 이벤트 인자.
    /// </summary>
    public sealed class ConnectionStateChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>이전 연결 상태.</summary>
        public ConnectionState OldState { get; }

        /// <summary>새 연결 상태.</summary>
        public ConnectionState NewState { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="ConnectionStateChangedEventArgs"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }

        #endregion
    }
}
