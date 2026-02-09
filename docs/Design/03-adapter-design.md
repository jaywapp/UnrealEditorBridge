# UnrealEditorBridge.Adapter 설계

## 1. 개요

UnrealEditorBridge.Adapter는 순수 .NET Class Library로, Memory-Mapped File 기반 IPC 통신을 담당하며 외부 소비자(WPF, Console, 다른 .NET 앱)에게 안정적인 Client API를 제공한다. UI 프레임워크 의존성이 전혀 없으며 여러 UE Editor 인스턴스를 동시에 지원한다.

**프로젝트 구성:**
- 타겟 프레임워크: `net8.0` (추가로 `netstandard2.0` 멀티타겟 가능)
- NuGet 의존성: 없음 (System 네임스페이스만 사용)
- 어셈블리명: `UnrealEditorBridge.Adapter.dll`

---

## 2. Public API 구조

### 2.1 네임스페이스 구조

```
UnrealEditorBridge.Adapter
├── IBridgeClient                    // 핵심 Public API
├── BridgeClientFactory              // IBridgeClient 생성 팩토리
├── EditorInstanceDiscovery          // Editor 인스턴스 탐색
├── EditorInstanceInfo               // 인스턴스 정보 DTO
├── ConnectionState                  // 연결 상태 열거형
├── Models/
│   ├── AssetSnapshot                // 스냅샷 전체 모델
│   ├── AssetInfo                    // 개별 에셋 정보
│   ├── AssetEvent                   // 이벤트 모델
│   └── AssetEventType               // 이벤트 타입 열거형
└── Events/
    ├── SnapshotReceivedEventArgs    // 스냅샷 수신 이벤트 인자
    ├── AssetEventReceivedEventArgs  // 에셋 이벤트 수신 인자
    ├── ConnectionStateChangedEventArgs // 연결 상태 변경 인자
    └── EventOverflowEventArgs       // 이벤트 오버플로 인자
```

### 2.2 IBridgeClient 인터페이스

```csharp
namespace UnrealEditorBridge.Adapter;

public interface IBridgeClient : IDisposable
{
    // === 연결 관리 ===

    /// <summary>
    /// 지정된 Editor 인스턴스에 연결한다.
    /// </summary>
    Task ConnectAsync(string mmfName, CancellationToken ct = default);

    /// <summary>
    /// 연결을 종료하고 모든 자원을 정리한다.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 현재 연결 상태를 반환한다.
    /// </summary>
    ConnectionState State { get; }

    // === 데이터 접근 ===

    /// <summary>
    /// 가장 최근 수신한 에셋 스냅샷을 반환한다.
    /// 연결 전이면 null을 반환한다.
    /// </summary>
    AssetSnapshot? CurrentSnapshot { get; }

    /// <summary>
    /// 강제로 Snapshot을 다시 읽는다.
    /// </summary>
    Task<AssetSnapshot> RefreshSnapshotAsync(CancellationToken ct = default);

    // === 이벤트 ===

    /// <summary>
    /// 새 Snapshot이 수신되었을 때 발생한다.
    /// </summary>
    event EventHandler<SnapshotReceivedEventArgs> SnapshotReceived;

    /// <summary>
    /// 에셋 이벤트가 수신되었을 때 발생한다.
    /// </summary>
    event EventHandler<AssetEventReceivedEventArgs> EventReceived;

    /// <summary>
    /// 연결 상태가 변경되었을 때 발생한다.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

    /// <summary>
    /// Ring Buffer 오버플로가 감지되었을 때 발생한다.
    /// </summary>
    event EventHandler<EventOverflowEventArgs> EventOverflow;
}
```

### 2.3 ConnectionState 열거형

```csharp
public enum ConnectionState
{
    /// <summary>연결되지 않은 초기 상태</summary>
    Disconnected,

    /// <summary>연결 시도 중</summary>
    Connecting,

    /// <summary>정상 연결 상태</summary>
    Connected,

    /// <summary>Heartbeat 미수신 (Editor 응답 없음 의심)</summary>
    Lost,

    /// <summary>프로토콜 버전 불일치로 연결 불가</summary>
    VersionMismatch,

    /// <summary>오류로 인한 연결 실패</summary>
    Error
}
```

### 2.4 데이터 모델 클래스

```csharp
public sealed class AssetSnapshot
{
    public DateTime Timestamp { get; init; }
    public int AssetCount { get; init; }
    public IReadOnlyList<AssetInfo> Assets { get; init; }
    public uint Version { get; init; }
}

public sealed class AssetInfo
{
    public string ObjectPath { get; init; }
    public string PackagePath { get; init; }
    public string AssetName { get; init; }
    public string ClassName { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; }
    public AssetDependencyInfo Dependencies { get; init; }
}

public sealed class AssetDependencyInfo
{
    public IReadOnlyList<string> Hard { get; init; }
    public IReadOnlyList<string> Soft { get; init; }
}

public sealed class AssetEvent
{
    public ulong SequenceNumber { get; init; }
    public DateTime Timestamp { get; init; }
    public AssetEventType EventType { get; init; }
    public string ObjectPath { get; init; }
    public string? AssetName { get; init; }
    public string? ClassName { get; init; }
    public string? OldObjectPath { get; init; }  // Rename/Move 시
    public string? OldAssetName { get; init; }    // Rename 시
    public string RawPayloadJson { get; init; }   // 원본 JSON
}

public enum AssetEventType
{
    None = 0,
    AssetCreated = 1,
    AssetDeleted = 2,
    AssetRenamed = 3,
    AssetSaved = 4,
    AssetTagsChanged = 5,
    AssetMoved = 6,
    AssetLoaded = 7,
    AssetDependencyChanged = 8,
    SnapshotUpdated = 100,
    EditorShutdown = 200
}
```

### 2.5 EditorInstanceDiscovery

```csharp
public sealed class EditorInstanceDiscovery : IDisposable
{
    /// <summary>
    /// 현재 실행 중인 모든 UE Editor 인스턴스 목록을 반환한다.
    /// Discovery MMF를 읽어 활성 인스턴스를 탐색한다.
    /// </summary>
    public IReadOnlyList<EditorInstanceInfo> GetActiveInstances();

    /// <summary>
    /// 인스턴스 목록 변경을 감시한다 (폴링 기반, 2초 간격).
    /// </summary>
    public event EventHandler<EventArgs> InstancesChanged;

    /// <summary>
    /// 감시를 시작한다.
    /// </summary>
    public void StartWatching();

    /// <summary>
    /// 감시를 중지한다.
    /// </summary>
    public void StopWatching();
}

public sealed class EditorInstanceInfo
{
    public uint ProcessId { get; init; }
    public string ProjectName { get; init; }
    public string MmfName { get; init; }
    public DateTime RegisteredAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public bool IsAlive { get; }  // LastHeartbeat 기준 5초 이내
}

public static class BridgeClientFactory
{
    /// <summary>
    /// IBridgeClient의 기본 구현체를 생성한다.
    /// </summary>
    public static IBridgeClient Create();

    /// <summary>
    /// 옵션을 지정하여 IBridgeClient를 생성한다.
    /// </summary>
    public static IBridgeClient Create(BridgeClientOptions options);
}

public sealed class BridgeClientOptions
{
    /// <summary>Heartbeat 체크 간격 (기본: 2초)</summary>
    public TimeSpan HeartbeatCheckInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Heartbeat 타임아웃 (기본: 5초)</summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>이벤트 폴링 간격 (Named Event 사용 시 무시됨)</summary>
    public TimeSpan EventPollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Snapshot 자동 갱신 여부 (기본: true)</summary>
    public bool AutoRefreshSnapshot { get; set; } = true;

    /// <summary>Overflow 감지 시 자동 Snapshot 재요청 여부 (기본: true)</summary>
    public bool AutoRecoverOnOverflow { get; set; } = true;
}
```

---

## 3. 내부 모듈 분리 구조

### 3.1 모듈 다이어그램

```
UnrealEditorBridge.Adapter (Assembly)
│
├── Public/                          (외부 노출)
│   ├── IBridgeClient.cs
│   ├── BridgeClientFactory.cs
│   ├── EditorInstanceDiscovery.cs
│   ├── Models/
│   │   ├── AssetSnapshot.cs
│   │   ├── AssetInfo.cs
│   │   ├── AssetEvent.cs
│   │   └── AssetEventType.cs
│   └── Events/
│       ├── SnapshotReceivedEventArgs.cs
│       ├── AssetEventReceivedEventArgs.cs
│       ├── ConnectionStateChangedEventArgs.cs
│       └── EventOverflowEventArgs.cs
│
└── Internal/                        (internal 접근 제한)
    ├── BridgeClient.cs              // IBridgeClient 구현체
    ├── Ipc/
    │   ├── MmfAccessor.cs           // MMF 열기/닫기/매핑
    │   ├── HeaderReader.cs          // Header 구조체 읽기
    │   ├── SnapshotReader.cs        // Snapshot 영역 읽기 + 역직렬화
    │   ├── EventRingReader.cs       // Ring Buffer 읽기 + 역직렬화
    │   └── DiscoveryMmfReader.cs    // Discovery MMF 읽기
    ├── Protocol/
    │   ├── ProtocolConstants.cs     // 매직넘버, 오프셋, 기본값
    │   ├── HeaderLayout.cs          // Header 필드 오프셋 상수
    │   ├── EventSlotLayout.cs       // Event 슬롯 레이아웃 상수
    │   └── Crc32.cs                 // CRC32 계산
    ├── Connection/
    │   ├── BridgeConnection.cs      // 연결 수립/해제 로직
    │   ├── HeartbeatMonitor.cs      // Heartbeat 감시 스레드
    │   └── ConnectionStateMachine.cs // 상태 전이 관리
    ├── Threading/
    │   ├── NamedMutexWrapper.cs     // Named Mutex 래퍼
    │   └── NamedEventWrapper.cs     // Named Event 래퍼
    └── Serialization/
        └── JsonSnapshotDeserializer.cs  // System.Text.Json 기반 역직렬화
```

### 3.2 모듈 책임

| 모듈 | 책임 |
|------|------|
| `Ipc/` | OS 수준 MMF 접근 로직 캡슐화 |
| `Protocol/` | 프로토콜 상수 및 레이아웃 정의 (C# 측 Protocol 구현) |
| `Connection/` | 연결 수명 관리 및 상태 기계 |
| `Threading/` | OS 동기화 객체 래퍼 (Dispose 패턴 적용) |
| `Serialization/` | JSON 역직렬화 로직 격리 |

---

## 4. Threading 모델

### 4.1 스레드 구조

```
┌─────────────────────────────────────────────────────────┐
│  BridgeClient 내부 스레드 구성                           │
│                                                          │
│  [호출자 스레드]                                         │
│    └─ ConnectAsync / DisconnectAsync / RefreshSnapshot   │
│                                                          │
│  [Heartbeat Monitor 스레드] (백그라운드, 단일)            │
│    └─ 주기: 2초                                          │
│    └─ Header.Heartbeat 읽기 → 상태 판단                  │
│    └─ ConnectionStateChanged 이벤트 발행                  │
│                                                          │
│  [Event Reader 스레드] (백그라운드, 단일)                 │
│    └─ Named Event 대기 (타임아웃 1초) → 이벤트 읽기      │
│    └─ EventReceived 이벤트 발행                           │
│    └─ Overflow 감지 시 Snapshot 재요청                    │
│                                                          │
│  [Snapshot Watcher 스레드] (백그라운드, 단일)             │
│    └─ Snapshot Named Event 대기                           │
│    └─ SnapshotVersion 변경 감지 → Snapshot 읽기          │
│    └─ SnapshotReceived 이벤트 발행                        │
└─────────────────────────────────────────────────────────┘
```

### 4.2 스레드 안전성 보장

| 항목 | 전략 |
|------|------|
| MMF 접근 | Named Mutex로 프로세스 간 동기화 |
| 내부 상태 변경 | `lock` 또는 `Interlocked` 사용 |
| 이벤트 발행 | 이벤트 핸들러는 백그라운드 스레드에서 호출됨. UI 스레드 마샬링은 소비자 책임 |
| CurrentSnapshot | `volatile` 참조 교체 패턴 (`Interlocked.Exchange`) |
| CancellationToken | 모든 백그라운드 루프에 전파하여 정상 종료 보장 |

### 4.3 이벤트 발행 스레드 정책

**설계 결정:** 이벤트는 백그라운드 스레드에서 직접 발행한다.

근거:
- Adapter에 `SynchronizationContext` 의존성을 도입하면 UI 프레임워크 결합이 발생한다
- WPF의 `Dispatcher`, WinForms의 `Control.Invoke` 등은 소비자 레이어에서 처리해야 한다
- 이 정책은 Adapter가 콘솔 앱, 서비스 등 UI가 없는 환경에서도 사용 가능하게 한다

소비자 측 권장 패턴:
```csharp
// WPF에서의 사용 예시
client.SnapshotReceived += (s, e) =>
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        // UI 갱신
    });
};
```

---

## 5. Connection Lifecycle

### 5.1 상태 전이 다이어그램

```
                    ┌──────────────┐
                    │ Disconnected │ ◄──────────────────┐
                    └──────┬───────┘                    │
                           │ ConnectAsync()             │ DisconnectAsync()
                           ▼                            │ 또는 복구 불가
                    ┌──────────────┐                    │
                    │  Connecting  │                    │
                    └──────┬───────┘                    │
                           │                            │
              ┌────────────┼────────────┐               │
              │ 성공        │ 버전불일치  │ 오류          │
              ▼            ▼            ▼               │
       ┌───────────┐ ┌──────────────┐ ┌───────┐        │
       │ Connected  │ │VersionMis-  │ │ Error │────────┤
       └─────┬─────┘ │   match     │ └───────┘        │
             │        └──────────────┘                  │
             │ Heartbeat 타임아웃                        │
             ▼                                          │
       ┌───────────┐                                    │
       │   Lost    │                                    │
       └─────┬─────┘                                    │
             │                                          │
             ├─ Heartbeat 복구 → Connected              │
             └─ 장기 미복구 (30초) ───────────────────────┘
```

### 5.2 연결 수립 절차

```
ConnectAsync(mmfName)
    │
    ├─ 1. State → Connecting
    │
    ├─ 2. MMF 열기 시도
    │     └─ 실패 → State = Error, 예외 발생
    │
    ├─ 3. Header 읽기
    │     ├─ Magic 검증 → 불일치 시 Error
    │     └─ ProtocolVersion Major 검증 → 불일치 시 VersionMismatch
    │
    ├─ 4. 초기 Snapshot 읽기
    │     └─ CurrentSnapshot 설정
    │
    ├─ 5. 백그라운드 스레드 시작
    │     ├─ Heartbeat Monitor
    │     ├─ Event Reader
    │     └─ Snapshot Watcher
    │
    └─ 6. State → Connected
```

### 5.3 연결 해제 절차

```
DisconnectAsync()
    │
    ├─ 1. CancellationTokenSource.Cancel()
    │     └─ 모든 백그라운드 스레드 종료 신호
    │
    ├─ 2. 백그라운드 스레드 Join (타임아웃: 3초)
    │
    ├─ 3. Named Event / Named Mutex 해제
    │
    ├─ 4. MMF 뷰 해제 및 MMF 닫기
    │
    └─ 5. State → Disconnected
```

---

## 6. Health Check / Heartbeat 처리

### 6.1 HeartbeatMonitor 동작

```csharp
// 의사 코드
while (!cancellationToken.IsCancellationRequested)
{
    await Task.Delay(HeartbeatCheckInterval, cancellationToken);

    long writerHeartbeat = headerReader.ReadHeartbeat();
    long now = DateTime.UtcNow.Ticks;
    TimeSpan elapsed = TimeSpan.FromTicks(now - writerHeartbeat);

    if (elapsed > HeartbeatTimeout)
    {
        if (currentState == ConnectionState.Connected)
        {
            TransitionTo(ConnectionState.Lost);
        }
        else if (currentState == ConnectionState.Lost
                 && elapsed > MaxLostDuration)  // 30초
        {
            TransitionTo(ConnectionState.Disconnected);
            // 자원 정리
        }
    }
    else
    {
        if (currentState == ConnectionState.Lost)
        {
            TransitionTo(ConnectionState.Connected);
            // Snapshot 재요청 (Lost 동안 누락 이벤트 보상)
        }
    }
}
```

### 6.2 Heartbeat 타이밍 상수

| 항목 | 값 | 설명 |
|------|-----|------|
| Writer 갱신 주기 | 1초 | Editor가 Heartbeat를 갱신하는 간격 |
| Reader 체크 주기 | 2초 | Adapter가 Heartbeat를 확인하는 간격 |
| Heartbeat 타임아웃 | 5초 | 이 시간 동안 미갱신 시 Lost 상태 |
| Lost → Disconnected | 30초 | Lost 상태 지속 시 완전 연결 해제 |

---

## 7. UI 비종속성 유지 전략

### 7.1 금지 의존성

Adapter 프로젝트에서 다음 참조를 금지한다:

- `PresentationFramework` (WPF)
- `PresentationCore` (WPF)
- `WindowsBase` (WPF)
- `System.Windows.Forms`
- 모든 UI 프레임워크 NuGet 패키지

### 7.2 검증 방법

```xml
<!-- UnrealEditorBridge.Adapter.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- UseWPF, UseWindowsForms 명시적 비활성화 -->
    <UseWPF>false</UseWPF>
    <UseWindowsForms>false</UseWindowsForms>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <!-- Windows API 접근이 필요하므로 net8.0-windows가 아닌
         net8.0 + EnableWindowsTargeting 조합 사용 -->
  </PropertyGroup>
</Project>
```

### 7.3 소비자 계약

- 모든 Public 이벤트는 **백그라운드 스레드**에서 발행됨
- UI 스레드 마샬링은 소비자가 직접 수행
- 데이터 모델은 불변(immutable) 객체로 제공
- `INotifyPropertyChanged` 등 UI 바인딩 인터페이스는 Adapter에서 구현하지 않음
- 이러한 인터페이스는 WPF ViewModel 레이어에서 래핑하여 구현

### 7.4 플랫폼 의존성 처리

MMF, Named Mutex, Named Event는 Windows API 기반이다. 현재 버전은 Windows 전용으로 설계하되, 추후 확장을 위해 다음과 같이 추상화한다:

```csharp
// 현재는 Windows 구현만 존재하나 인터페이스로 격리
internal interface IIpcTransport : IDisposable
{
    bool TryOpen(string name);
    ReadOnlySpan<byte> ReadHeader();
    ReadOnlySpan<byte> ReadSnapshot();
    ReadOnlySpan<byte> ReadEventSlot(int index);
    bool WaitForEvent(string eventName, TimeSpan timeout);
    bool AcquireMutex(TimeSpan timeout);
    void ReleaseMutex();
}
```
