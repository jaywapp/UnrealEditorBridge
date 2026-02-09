using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Internal.Connection;
using UnrealEditorBridge.Adapter.Internal.Ipc;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Internal
{

    /// <summary>
    /// <see cref="IBridgeClient"/> 인터페이스의 핵심 구현체.
    /// MMF를 열고, Header를 검증하고, 백그라운드 스레드로 Heartbeat/Event/Snapshot을 모니터링한다.
    /// </summary>
    internal sealed class BridgeClient : IBridgeClient
    {
        #region Internal Fields

        private readonly BridgeClientOptions _options;
        private readonly ConnectionStateMachine _stateMachine = new();

        private MmfAccessor? _mmf;
        private HeaderReader? _headerReader;
        private SnapshotReader? _snapshotReader;
        private EventRingReader? _eventRingReader;
        private HeartbeatMonitor? _heartbeatMonitor;

        private Mutex? _mutex;
        private EventWaitHandle? _snapshotEvent;
        private EventWaitHandle? _streamEvent;

        private CancellationTokenSource? _cts;
        private Task? _eventReaderTask;
        private Task? _snapshotWatcherTask;

        private AssetSnapshot? _currentSnapshot;
        private bool _disposed;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public ConnectionState State => _stateMachine.Current;

        /// <inheritdoc/>
        public AssetSnapshot? CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="BridgeClient"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="options">클라이언트 옵션.</param>
        public BridgeClient(BridgeClientOptions options)
        {
            _options = options;
            _stateMachine.StateChanged += OnStateMachineStateChanged;
        }

        #endregion

        #region Events

        /// <inheritdoc/>
        public event EventHandler<SnapshotReceivedEventArgs>? SnapshotReceived;

        /// <inheritdoc/>
        public event EventHandler<AssetEventReceivedEventArgs>? EventReceived;

        /// <inheritdoc/>
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <inheritdoc/>
        public event EventHandler<EventOverflowEventArgs>? EventOverflow;

        #endregion

        #region Functions

        /// <inheritdoc/>
        public Task ConnectAsync(string mmfName, CancellationToken ct = default)
        {
            if (State != ConnectionState.Disconnected)
                throw new InvalidOperationException($"Cannot connect in state {State}. Disconnect first.");

            _stateMachine.TransitionTo(ConnectionState.Connecting);

            try
            {
                // 1. MMF 열기
                _mmf = new MmfAccessor();
                if (!_mmf.TryOpen(mmfName))
                {
                    _stateMachine.TransitionTo(ConnectionState.Error);
                    throw new FileNotFoundException($"MMF '{mmfName}' not found. Is the UE Editor running?");
                }

                // 2. Header 읽기 + 검증
                _headerReader = new HeaderReader(_mmf);
                var header = _headerReader.ReadHeader();

                if (!header.IsValidMagic())
                {
                    _stateMachine.TransitionTo(ConnectionState.Error);
                    throw new InvalidDataException(
                        $"Invalid MMF magic: 0x{header.Magic:X8}. Expected 0x{ProtocolConstants.Magic:X8}.");
                }

                if (!header.IsMajorCompatible())
                {
                    _stateMachine.TransitionTo(ConnectionState.VersionMismatch);
                    throw new InvalidOperationException(
                        $"Protocol version mismatch. Writer: {header.ProtocolVersion}, Reader: {ProtocolConstants.ProtocolVersion}.");
                }

                // 3. IPC 동기화 객체 열기
                var mutexName = ProtocolConstants.BuildMutexName(mmfName);
                var snapshotEvtName = ProtocolConstants.BuildSnapshotEventName(mmfName);
                var streamEvtName = ProtocolConstants.BuildStreamEventName(mmfName);

                _mutex = Mutex.OpenExisting(mutexName);
                _snapshotEvent = EventWaitHandle.OpenExisting(snapshotEvtName);
                _streamEvent = EventWaitHandle.OpenExisting(streamEvtName);

                // 4. 초기 Snapshot 읽기
                _snapshotReader = new SnapshotReader(_mmf);
                var snapshot = _snapshotReader.ReadSnapshotWithRetry(header, _mutex);
                if (snapshot != null)
                {
                    Interlocked.Exchange(ref _currentSnapshot, snapshot);
                }

                // 5. Event Ring Reader 초기화 (기존 이벤트 스킵)
                _eventRingReader = new EventRingReader(_mmf);
                _eventRingReader.Initialize(header);

                // 6. 백그라운드 모니터링 시작
                _cts = new CancellationTokenSource();

                _heartbeatMonitor = new HeartbeatMonitor(_headerReader, _stateMachine, _options);
                _heartbeatMonitor.RecoveredFromLost += OnRecoveredFromLost;
                _heartbeatMonitor.Start();

                _eventReaderTask = Task.Run(() => EventReaderLoop(_cts.Token));
                if (_options.AutoRefreshSnapshot)
                    _snapshotWatcherTask = Task.Run(() => SnapshotWatcherLoop(_cts.Token));

                _stateMachine.TransitionTo(ConnectionState.Connected);

                // 초기 Snapshot 이벤트 발행
                var initialSnapshot = _currentSnapshot;
                if (initialSnapshot != null)
                {
                    SnapshotReceived?.Invoke(this, new SnapshotReceivedEventArgs(initialSnapshot));
                }
            }
            catch (Exception) when (State == ConnectionState.Connecting)
            {
                _stateMachine.TransitionTo(ConnectionState.Error);
                CleanupResources();
                throw;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync()
        {
            _cts?.Cancel();

            if (_heartbeatMonitor != null)
                await _heartbeatMonitor.StopAsync().ConfigureAwait(false);

            var tasks = new List<Task>();
            if (_eventReaderTask != null) tasks.Add(_eventReaderTask);
            if (_snapshotWatcherTask != null) tasks.Add(_snapshotWatcherTask);

            if (tasks.Count > 0)
            {
                try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
                catch { /* 타임아웃 허용 */ }
            }

            CleanupResources();
            _stateMachine.TransitionTo(ConnectionState.Disconnected);
        }

        /// <inheritdoc/>
        public Task<AssetSnapshot> RefreshSnapshotAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                if (_headerReader == null || _snapshotReader == null || _mutex == null)
                    throw new InvalidOperationException("Not connected.");

                var header = _headerReader.ReadHeader();
                var snapshot = _snapshotReader.ReadSnapshotWithRetry(header, _mutex);

                if (snapshot != null)
                {
                    Interlocked.Exchange(ref _currentSnapshot, snapshot);
                    SnapshotReceived?.Invoke(this, new SnapshotReceivedEventArgs(snapshot));
                }

                return snapshot ?? CurrentSnapshot ?? new AssetSnapshot();
            }, ct);
        }

        #endregion

        #region Event Handlers

        private void OnStateMachineStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, e);
        }

        private void OnRecoveredFromLost()
        {
            // Lost 복구 시 Snapshot 재요청
            if (_options.AutoRefreshSnapshot)
            {
                _ = Task.Run(async () =>
                {
                    try { await RefreshSnapshotAsync().ConfigureAwait(false); }
                    catch { /* 무시 */ }
                });
            }
        }

        #endregion

        #region Background Loops

        private async Task EventReaderLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Named Event 대기
                    var signaled = _streamEvent?.WaitOne(_options.EventPollInterval) ?? false;

                    if (ct.IsCancellationRequested) break;
                    if (_stateMachine.Current != ConnectionState.Connected) continue;
                    if (_headerReader == null || _eventRingReader == null || _mutex == null) break;

                    // Mutex 획득 하에 Header 읽기 + 이벤트 읽기
                    List<AssetEvent> events;
                    bool overflowDetected;
                    ulong missedCount;

                    if (!_mutex.WaitOne(50)) continue;
                    try
                    {
                        var header = _headerReader.ReadHeader();
                        events = _eventRingReader.ReadNewEvents(header, out overflowDetected, out missedCount);
                    }
                    finally
                    {
                        _mutex.ReleaseMutex();
                    }

                    // 오버플로 처리
                    if (overflowDetected)
                    {
                        EventOverflow?.Invoke(this, new EventOverflowEventArgs(missedCount));
                        if (_options.AutoRecoverOnOverflow)
                        {
                            try { await RefreshSnapshotAsync(ct).ConfigureAwait(false); }
                            catch { /* 무시 */ }
                        }
                        continue;
                    }

                    // 이벤트 발행
                    foreach (var evt in events)
                    {
                        EventReceived?.Invoke(this, new AssetEventReceivedEventArgs(evt));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (AbandonedMutexException)
                {
                    try { _mutex?.ReleaseMutex(); } catch { }
                }
                catch { /* 예외 삼킴, 루프 계속 */ }
            }
        }

        private async Task SnapshotWatcherLoop(CancellationToken ct)
        {
            uint lastVersion = CurrentSnapshot?.Version ?? 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var signaled = _snapshotEvent?.WaitOne(_options.EventPollInterval) ?? false;
                    if (ct.IsCancellationRequested) break;
                    if (_stateMachine.Current != ConnectionState.Connected) continue;
                    if (_headerReader == null) continue;

                    var currentVersion = _headerReader.ReadSnapshotVersion();
                    if (currentVersion != lastVersion)
                    {
                        lastVersion = currentVersion;
                        try { await RefreshSnapshotAsync(ct).ConfigureAwait(false); }
                        catch { /* 무시 */ }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* 루프 계속 */ }
            }
        }

        #endregion

        #region Resource Cleanup

        private void CleanupResources()
        {
            _cts?.Dispose();
            _cts = null;

            _heartbeatMonitor = null;
            _eventReaderTask = null;
            _snapshotWatcherTask = null;

            _snapshotEvent?.Dispose();
            _snapshotEvent = null;
            _streamEvent?.Dispose();
            _streamEvent = null;
            _mutex?.Dispose();
            _mutex = null;

            _mmf?.Dispose();
            _mmf = null;

            _headerReader = null;
            _snapshotReader = null;
            _eventRingReader = null;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cts?.Cancel();
                CleanupResources();
                _disposed = true;
            }
        }

        #endregion
    }
}
