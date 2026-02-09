# UnrealEditorBridge.Wpf API Reference

> **프로젝트**: UnrealEditorBridge.Wpf
> **타겟 프레임워크**: .NET 8.0-windows (WPF)
> **주요 의존성**: ReactiveUI.WPF 20.1.1, Prism.Core 9.0.537, Unity.Container 5.11.11

---

## 목차

1. [DI 컨테이너 구성](#1-di-컨테이너-구성)
2. [Services](#2-services)
3. [ViewModels](#3-viewmodels)
4. [Models](#4-models)
5. [Converters](#5-converters)
6. [MVVM 규칙](#6-mvvm-규칙)

---

## 1. DI 컨테이너 구성

`App.xaml.cs`에서 UnityContainer를 사용하여 모든 의존성을 등록한다.

```csharp
// 등록 순서
container.RegisterType<IBridgeClient>(
    new ContainerControlledLifetimeManager(),
    new InjectionFactory(c => BridgeClientFactory.Create()));

container.RegisterType<IBridgeService, BridgeService>(
    new ContainerControlledLifetimeManager());

container.RegisterType<EditorInstanceDiscovery>(
    new ContainerControlledLifetimeManager());

// ViewModel은 Transient
container.RegisterType<MainViewModel>();
container.RegisterType<ConnectionViewModel>();
container.RegisterType<AssetListViewModel>();
container.RegisterType<AssetDetailViewModel>();
container.RegisterType<EventLogViewModel>();
```

---

## 2. Services

### 2.1 IBridgeService

```csharp
namespace UnrealEditorBridge.Wpf.Services
{
    public interface IBridgeService : IDisposable
}
```

ViewModel이 Adapter를 소비하기 위한 서비스 인터페이스.
백그라운드 스레드 이벤트를 UI 스레드(Dispatcher)로 마샬링한다.

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `State` | `ConnectionState` | 현재 연결 상태 |
| `CurrentSnapshot` | `AssetSnapshot?` | 가장 최근 수신한 에셋 스냅샷 |

#### Methods

| 시그니처 | 설명 |
|----------|------|
| `Task ConnectAsync(string mmfName)` | 지정된 MMF 이름으로 Editor에 연결 |
| `Task DisconnectAsync()` | 연결 종료 |
| `Task<AssetSnapshot> RefreshSnapshotAsync()` | Snapshot 강제 재요청 |

#### Events

| 이름 | EventArgs 타입 | 설명 |
|------|----------------|------|
| `SnapshotReceived` | `SnapshotReceivedEventArgs` | 새 Snapshot 수신 시 (UI 스레드) |
| `EventReceived` | `AssetEventReceivedEventArgs` | 에셋 이벤트 수신 시 (UI 스레드) |
| `ConnectionStateChanged` | `ConnectionStateChangedEventArgs` | 연결 상태 변경 시 (UI 스레드) |
| `EventOverflow` | `EventOverflowEventArgs` | Ring Buffer 오버플로 감지 시 (UI 스레드) |

---

### 2.2 BridgeService

```csharp
namespace UnrealEditorBridge.Wpf.Services
{
    public sealed class BridgeService : IBridgeService
}
```

`IBridgeService`의 구현체. `IBridgeClient`의 백그라운드 스레드 이벤트를
`Application.Current.Dispatcher.BeginInvoke()`로 UI 스레드에 마샬링한다.

#### Constructor

```csharp
public BridgeService(IBridgeClient client)
```

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `client` | `IBridgeClient` | Adapter 클라이언트 인스턴스 |

#### 마샬링 패턴

```csharp
_client.SnapshotReceived += (s, e) =>
    _dispatcher.BeginInvoke(() => SnapshotReceived?.Invoke(this, e));
```

모든 이벤트가 동일한 패턴으로 UI 스레드에 전달된다.

---

## 3. ViewModels

모든 ViewModel은 `ReactiveObject`를 상속하며, Prism의 `DelegateCommand`를 사용한다.

### 3.1 MainViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class MainViewModel : ReactiveObject
}
```

메인 윈도우의 최상위 ViewModel. 하위 ViewModel을 보유하고 이들 간 반응형 연결을 조율한다.

#### Constructor

```csharp
public MainViewModel(
    ConnectionViewModel connection,
    AssetListViewModel assetList,
    AssetDetailViewModel assetDetail,
    EventLogViewModel eventLog)
```

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `Connection` | `ConnectionViewModel` | 연결 패널 ViewModel |
| `AssetList` | `AssetListViewModel` | 에셋 목록 ViewModel |
| `AssetDetail` | `AssetDetailViewModel` | 에셋 상세 ViewModel |
| `EventLog` | `EventLogViewModel` | 이벤트 로그 ViewModel |
| `StatusMessage` | `string` | 상태 표시줄 메시지 |
| `IsLoading` | `bool` | 로딩 상태 여부 |

#### 반응형 연결

```csharp
// 에셋 선택 → 상세 정보 갱신
AssetList.WhenAnyValue(x => x.SelectedAsset)
    .Subscribe(selected => AssetDetail.ShowAsset(selected?.AssetInfo));

// 연결 상태 → 상태 표시줄
Connection.WhenAnyValue(x => x.StateText)
    .Subscribe(text => StatusMessage = text);
```

---

### 3.2 ConnectionViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class ConnectionViewModel : ReactiveObject
}
```

Editor 인스턴스 탐색 및 연결 관리 ViewModel.

#### Constructor

```csharp
public ConnectionViewModel(
    IBridgeService bridgeService,
    EditorInstanceDiscovery discovery)
```

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `Instances` | `ObservableCollection<EditorInstanceInfo>` | 활성 Editor 인스턴스 목록 |
| `SelectedInstance` | `EditorInstanceInfo?` | 선택된 인스턴스 |
| `State` | `ConnectionState` | 현재 연결 상태 |
| `StateText` | `string` | 상태 표시 텍스트 |
| `IsConnecting` | `bool` | 연결 중 여부 |

#### Commands

| 이름 | CanExecute 조건 | 동작 |
|------|-----------------|------|
| `ConnectCommand` | `SelectedInstance != null && State == Disconnected && !IsConnecting` | 선택된 인스턴스에 연결 |
| `DisconnectCommand` | `State == Connected \|\| State == Lost` | 연결 해제 |
| `RefreshInstancesCommand` | 항상 | Discovery MMF에서 인스턴스 목록 갱신 |

#### 상태 텍스트 매핑

| ConnectionState | StateText |
|----------------|-----------|
| `Disconnected` | "연결 안됨" |
| `Connecting` | "연결 중..." |
| `Connected` | "연결됨 - {ProjectName}" |
| `Lost` | "응답 없음 - 재연결 대기 중" |
| `VersionMismatch` | "프로토콜 버전 불일치" |
| `Error` | "연결 오류" |

---

### 3.3 AssetListViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class AssetListViewModel : ReactiveObject
}
```

에셋 목록 표시 및 필터링 ViewModel.

#### Constructor

```csharp
public AssetListViewModel(IBridgeService bridgeService)
```

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `FilteredAssets` | `ObservableCollection<AssetItemViewModel>` | 필터링된 에셋 목록 |
| `SearchText` | `string?` | 검색 텍스트 (이름/경로) |
| `ClassFilter` | `string?` | 클래스 필터 |
| `PathFilter` | `string?` | 경로 필터 |
| `SelectedAsset` | `AssetItemViewModel?` | 현재 선택된 에셋 |
| `TotalCount` | `int` | 전체 에셋 개수 |
| `FilteredCount` | `int` | 필터링된 에셋 개수 |
| `AvailableClasses` | `ObservableCollection<string>` | 사용 가능한 클래스 목록 |

#### Commands

| 이름 | 동작 |
|------|------|
| `ClearFiltersCommand` | SearchText, ClassFilter, PathFilter 초기화 |
| `RefreshCommand` | `RefreshSnapshotAsync()` 호출 후 목록 재구성 |

#### 필터링 동작

- `WhenAnyValue`로 SearchText/ClassFilter/PathFilter 변경 감지
- **300ms Throttle** 후 `RebuildFilteredList()` 호출
- 검색: AssetName 또는 ObjectPath에 대한 대소문자 무시 부분 일치
- 클래스: 정확 일치
- 경로: `PackagePath.StartsWith()` 접두사 일치

#### 실시간 이벤트 반영

| EventType | 동작 |
|-----------|------|
| `AssetCreated` | `_allAssets`에 추가 + 필터 일치 시 `FilteredAssets`에 추가 |
| `AssetDeleted` | `_allAssets`에서 제거 + `FilteredAssets`에서 제거 |
| `AssetRenamed` | 이전 경로 제거 + 새 에셋 추가 |

---

### 3.4 AssetItemViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class AssetItemViewModel : ReactiveObject
}
```

에셋 목록의 개별 항목 ViewModel.

#### Constructor

```csharp
public AssetItemViewModel(AssetInfo assetInfo)
```

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `AssetInfo` | `AssetInfo` | 원본 에셋 정보 |
| `AssetName` | `string` | 에셋 이름 |
| `ClassName` | `string` | 에셋 클래스명 |
| `ObjectPath` | `string` | 에셋 오브젝트 경로 |

---

### 3.5 AssetDetailViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class AssetDetailViewModel : ReactiveObject
}
```

선택된 에셋의 상세 정보를 표시하는 ViewModel.

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `ObjectPath` | `string?` | 에셋 오브젝트 경로 |
| `PackagePath` | `string?` | 에셋 패키지 경로 |
| `AssetName` | `string?` | 에셋 이름 |
| `ClassName` | `string?` | 에셋 클래스명 |
| `HasSelection` | `bool` | 에셋 선택 여부 |
| `Tags` | `ObservableCollection<KeyValuePair<string, string>>` | 에셋 태그 목록 |
| `HardDependencies` | `ObservableCollection<string>` | 하드 의존성 경로 |
| `SoftDependencies` | `ObservableCollection<string>` | 소프트 의존성 경로 |

#### Methods

```csharp
public void ShowAsset(AssetInfo? asset)
```

지정된 에셋의 상세 정보를 표시한다. `null`이면 선택 해제.

---

### 3.6 EventLogViewModel

```csharp
namespace UnrealEditorBridge.Wpf.ViewModels
{
    public class EventLogViewModel : ReactiveObject
}
```

에셋 이벤트 스트림의 실시간 로그 ViewModel.

#### Constructor

```csharp
public EventLogViewModel(IBridgeService bridgeService)
```

#### Properties

| 이름 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `Events` | `ObservableCollection<EventLogItem>` | 빈 컬렉션 | 이벤트 로그 항목 |
| `ShowCreated` | `bool` | `true` | 생성 이벤트 표시 |
| `ShowDeleted` | `bool` | `true` | 삭제 이벤트 표시 |
| `ShowRenamed` | `bool` | `true` | 이름 변경 이벤트 표시 |
| `ShowSaved` | `bool` | `true` | 저장 이벤트 표시 |
| `ShowOther` | `bool` | `true` | 기타 이벤트 표시 |
| `AutoScroll` | `bool` | `true` | 자동 스크롤 |
| `HasOverflowWarning` | `bool` | `false` | 오버플로 경고 표시 |
| `OverflowMessage` | `string?` | `null` | 오버플로 경고 메시지 |

#### Commands

| 이름 | 동작 |
|------|------|
| `ClearLogCommand` | 로그 목록 초기화 |
| `DismissOverflowWarningCommand` | 오버플로 경고 숨김 |

---

## 4. Models

### 4.1 EventLogItem

```csharp
namespace UnrealEditorBridge.Wpf.Models
{
    public sealed class EventLogItem
}
```

이벤트 로그 뷰에 표시되는 개별 로그 항목.

#### Properties

| 이름 | 타입 | 설명 |
|------|------|------|
| `SequenceNumber` | `ulong` | 이벤트 시퀀스 번호 |
| `Timestamp` | `DateTime` | 이벤트 발생 시각 |
| `EventType` | `AssetEventType` | 이벤트 타입 |
| `ObjectPath` | `string` | 대상 에셋 경로 |
| `Description` | `string` | 로그 표시용 설명 문자열 |

---

## 5. Converters

WPF View에서 사용하는 IValueConverter 구현체.

| 클래스 | 입력 타입 | 출력 타입 | 설명 |
|--------|-----------|-----------|------|
| `ConnectionStateToColorConverter` | `ConnectionState` | `Brush` | 상태별 색상 (Gray/Green/Orange/Red) |
| `ConnectionStateToBoolConverter` | `ConnectionState` | `bool` | Connected이면 true |
| `EventTypeToIconConverter` | `AssetEventType` | `string` | 이벤트 타입별 아이콘 문자열 |
| `BoolToVisibilityConverter` | `bool` | `Visibility` | true→Visible, false→Collapsed |

### 상태별 색상 매핑

| ConnectionState | 색상 |
|----------------|------|
| `Disconnected` | Gray |
| `Connecting` | Orange |
| `Connected` | Green |
| `Lost` | Orange |
| `VersionMismatch` | Red |
| `Error` | Red |

---

## 6. MVVM 규칙

이 프로젝트에서 준수하는 WPF MVVM 규칙:

### ViewModel 기본 클래스

```csharp
// ReactiveObject 사용 (ObservableObject 사용 금지)
public class MyViewModel : ReactiveObject
```

### Property 변경 알림

```csharp
// RaiseAndSetIfChanged 필수
private string _name;
public string Name
{
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
}
```

### Property 관찰

```csharp
// WhenAnyValue 필수
this.WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ => DoSearch());
```

### Command

```csharp
// DelegateCommand (Prism) ONLY - ReactiveCommand 사용 금지
public DelegateCommand SaveCommand { get; }

SaveCommand = new DelegateCommand(
    async () => await ExecuteSaveAsync(),
    () => CanSave());
```

### DI 컨테이너

```csharp
// UnityContainer 직접 사용 (PrismApplication 사용 금지)
var container = new UnityContainer();
container.RegisterType<IBridgeService, BridgeService>(
    new ContainerControlledLifetimeManager());
```

### 파일 구조

- 클래스 하나당 파일 하나
- 중첩 클래스 금지
- MVVM 폴더 구조: `ViewModels/`, `Views/`, `Models/`, `Services/`, `Converters/`
