using UnrealEditorBridge.Adapter.Events;

namespace UnrealEditorBridge.Adapter.Internal.Connection
{

    /// <summary>
    /// 연결 상태 전이를 관리하는 상태 기계.
    /// 유효한 전이만 허용하고 상태 변경 이벤트를 발행한다.
    /// </summary>
    internal sealed class ConnectionStateMachine
    {
        #region Internal Fields

        private readonly object _lock = new();
        private ConnectionState _state = ConnectionState.Disconnected;

        #endregion

        #region Properties

        /// <summary>현재 연결 상태. 스레드 안전.</summary>
        public ConnectionState Current
        {
            get { lock (_lock) return _state; }
        }

        #endregion

        #region Events

        /// <summary>상태가 변경되었을 때 발생한다.</summary>
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 상태로 전이를 시도한다.
        /// 현재 상태와 동일하면 아무 것도 하지 않는다.
        /// </summary>
        /// <param name="newState">목표 상태.</param>
        /// <returns>전이가 실제로 발생했으면 true.</returns>
        public bool TransitionTo(ConnectionState newState)
        {
            ConnectionState oldState;
            lock (_lock)
            {
                if (_state == newState)
                    return false;

                oldState = _state;
                _state = newState;
            }

            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
            return true;
        }

        /// <summary>
        /// 상태를 Disconnected로 강제 리셋한다.
        /// </summary>
        public void Reset()
        {
            TransitionTo(ConnectionState.Disconnected);
        }

        #endregion
    }
}
