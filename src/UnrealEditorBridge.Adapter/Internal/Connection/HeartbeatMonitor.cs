using UnrealEditorBridge.Adapter.Internal.Ipc;

namespace UnrealEditorBridge.Adapter.Internal.Connection
{

    /// <summary>
    /// Heartbeat을 주기적으로 체크하여 연결 상태를 판단한다.
    /// 백그라운드 Task로 실행되며, CancellationToken으로 중지한다.
    /// </summary>
    internal sealed class HeartbeatMonitor
    {
        #region Internal Fields

        private readonly HeaderReader _headerReader;
        private readonly ConnectionStateMachine _stateMachine;
        private readonly BridgeClientOptions _options;
        private Task? _monitorTask;
        private CancellationTokenSource? _cts;
        private DateTime _lostSince = DateTime.MaxValue;

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="HeartbeatMonitor"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        public HeartbeatMonitor(
            HeaderReader headerReader,
            ConnectionStateMachine stateMachine,
            BridgeClientOptions options)
        {
            _headerReader = headerReader;
            _stateMachine = stateMachine;
            _options = options;
        }

        #endregion

        #region Events

        /// <summary>Lost 상태에서 복구되었을 때 발생한다. Snapshot 재요청이 필요함을 알린다.</summary>
        public event Action? RecoveredFromLost;

        #endregion

        #region Functions

        /// <summary>
        /// Heartbeat 모니터링을 시작한다.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        }

        /// <summary>
        /// Heartbeat 모니터링을 중지한다.
        /// </summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_monitorTask != null)
            {
                try { await _monitorTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _cts?.Dispose();
            _cts = null;
        }

        private async Task MonitorLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.HeartbeatCheckInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    var heartbeatTicks = _headerReader.ReadHeartbeat();
                    var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - heartbeatTicks);

                    if (elapsed > _options.HeartbeatTimeout)
                    {
                        HandleHeartbeatTimeout(elapsed);
                    }
                    else
                    {
                        HandleHeartbeatAlive();
                    }
                }
                catch
                {
                    // MMF 접근 실패 시 Disconnected 처리
                    _stateMachine.TransitionTo(ConnectionState.Disconnected);
                    break;
                }
            }
        }

        private void HandleHeartbeatTimeout(TimeSpan elapsed)
        {
            var current = _stateMachine.Current;

            if (current == ConnectionState.Connected)
            {
                _stateMachine.TransitionTo(ConnectionState.Lost);
                _lostSince = DateTime.UtcNow;
            }
            else if (current == ConnectionState.Lost)
            {
                if (DateTime.UtcNow - _lostSince > _options.MaxLostDuration)
                {
                    _stateMachine.TransitionTo(ConnectionState.Disconnected);
                }
            }
        }

        private void HandleHeartbeatAlive()
        {
            var current = _stateMachine.Current;
            if (current == ConnectionState.Lost)
            {
                _stateMachine.TransitionTo(ConnectionState.Connected);
                _lostSince = DateTime.MaxValue;
                RecoveredFromLost?.Invoke();
            }
        }

        #endregion
    }
}
