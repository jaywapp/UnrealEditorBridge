using System.Reactive.Linq;
using ReactiveUI;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// 메인 윈도우의 최상위 ViewModel.
    /// 하위 ViewModel을 보유하고 이들 간 반응형 연결을 조율한다.
    /// </summary>
    public class MainViewModel : ReactiveObject
    {
        #region Internal Fields

        private string _statusMessage = "연결 안됨";
        private bool _isLoading;

        #endregion

        #region Properties

        /// <summary>연결 패널 ViewModel.</summary>
        public ConnectionViewModel Connection { get; }

        /// <summary>에셋 목록 ViewModel.</summary>
        public AssetListViewModel AssetList { get; }

        /// <summary>에셋 상세 ViewModel.</summary>
        public AssetDetailViewModel AssetDetail { get; }

        /// <summary>이벤트 로그 ViewModel.</summary>
        public EventLogViewModel EventLog { get; }

        /// <summary>상태 표시줄에 표시할 메시지.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        /// <summary>로딩 상태 여부.</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="MainViewModel"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="connection">연결 ViewModel.</param>
        /// <param name="assetList">에셋 목록 ViewModel.</param>
        /// <param name="assetDetail">에셋 상세 ViewModel.</param>
        /// <param name="eventLog">이벤트 로그 ViewModel.</param>
        public MainViewModel(
            ConnectionViewModel connection,
            AssetListViewModel assetList,
            AssetDetailViewModel assetDetail,
            EventLogViewModel eventLog)
        {
            Connection = connection;
            AssetList = assetList;
            AssetDetail = assetDetail;
            EventLog = eventLog;

            // 에셋 선택 변경 → 상세 정보 갱신
            AssetList
                .WhenAnyValue(x => x.SelectedAsset)
                .Subscribe(selected =>
                {
                    AssetDetail.ShowAsset(selected?.AssetInfo);
                });

            // 연결 상태 → 상태 표시줄
            Connection
                .WhenAnyValue(x => x.StateText)
                .Subscribe(text =>
                {
                    StatusMessage = text;
                });
        }

        #endregion
    }
}
