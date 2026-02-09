# UnrealEditorBridge.Wpf 설계

## 1. 개요

UnrealEditorBridge.Wpf는 UnrealEditorBridge.Adapter를 소비하여 에셋 목록, 이벤트 스트림, 연결 상태를 시각화하는 WPF 애플리케이션이다. MVVM 패턴을 엄격히 적용하며 IPC 로직을 직접 포함하지 않는다.

**프로젝트 구성:**
- 타겟 프레임워크: `net8.0-windows`
- WPF 활성화: `<UseWPF>true</UseWPF>`
- 프로젝트 참조: `UnrealEditorBridge.Adapter`

**NuGet 패키지:**

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `Prism.Core` | 6.3.0 | MVVM 기반 (DelegateCommand, EventAggregator) |
| `Prism.Unity` | 6.3.0 | Unity DI 통합 |
| `Prism.Wpf` | 6.3.0 | PrismApplication, Region, Navigation |
| `Unity` | 4.0.1 | DI 컨테이너 |
| `ReactiveUI` | 19.4.1 | ReactiveObject, WhenAnyValue |
| `ReactiveUI.WPF` | 19.4.1 | WPF Scheduler 통합 |

**핵심 규칙:**
- ViewModel은 반드시 `ReactiveObject`를 상속한다
- 프로퍼티 변경 알림은 반드시 `RaiseAndSetIfChanged`를 사용한다
- 반응형 프로퍼티 관찰은 반드시 `WhenAnyValue`를 사용한다
- Command는 반드시 `DelegateCommand`(Prism)만 사용한다 (`ReactiveCommand` 사용 금지)
- 파일 하나에 클래스 하나만 정의한다 (중첩 클래스 금지)
- MVVM 폴더 구조를 엄격히 준수한다

---

## 2. MVVM 구조

### 2.1 프로젝트 디렉토리 구조

```
UnrealEditorBridge.Wpf/
├── App.xaml / App.xaml.cs              // PrismApplication 진입점
├── Bootstrapper.cs                     // Unity 컨테이너 등록
├── Services/
│   ├── IBridgeService.cs              // ViewModel용 Adapter 래퍼 인터페이스
│   └── BridgeService.cs               // IBridgeService 구현 (Dispatcher 마샬링 포함)
├── Models/
│   └── EventLogItem.cs                // 이벤트 로그 항목 모델
├── ViewModels/
│   ├── MainViewModel.cs               // 전체 화면 상태 조율
│   ├── ConnectionViewModel.cs         // 연결 상태 및 인스턴스 선택
│   ├── AssetListViewModel.cs          // 에셋 목록 및 필터링
│   ├── AssetItemViewModel.cs          // 에셋 항목 개별 ViewModel
│   ├── AssetDetailViewModel.cs        // 에셋 상세 정보
│   └── EventLogViewModel.cs           // 이벤트 스트림 로그
├── Views/
│   ├── MainWindow.xaml                // 메인 레이아웃 (Shell)
│   ├── ConnectionPanelView.xaml       // 연결 상태 패널
│   ├── AssetListView.xaml             // 에셋 목록 뷰
│   ├── AssetDetailView.xaml           // 에셋 상세 뷰
│   └── EventLogView.xaml              // 이벤트 로그 뷰
├── Converters/
│   ├── ConnectionStateToColorConverter.cs
│   ├── ConnectionStateToBoolConverter.cs
│   ├── EventTypeToIconConverter.cs
│   └── BoolToVisibilityConverter.cs
└── Resources/
    ├── Styles.xaml                    // 공용 스타일
    └── Colors.xaml                    // 테마 색상
```

### 2.2 PrismApplication + Unity DI 구성

```csharp
// App.xaml
// <prism:PrismApplication x:Class="UnrealEditorBridge.Wpf.App"
//     xmlns:prism="http://prismlibrary.com/" ... >
// </prism:PrismApplication>
```

```csharp
// App.xaml.cs
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Adapter 등록
        containerRegistry.RegisterSingleton<IBridgeClient>(
            () => BridgeClientFactory.Create());
        containerRegistry.RegisterSingleton<EditorInstanceDiscovery>();

        // Services 등록
        containerRegistry.RegisterSingleton<IBridgeService, BridgeService>();

        // ViewModels 등록
        containerRegistry.RegisterSingleton<MainViewModel>();
        containerRegistry.Register<ConnectionViewModel>();
        containerRegistry.Register<AssetListViewModel>();
        containerRegistry.Register<AssetItemViewModel>();
        containerRegistry.Register<AssetDetailViewModel>();
        containerRegistry.Register<EventLogViewModel>();
    }
}
```

---

## 3. 주요 ViewModel 책임

### 3.1 MainViewModel

전체 화면의 상태를 조율하는 최상위 ViewModel.

```csharp
// MainViewModel.cs
public class MainViewModel : ReactiveObject
{
    public ConnectionViewModel Connection { get; }
    public AssetListViewModel AssetList { get; }
    public AssetDetailViewModel AssetDetail { get; }
    public EventLogViewModel EventLog { get; }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public MainViewModel(
        ConnectionViewModel connection,
        AssetListViewModel assetList,
        AssetDetailViewModel assetDetail,
        EventLogViewModel eventLog,
        IEventAggregator eventAggregator)
    {
        Connection = connection;
        AssetList = assetList;
        AssetDetail = assetDetail;
        EventLog = eventLog;

        // 에셋 선택 변경 → 상세 정보 갱신 (WhenAnyValue)
        AssetList
            .WhenAnyValue(x => x.SelectedAsset)
            .Subscribe(selected =>
            {
                AssetDetail.ShowAsset(selected?.AssetInfo);
            });

        // 연결 상태 → 상태 표시줄 (WhenAnyValue)
        Connection
            .WhenAnyValue(x => x.StateText)
            .Subscribe(text =>
            {
                StatusMessage = text;
            });
    }
}
```

**책임:**
- 하위 ViewModel 보유 및 생명주기 관리 (Unity DI가 생성)
- 하위 ViewModel 간 반응형 연결 (`WhenAnyValue` 기반)
- 전역 상태 표시줄 관리

### 3.2 ConnectionViewModel

Editor 인스턴스 탐색 및 연결 관리.

```csharp
// ConnectionViewModel.cs
public class ConnectionViewModel : ReactiveObject
{
    private readonly IBridgeService _bridgeService;
    private readonly EditorInstanceDiscovery _discovery;

    public ObservableCollection<EditorInstanceInfo> Instances { get; }
        = new ObservableCollection<EditorInstanceInfo>();

    private EditorInstanceInfo _selectedInstance;
    public EditorInstanceInfo SelectedInstance
    {
        get => _selectedInstance;
        set => this.RaiseAndSetIfChanged(ref _selectedInstance, value);
    }

    private ConnectionState _state;
    public ConnectionState State
    {
        get => _state;
        set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    private string _stateText;
    public string StateText
    {
        get => _stateText;
        set => this.RaiseAndSetIfChanged(ref _stateText, value);
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
    }

    // Prism DelegateCommand
    public DelegateCommand ConnectCommand { get; }
    public DelegateCommand DisconnectCommand { get; }
    public DelegateCommand RefreshInstancesCommand { get; }

    public ConnectionViewModel(
        IBridgeService bridgeService,
        EditorInstanceDiscovery discovery)
    {
        _bridgeService = bridgeService;
        _discovery = discovery;

        // DelegateCommand 생성
        ConnectCommand = new DelegateCommand(
            async () => await ExecuteConnectAsync(),
            () => CanConnect());

        DisconnectCommand = new DelegateCommand(
            async () => await ExecuteDisconnectAsync(),
            () => CanDisconnect());

        RefreshInstancesCommand = new DelegateCommand(ExecuteRefreshInstances);

        // 상태 변경 시 Command CanExecute 재평가 (WhenAnyValue)
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

    private bool CanConnect()
        => SelectedInstance != null
           && State == ConnectionState.Disconnected
           && !IsConnecting;

    private bool CanDisconnect()
        => State == ConnectionState.Connected
           || State == ConnectionState.Lost;

    private async Task ExecuteConnectAsync()
    {
        IsConnecting = true;
        try
        {
            await _bridgeService.ConnectAsync(SelectedInstance.MmfName);
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

    private void OnConnectionStateChanged(
        object sender, ConnectionStateChangedEventArgs e)
    {
        State = e.NewState;
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
}
```

**책임:**
- `EditorInstanceDiscovery`를 통한 활성 인스턴스 탐색
- 사용자가 선택한 인스턴스로 `IBridgeClient` 연결/해제
- 연결 상태 변경을 UI에 반영
- `WhenAnyValue`로 상태 변경 시 Command CanExecute 자동 재평가

### 3.3 AssetListViewModel

에셋 목록 표시 및 필터링.

```csharp
// AssetListViewModel.cs
public class AssetListViewModel : ReactiveObject
{
    private readonly IBridgeService _bridgeService;
    private List<AssetInfo> _allAssets = new();

    public ObservableCollection<AssetItemViewModel> FilteredAssets { get; }
        = new ObservableCollection<AssetItemViewModel>();

    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    private string _classFilter;
    public string ClassFilter
    {
        get => _classFilter;
        set => this.RaiseAndSetIfChanged(ref _classFilter, value);
    }

    private string _pathFilter;
    public string PathFilter
    {
        get => _pathFilter;
        set => this.RaiseAndSetIfChanged(ref _pathFilter, value);
    }

    private AssetItemViewModel _selectedAsset;
    public AssetItemViewModel SelectedAsset
    {
        get => _selectedAsset;
        set => this.RaiseAndSetIfChanged(ref _selectedAsset, value);
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    private int _filteredCount;
    public int FilteredCount
    {
        get => _filteredCount;
        set => this.RaiseAndSetIfChanged(ref _filteredCount, value);
    }

    public ObservableCollection<string> AvailableClasses { get; }
        = new ObservableCollection<string>();

    // Prism DelegateCommand
    public DelegateCommand ClearFiltersCommand { get; }
    public DelegateCommand RefreshCommand { get; }

    public AssetListViewModel(IBridgeService bridgeService)
    {
        _bridgeService = bridgeService;

        ClearFiltersCommand = new DelegateCommand(ExecuteClearFilters);
        RefreshCommand = new DelegateCommand(
            async () => await ExecuteRefreshAsync());

        // 필터 변경 시 300ms 디바운싱 후 목록 재구성 (WhenAnyValue + Throttle)
        this.WhenAnyValue(
                x => x.SearchText,
                x => x.ClassFilter,
                x => x.PathFilter)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => RebuildFilteredList());

        // Snapshot 수신 구독
        _bridgeService.SnapshotReceived += OnSnapshotReceived;
        _bridgeService.EventReceived += OnEventReceived;
    }

    private void OnSnapshotReceived(
        object sender, SnapshotReceivedEventArgs e)
    {
        _allAssets = e.Snapshot.Assets.ToList();
        UpdateAvailableClasses();
        RebuildFilteredList();
    }

    private void RebuildFilteredList()
    {
        var filtered = ApplyFilters(_allAssets);

        FilteredAssets.Clear();
        foreach (var asset in filtered)
        {
            FilteredAssets.Add(new AssetItemViewModel(asset));
        }

        TotalCount = _allAssets.Count;
        FilteredCount = filtered.Count;
    }

    private List<AssetInfo> ApplyFilters(List<AssetInfo> source)
    {
        IEnumerable<AssetInfo> query = source;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.ToLowerInvariant();
            query = query.Where(a =>
                a.AssetName.ToLowerInvariant().Contains(term) ||
                a.ObjectPath.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(ClassFilter))
        {
            query = query.Where(a => a.ClassName == ClassFilter);
        }

        if (!string.IsNullOrWhiteSpace(PathFilter))
        {
            query = query.Where(a =>
                a.PackagePath.StartsWith(PathFilter,
                    StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    private void UpdateAvailableClasses()
    {
        AvailableClasses.Clear();
        var classes = _allAssets
            .Select(a => a.ClassName)
            .Distinct()
            .OrderBy(c => c);
        foreach (var cls in classes)
        {
            AvailableClasses.Add(cls);
        }
    }

    private void OnEventReceived(
        object sender, AssetEventReceivedEventArgs e)
    {
        switch (e.Event.EventType)
        {
            case AssetEventType.AssetCreated:
                AddAssetIfMatchesFilter(e.Event);
                break;
            case AssetEventType.AssetDeleted:
                RemoveAssetByPath(e.Event.ObjectPath);
                break;
            case AssetEventType.AssetRenamed:
                UpdateAssetByPath(e.Event.OldObjectPath, e.Event);
                break;
        }
    }

    private void ExecuteClearFilters()
    {
        SearchText = string.Empty;
        ClassFilter = null;
        PathFilter = null;
    }

    private async Task ExecuteRefreshAsync()
    {
        var snapshot = await _bridgeService.RefreshSnapshotAsync();
        _allAssets = snapshot.Assets.ToList();
        RebuildFilteredList();
    }

    // --- 증분 갱신 헬퍼 (생략: AddAssetIfMatchesFilter, RemoveAssetByPath, UpdateAssetByPath) ---
}
```

**책임:**
- Snapshot 수신 시 에셋 목록 갱신
- 실시간 이벤트에 따른 증분 갱신 (추가/삭제/변경)
- `WhenAnyValue` + `Throttle`로 필터 디바운싱 (300ms)
- `DelegateCommand`로 초기화/새로고침

### 3.4 AssetItemViewModel

에셋 목록의 개별 항목 ViewModel. (파일 하나에 클래스 하나 원칙 준수)

```csharp
// AssetItemViewModel.cs
public class AssetItemViewModel : ReactiveObject
{
    public AssetInfo AssetInfo { get; }

    private string _assetName;
    public string AssetName
    {
        get => _assetName;
        set => this.RaiseAndSetIfChanged(ref _assetName, value);
    }

    private string _className;
    public string ClassName
    {
        get => _className;
        set => this.RaiseAndSetIfChanged(ref _className, value);
    }

    private string _objectPath;
    public string ObjectPath
    {
        get => _objectPath;
        set => this.RaiseAndSetIfChanged(ref _objectPath, value);
    }

    public AssetItemViewModel(AssetInfo assetInfo)
    {
        AssetInfo = assetInfo;
        _assetName = assetInfo.AssetName;
        _className = assetInfo.ClassName;
        _objectPath = assetInfo.ObjectPath;
    }
}
```

### 3.5 AssetDetailViewModel

선택된 에셋의 상세 정보 표시.

```csharp
// AssetDetailViewModel.cs
public class AssetDetailViewModel : ReactiveObject
{
    private string _objectPath;
    public string ObjectPath
    {
        get => _objectPath;
        set => this.RaiseAndSetIfChanged(ref _objectPath, value);
    }

    private string _packagePath;
    public string PackagePath
    {
        get => _packagePath;
        set => this.RaiseAndSetIfChanged(ref _packagePath, value);
    }

    private string _assetName;
    public string AssetName
    {
        get => _assetName;
        set => this.RaiseAndSetIfChanged(ref _assetName, value);
    }

    private string _className;
    public string ClassName
    {
        get => _className;
        set => this.RaiseAndSetIfChanged(ref _className, value);
    }

    private bool _hasSelection;
    public bool HasSelection
    {
        get => _hasSelection;
        set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
    }

    public ObservableCollection<KeyValuePair<string, string>> Tags { get; }
        = new ObservableCollection<KeyValuePair<string, string>>();

    public ObservableCollection<string> HardDependencies { get; }
        = new ObservableCollection<string>();

    public ObservableCollection<string> SoftDependencies { get; }
        = new ObservableCollection<string>();

    public void ShowAsset(AssetInfo asset)
    {
        if (asset == null)
        {
            HasSelection = false;
            ObjectPath = null;
            PackagePath = null;
            AssetName = null;
            ClassName = null;
            Tags.Clear();
            HardDependencies.Clear();
            SoftDependencies.Clear();
            return;
        }

        HasSelection = true;
        ObjectPath = asset.ObjectPath;
        PackagePath = asset.PackagePath;
        AssetName = asset.AssetName;
        ClassName = asset.ClassName;

        Tags.Clear();
        foreach (var tag in asset.Tags)
        {
            Tags.Add(tag);
        }

        HardDependencies.Clear();
        foreach (var dep in asset.Dependencies.Hard)
        {
            HardDependencies.Add(dep);
        }

        SoftDependencies.Clear();
        foreach (var dep in asset.Dependencies.Soft)
        {
            SoftDependencies.Add(dep);
        }
    }
}
```

**책임:**
- 선택된 에셋의 전체 메타데이터 표시
- 태그 키-값 쌍 테이블 표시
- 하드/소프트 의존성 목록 표시
- 선택 없음 상태 처리

### 3.6 EventLogViewModel

에셋 이벤트 스트림의 실시간 로그 표시.

```csharp
// EventLogViewModel.cs
public class EventLogViewModel : ReactiveObject
{
    private readonly IBridgeService _bridgeService;
    private const int MaxLogItems = 10000;

    public ObservableCollection<EventLogItem> Events { get; }
        = new ObservableCollection<EventLogItem>();

    private bool _showCreated = true;
    public bool ShowCreated
    {
        get => _showCreated;
        set => this.RaiseAndSetIfChanged(ref _showCreated, value);
    }

    private bool _showDeleted = true;
    public bool ShowDeleted
    {
        get => _showDeleted;
        set => this.RaiseAndSetIfChanged(ref _showDeleted, value);
    }

    private bool _showRenamed = true;
    public bool ShowRenamed
    {
        get => _showRenamed;
        set => this.RaiseAndSetIfChanged(ref _showRenamed, value);
    }

    private bool _showSaved = true;
    public bool ShowSaved
    {
        get => _showSaved;
        set => this.RaiseAndSetIfChanged(ref _showSaved, value);
    }

    private bool _showOther = true;
    public bool ShowOther
    {
        get => _showOther;
        set => this.RaiseAndSetIfChanged(ref _showOther, value);
    }

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
    }

    private bool _hasOverflowWarning;
    public bool HasOverflowWarning
    {
        get => _hasOverflowWarning;
        set => this.RaiseAndSetIfChanged(ref _hasOverflowWarning, value);
    }

    private string _overflowMessage;
    public string OverflowMessage
    {
        get => _overflowMessage;
        set => this.RaiseAndSetIfChanged(ref _overflowMessage, value);
    }

    // Prism DelegateCommand
    public DelegateCommand ClearLogCommand { get; }
    public DelegateCommand DismissOverflowWarningCommand { get; }

    public EventLogViewModel(IBridgeService bridgeService)
    {
        _bridgeService = bridgeService;

        ClearLogCommand = new DelegateCommand(() => Events.Clear());
        DismissOverflowWarningCommand = new DelegateCommand(
            () => HasOverflowWarning = false);

        // 필터 토글 변경 시 즉시 반영 (WhenAnyValue)
        this.WhenAnyValue(
                x => x.ShowCreated,
                x => x.ShowDeleted,
                x => x.ShowRenamed,
                x => x.ShowSaved,
                x => x.ShowOther)
            .Subscribe(_ => RebuildFilteredView());

        _bridgeService.EventReceived += OnEventReceived;
        _bridgeService.EventOverflow += OnEventOverflow;
    }

    private void OnEventReceived(
        object sender, AssetEventReceivedEventArgs e)
    {
        var item = new EventLogItem
        {
            SequenceNumber = e.Event.SequenceNumber,
            Timestamp = e.Event.Timestamp,
            EventType = e.Event.EventType,
            ObjectPath = e.Event.ObjectPath,
            Description = FormatEventDescription(e.Event)
        };

        if (!ShouldShow(item.EventType))
            return;

        Events.Insert(0, item);

        // 메모리 보호: 최대 항목 수 초과 시 오래된 항목 제거
        while (Events.Count > MaxLogItems)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private void OnEventOverflow(
        object sender, EventOverflowEventArgs e)
    {
        HasOverflowWarning = true;
        OverflowMessage = $"이벤트 {e.MissedCount}건 누락 - Snapshot 동기화 진행 중";
    }

    private bool ShouldShow(AssetEventType type) => type switch
    {
        AssetEventType.AssetCreated => ShowCreated,
        AssetEventType.AssetDeleted => ShowDeleted,
        AssetEventType.AssetRenamed => ShowRenamed,
        AssetEventType.AssetSaved => ShowSaved,
        _ => ShowOther
    };

    private string FormatEventDescription(AssetEvent evt) => evt.EventType switch
    {
        AssetEventType.AssetCreated => $"[생성] {evt.AssetName}",
        AssetEventType.AssetDeleted => $"[삭제] {evt.AssetName}",
        AssetEventType.AssetRenamed => $"[이름변경] {evt.OldAssetName} → {evt.AssetName}",
        AssetEventType.AssetSaved => $"[저장] {evt.AssetName}",
        AssetEventType.AssetMoved => $"[이동] {evt.AssetName}",
        AssetEventType.AssetTagsChanged => $"[태그변경] {evt.AssetName}",
        _ => $"[{evt.EventType}] {evt.ObjectPath}"
    };

    private void RebuildFilteredView()
    {
        // CollectionViewSource 필터 갱신으로 대체 가능
        // 현재는 전체 목록에 필터를 적용하지 않고
        // 삽입 시점에 ShouldShow()로 제어
    }
}
```

**책임:**
- 실시간 이벤트 수신 및 로그 항목 추가
- `WhenAnyValue`로 이벤트 타입별 필터 토글 감시
- 최대 항목 수 제한 (오래된 항목 자동 제거)
- `DelegateCommand`로 로그 초기화 및 오버플로 경고 해제

---

## 4. Adapter 소비 방식

### 4.1 IBridgeService (Dispatcher 마샬링 래퍼)

Adapter의 이벤트는 백그라운드 스레드에서 발행되므로, WPF UI 스레드로 마샬링하는 서비스 레이어를 도입한다.

```csharp
// IBridgeService.cs
public interface IBridgeService : IDisposable
{
    ConnectionState State { get; }
    AssetSnapshot CurrentSnapshot { get; }

    Task ConnectAsync(string mmfName);
    Task DisconnectAsync();
    Task<AssetSnapshot> RefreshSnapshotAsync();

    event EventHandler<SnapshotReceivedEventArgs> SnapshotReceived;
    event EventHandler<AssetEventReceivedEventArgs> EventReceived;
    event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
    event EventHandler<EventOverflowEventArgs> EventOverflow;
}
```

```csharp
// BridgeService.cs
public class BridgeService : IBridgeService
{
    private readonly IBridgeClient _client;
    private readonly Dispatcher _dispatcher;

    public ConnectionState State => _client.State;
    public AssetSnapshot CurrentSnapshot => _client.CurrentSnapshot;

    public event EventHandler<SnapshotReceivedEventArgs> SnapshotReceived;
    public event EventHandler<AssetEventReceivedEventArgs> EventReceived;
    public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
    public event EventHandler<EventOverflowEventArgs> EventOverflow;

    public BridgeService(IBridgeClient client)
    {
        _client = client;
        _dispatcher = Application.Current.Dispatcher;

        _client.SnapshotReceived += (s, e) =>
            _dispatcher.BeginInvoke(
                () => SnapshotReceived?.Invoke(this, e));

        _client.EventReceived += (s, e) =>
            _dispatcher.BeginInvoke(
                () => EventReceived?.Invoke(this, e));

        _client.ConnectionStateChanged += (s, e) =>
            _dispatcher.BeginInvoke(
                () => ConnectionStateChanged?.Invoke(this, e));

        _client.EventOverflow += (s, e) =>
            _dispatcher.BeginInvoke(
                () => EventOverflow?.Invoke(this, e));
    }

    public Task ConnectAsync(string mmfName)
        => _client.ConnectAsync(mmfName);

    public Task DisconnectAsync()
        => _client.DisconnectAsync();

    public Task<AssetSnapshot> RefreshSnapshotAsync()
        => _client.RefreshSnapshotAsync();

    public void Dispose()
        => _client.Dispose();
}
```

### 4.2 소비 흐름

```
[Adapter 백그라운드 스레드]
    │
    ▼
IBridgeClient.EventReceived 발행
    │
    ▼
BridgeService (Dispatcher.BeginInvoke)
    │
    ▼
[WPF UI 스레드]
    │
    ▼
EventLogViewModel.OnEventReceived()
    │
    ▼
ObservableCollection.Insert(0, item) → UI 자동 갱신
```

---

## 5. 대량 데이터 UI 갱신 전략

### 5.1 문제 정의

Unreal 프로젝트는 수만~수십만 개의 에셋을 포함할 수 있다. Snapshot 수신 시 전체 목록을 ObservableCollection에 한 번에 추가하면 UI가 멈춘다.

### 5.2 해결 전략

#### 전략 1: 배치 갱신 (Batch Update)

Snapshot 수신 시 ObservableCollection 전체 교체 후 Reset 알림을 보낸다.

```csharp
// AssetListViewModel 내부
private void OnSnapshotReceived(AssetSnapshot snapshot)
{
    _allAssets = snapshot.Assets.ToList();
    RebuildFilteredList();
}

private void RebuildFilteredList()
{
    var filtered = ApplyFilters(_allAssets);

    FilteredAssets.Clear();
    foreach (var item in filtered)
    {
        FilteredAssets.Add(new AssetItemViewModel(item));
    }

    TotalCount = _allAssets.Count;
    FilteredCount = filtered.Count;
}
```

#### 전략 2: 가상화 (VirtualizingStackPanel)

WPF의 UI 가상화를 활용하여 화면에 보이는 항목만 렌더링한다.

```xml
<!-- AssetListView.xaml -->
<ListView ItemsSource="{Binding FilteredAssets}"
          VirtualizingStackPanel.IsVirtualizing="True"
          VirtualizingStackPanel.VirtualizationMode="Recycling"
          ScrollViewer.IsDeferredScrollingEnabled="True">
    <ListView.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListView.ItemsPanel>
</ListView>
```

#### 전략 3: 필터 디바운싱 (WhenAnyValue + Throttle)

`WhenAnyValue` + `Throttle`을 사용하여 필터 변경 시 300ms 디바운싱을 자연스럽게 적용한다. 수동 `CancellationTokenSource` 관리가 불필요하다.

```csharp
// AssetListViewModel 생성자 내부
this.WhenAnyValue(
        x => x.SearchText,
        x => x.ClassFilter,
        x => x.PathFilter)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ => RebuildFilteredList());
```

#### 전략 4: 증분 이벤트 적용

실시간 이벤트(AssetCreated, AssetDeleted 등)는 전체 목록을 재구성하지 않고 해당 항목만 추가/제거/갱신한다.

```csharp
private void OnEventReceived(AssetEvent evt)
{
    switch (evt.EventType)
    {
        case AssetEventType.AssetCreated:
            AddAssetIfMatchesFilter(evt);
            break;
        case AssetEventType.AssetDeleted:
            RemoveAssetByPath(evt.ObjectPath);
            break;
        case AssetEventType.AssetRenamed:
            UpdateAssetByPath(evt.OldObjectPath, evt);
            break;
    }
}
```

---

## 6. 연결 상태 UX 처리 방식

### 6.1 상태별 UI 표시

| ConnectionState | 색상 | 아이콘 | 상태 텍스트 | 동작 |
|-----------------|------|--------|-------------|------|
| `Disconnected` | 회색 (#888888) | 빈 원 | "연결 안됨" | 연결 버튼 활성화, 해제 버튼 비활성화 |
| `Connecting` | 노란색 (#FFC107) | 회전 로딩 | "연결 중..." | 양쪽 버튼 비활성화 |
| `Connected` | 초록색 (#4CAF50) | 채워진 원 | "연결됨 - {ProjectName}" | 연결 버튼 비활성화, 해제 버튼 활성화 |
| `Lost` | 주황색 (#FF9800) | 경고 삼각형 | "응답 없음 - 재연결 대기 중" | 해제 버튼 활성화 |
| `VersionMismatch` | 빨간색 (#F44336) | X 표시 | "프로토콜 버전 불일치" | 연결 버튼 활성화 |
| `Error` | 빨간색 (#F44336) | X 표시 | "연결 오류: {메시지}" | 연결 버튼 활성화 |

### 6.2 연결 상태 패널 레이아웃

```
┌───────────────────────────────────────────────────────────────┐
│  ● 연결됨 - MyGame (PID: 12345)          [연결 해제]          │
├───────────────────────────────────────────────────────────────┤
│  인스턴스 선택: [▼ MyGame (12345)       ]  [새로고침] [연결]  │
└───────────────────────────────────────────────────────────────┘
```

### 6.3 연결 끊김 시 UI 동작

```
[Connected → Lost 전이]
    │
    ├─ 상태 표시줄: 주황색 + "응답 없음" 표시
    ├─ 에셋 목록: 현재 데이터 유지 (마지막 Snapshot)
    ├─ 이벤트 로그: "Editor 응답 없음 감지" 항목 추가
    └─ 오버레이: 반투명 경고 배너 표시

[Lost → Connected 전이 (복구)]
    │
    ├─ 상태 표시줄: 초록색 복귀
    ├─ Snapshot 자동 재요청 → 목록 갱신
    └─ 이벤트 로그: "연결 복구" 항목 추가

[Lost → Disconnected 전이 (30초 경과)]
    │
    ├─ 상태 표시줄: 회색 + "연결 안됨"
    ├─ 에셋 목록: 빈 상태 표시 ("연결 후 에셋 목록이 표시됩니다")
    ├─ 이벤트 로그: "연결 종료" 항목 추가
    └─ 인스턴스 목록 자동 새로고침
```

### 6.4 메인 윈도우 레이아웃

```
┌─────────────────────────────────────────────────────────┐
│  UnrealEditorBridge                              _ □ X  │
├─────────────────────────────────────────────────────────┤
│  [연결 상태 패널]                                        │
├──────────────────────────┬──────────────────────────────┤
│                          │                              │
│    에셋 목록              │    에셋 상세 정보             │
│    ┌──────────────────┐  │    ┌────────────────────┐    │
│    │ 검색: [________] │  │    │ ObjectPath: ...     │    │
│    │ 클래스: [▼ 전체 ]│  │    │ ClassName: ...      │    │
│    ├──────────────────┤  │    │ PackagePath: ...    │    │
│    │ SK_Hero          │  │    ├────────────────────┤    │
│    │ M_Hero           │◄─┤    │ 태그                │    │
│    │ BP_NewActor      │  │    │  Purpose: Character │    │
│    │ SM_Wall          │  │    ├────────────────────┤    │
│    │ ...              │  │    │ 의존성              │    │
│    └──────────────────┘  │    │  Hard: M_Hero       │    │
│    총 1,234개 / 56개 표시 │    │  Soft: ABP_Hero     │    │
│                          │    └────────────────────┘    │
├──────────────────────────┴──────────────────────────────┤
│  이벤트 로그                                  [지우기]  │
│  ┌──────────────────────────────────────────────────┐   │
│  │ 12:00:05 [저장] MainLevel                        │   │
│  │ 12:00:03 [생성] BP_NewActor                      │   │
│  │ 12:00:01 [이름변경] SM_Old → SM_New              │   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│  ● 연결됨 - MyGame | 에셋: 1,234개 | 이벤트: 56건      │
└─────────────────────────────────────────────────────────┘
```

---

## 7. 기술 스택 요약

| 항목 | 선택 | 근거 |
|------|------|------|
| MVVM 기반 클래스 | `ReactiveObject` (ReactiveUI) | `RaiseAndSetIfChanged`로 프로퍼티 변경 알림, `WhenAnyValue`로 반응형 관찰 |
| DI 컨테이너 | Unity 4.0.1 (via Prism.Unity) | Prism과 통합된 DI, `PrismApplication`에서 자동 관리 |
| Command | `DelegateCommand` (Prism.Core) | `CanExecute` 수동 관리 가능, `ReactiveCommand` 사용 금지 |
| 반응형 필터링 | `WhenAnyValue` + `Throttle` | 디바운싱과 스레드 마샬링을 선언적으로 처리 |
| View-ViewModel 연결 | Prism ViewModelLocator | `prism:ViewModelLocator.AutoWireViewModel="True"` |
| 파일 구조 규칙 | 클래스 1개 = 파일 1개 | 중첩 클래스 금지, MVVM 폴더 구조 엄격 준수 |
