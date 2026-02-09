using System.Windows;
using System.Windows.Threading;
using UnrealEditorBridge.Adapter;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Wpf.Services
{

    /// <summary>
    /// <see cref="IBridgeService"/>의 구현체.
    /// Adapter의 백그라운드 스레드 이벤트를 WPF UI 스레드(Dispatcher)로 마샬링한다.
    /// </summary>
    public sealed class BridgeService : IBridgeService
    {
        #region Internal Fields

        private readonly IBridgeClient _client;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public ConnectionState State => _client.State;

        /// <inheritdoc/>
        public AssetSnapshot? CurrentSnapshot => _client.CurrentSnapshot;

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="BridgeService"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="client">Adapter 클라이언트.</param>
        public BridgeService(IBridgeClient client)
        {
            _client = client;
            _dispatcher = Application.Current.Dispatcher;

            _client.SnapshotReceived += (s, e) =>
                _dispatcher.BeginInvoke(() => SnapshotReceived?.Invoke(this, e));

            _client.EventReceived += (s, e) =>
                _dispatcher.BeginInvoke(() => EventReceived?.Invoke(this, e));

            _client.ConnectionStateChanged += (s, e) =>
                _dispatcher.BeginInvoke(() => ConnectionStateChanged?.Invoke(this, e));

            _client.EventOverflow += (s, e) =>
                _dispatcher.BeginInvoke(() => EventOverflow?.Invoke(this, e));
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
        public Task ConnectAsync(string mmfName)
            => _client.ConnectAsync(mmfName);

        /// <inheritdoc/>
        public Task DisconnectAsync()
            => _client.DisconnectAsync();

        /// <inheritdoc/>
        public Task<AssetSnapshot> RefreshSnapshotAsync()
            => _client.RefreshSnapshotAsync();

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _client.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
