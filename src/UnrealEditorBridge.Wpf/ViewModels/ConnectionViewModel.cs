using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Prism.Commands;
using ReactiveUI;
using UnrealEditorBridge.Adapter;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Wpf.Services;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// Editor 인스턴스 탐색 및 연결 관리 ViewModel.
    /// </summary>
    public class ConnectionViewModel : ReactiveObject
    {
        #region Internal Fields

        private readonly IBridgeService _bridgeService;
        private readonly EditorInstanceDiscovery _discovery;

        private EditorInstanceInfo? _selectedInstance;
        private ConnectionState _state = ConnectionState.Disconnected;
        private string _stateText = "연결 안됨";
        private bool _isConnecting;

        #endregion

        #region Properties

        /// <summary>활성 Editor 인스턴스 목록.</summary>
        public ObservableCollection<EditorInstanceInfo> Instances { get; } = new();

        /// <summary>사용자가 선택한 Editor 인스턴스.</summary>
        public EditorInstanceInfo? SelectedInstance
        {
            get => _selectedInstance;
            set => this.RaiseAndSetIfChanged(ref _selectedInstance, value);
        }

        /// <summary>현재 연결 상태.</summary>
        public ConnectionState State
        {
            get => _state;
            set => this.RaiseAndSetIfChanged(ref _state, value);
        }

        /// <summary>사용자에게 표시할 상태 텍스트.</summary>
        public string StateText
        {
            get => _stateText;
            set => this.RaiseAndSetIfChanged(ref _stateText, value);
        }

        /// <summary>연결 중 여부.</summary>
        public bool IsConnecting
        {
            get => _isConnecting;
            set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
        }

        #endregion

        #region Commands

        /// <summary>선택된 인스턴스에 연결하는 커맨드.</summary>
        public DelegateCommand ConnectCommand { get; }

        /// <summary>현재 연결을 해제하는 커맨드.</summary>
        public DelegateCommand DisconnectCommand { get; }

        /// <summary>인스턴스 목록을 새로고침하는 커맨드.</summary>
        public DelegateCommand RefreshInstancesCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="ConnectionViewModel"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="bridgeService">Bridge 서비스.</param>
        /// <param name="discovery">Editor 인스턴스 탐색기.</param>
        public ConnectionViewModel(
            IBridgeService bridgeService,
            EditorInstanceDiscovery discovery)
        {
            _bridgeService = bridgeService;
            _discovery = discovery;

            ConnectCommand = new DelegateCommand(
                async () => await ExecuteConnectAsync(),
                () => CanConnect());

            DisconnectCommand = new DelegateCommand(
                async () => await ExecuteDisconnectAsync(),
                () => CanDisconnect());

            RefreshInstancesCommand = new DelegateCommand(ExecuteRefreshInstances);

            // 상태 변경 시 Command CanExecute 재평가
            this.WhenAnyValue(x => x.State, x => x.SelectedInstance, x => x.IsConnecting)
                .Subscribe(_ =>
                {
                    ConnectCommand.RaiseCanExecuteChanged();
                    DisconnectCommand.RaiseCanExecuteChanged();
                    UpdateStateText();
                });

            // 연결 상태 변경 이벤트 구독
            _bridgeService.ConnectionStateChanged += OnConnectionStateChanged;
        }

        #endregion

        #region Functions

        private bool CanConnect()
            => SelectedInstance != null
               && State == ConnectionState.Disconnected
               && !IsConnecting;

        private bool CanDisconnect()
            => State == ConnectionState.Connected
               || State == ConnectionState.Lost;

        private async Task ExecuteConnectAsync()
        {
            if (SelectedInstance == null) return;

            IsConnecting = true;
            try
            {
                await _bridgeService.ConnectAsync(SelectedInstance.MmfName);
            }
            catch (Exception ex)
            {
                StateText = $"연결 오류: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task ExecuteDisconnectAsync()
        {
            await _bridgeService.DisconnectAsync();
        }

        private void ExecuteRefreshInstances()
        {
            var active = _discovery.GetActiveInstances();
            Instances.Clear();
            foreach (var instance in active)
            {
                Instances.Add(instance);
            }
        }

        private void UpdateStateText()
        {
            StateText = State switch
            {
                ConnectionState.Disconnected => "연결 안됨",
                ConnectionState.Connecting => "연결 중...",
                ConnectionState.Connected
                    => $"연결됨 - {SelectedInstance?.ProjectName}",
                ConnectionState.Lost => "응답 없음 - 재연결 대기 중",
                ConnectionState.VersionMismatch => "프로토콜 버전 불일치",
                ConnectionState.Error => "연결 오류",
                _ => string.Empty
            };
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            State = e.NewState;
        }

        #endregion
    }
}
