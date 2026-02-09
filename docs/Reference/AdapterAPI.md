# UnrealEditorBridge.Adapter API 레퍼런스

> **네임스페이스:** `UnrealEditorBridge.Adapter`, `UnrealEditorBridge.Adapter.Models`, `UnrealEditorBridge.Adapter.Events`
> **어셈블리:** `UnrealEditorBridge.Adapter.dll`
> **대상 프레임워크:** .NET 8.0 / .NET 8.0-windows
> **역할:** Protocol 라이브러리 위에서 MMF 기반 IPC를 추상화하고, 연결 관리, Heartbeat 모니터링, Snapshot/Event 스트리밍 등 고수준 API를 제공한다.

---

## 목차

1. [IBridgeClient](#ibridgeclient)
2. [BridgeClientFactory](#bridgeclientfactory)
3. [BridgeClientOptions](#bridgeclientoptions)
4. [ConnectionState](#connectionstate)
5. [EditorInstanceDiscovery](#editorinstancediscovery)
6. [EditorInstanceInfo](#editorinstanceinfo)
7. [모델 (Models)](#모델-models)
   - [AssetSnapshot](#assetsnapshot)
   - [AssetInfo](#assetinfo)
   - [AssetDependencyInfo](#assetdependencyinfo)
   - [AssetEvent](#assetevent)
8. [이벤트 인자 (EventArgs)](#이벤트-인자-eventargs)
   - [SnapshotReceivedEventArgs](#snapshotreceivedeventargs)
   - [AssetEventReceivedEventArgs](#asseteventreceivedeventargs)
   - [ConnectionStateChangedEventArgs](#connectionstatechangedeventargs)
   - [EventOverflowEventArgs](#eventoverfloweventargs)
9. [사용 예시](#사용-예시)

---

## IBridgeClient

```csharp
public interface IBridgeClient : IDisposable
```

UnrealEditorBridge의 핵심 Public API. MMF 기반 IPC를 통해 Unreal Editor와 통신하는 클라이언트 인터페이스이다. 모든 이벤트는 **백그라운드 스레드**에서 발행되므로 UI 스레드 마샬링은 소비자 책임이다.

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `State` | `ConnectionState` | 현재 연결 상태 (읽기 전용). |
| `CurrentSnapshot` | `AssetSnapshot?` | 가장 최근 수신한 에셋 스냅샷. 연결 전이면 `null`. 스레드 안전하게 접근 가능 (volatile 참조 교체). |

### 메서드

#### ConnectAsync

```csharp
Task ConnectAsync(string mmfName, CancellationToken ct = default)
```

지정된 Editor 인스턴스에 연결한다. MMF를 열고, Header를 검증하고, 초기 Snapshot을 읽고, 백그라운드 모니터링을 시작한다.

| 매개변수 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `mmfName` | `string` | - | MMF 이름 (예: `"UEB_MyGame_12345"`). |
| `ct` | `CancellationToken` | `default` | 취소 토큰. |

**예외:**

| 예외 타입 | 조건 |
|---|---|
| `InvalidOperationException` | 이미 연결된 상태에서 호출한 경우. |
| `InvalidOperationException` | Major 버전 불일치 시. |

---

#### DisconnectAsync

```csharp
Task DisconnectAsync()
```

연결을 종료하고 모든 자원을 정리한다. 백그라운드 스레드를 중지하고 IPC 핸들을 해제한다.

---

#### RefreshSnapshotAsync

```csharp
Task<AssetSnapshot> RefreshSnapshotAsync(CancellationToken ct = default)
```

강제로 Snapshot을 다시 읽는다.

| 매개변수 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `ct` | `CancellationToken` | `default` | 취소 토큰. |

**반환값:** `Task<AssetSnapshot>` -- 갱신된 스냅샷.

### 이벤트

| 이벤트 | 타입 | 설명 |
|---|---|---|
| `SnapshotReceived` | `EventHandler<SnapshotReceivedEventArgs>?` | 새 Snapshot이 수신되었을 때 발생한다. 백그라운드 스레드에서 호출된다. |
| `EventReceived` | `EventHandler<AssetEventReceivedEventArgs>?` | 에셋 이벤트가 수신되었을 때 발생한다. 백그라운드 스레드에서 호출된다. |
| `ConnectionStateChanged` | `EventHandler<ConnectionStateChangedEventArgs>?` | 연결 상태가 변경되었을 때 발생한다. 백그라운드 스레드에서 호출된다. |
| `EventOverflow` | `EventHandler<EventOverflowEventArgs>?` | Ring Buffer 오버플로가 감지되었을 때 발생한다. |

---

## BridgeClientFactory

```csharp
public static class BridgeClientFactory
```

`IBridgeClient` 인스턴스를 생성하는 팩토리 클래스.

### 메서드

#### Create (매개변수 없음)

```csharp
public static IBridgeClient Create()
```

기본 옵션(`new BridgeClientOptions()`)으로 `IBridgeClient`를 생성한다.

**반환값:** `IBridgeClient` -- 새 클라이언트 인스턴스.

---

#### Create (옵션 지정)

```csharp
public static IBridgeClient Create(BridgeClientOptions options)
```

지정된 옵션으로 `IBridgeClient`를 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `options` | `BridgeClientOptions` | 클라이언트 옵션. |

**반환값:** `IBridgeClient` -- 새 클라이언트 인스턴스.

```csharp
// 기본 옵션으로 생성
IBridgeClient client = BridgeClientFactory.Create();

// 커스텀 옵션으로 생성
var options = new BridgeClientOptions
{
    HeartbeatCheckInterval = TimeSpan.FromSeconds(1),
    HeartbeatTimeout = TimeSpan.FromSeconds(3),
    EventPollInterval = TimeSpan.FromMilliseconds(500)
};
IBridgeClient customClient = BridgeClientFactory.Create(options);
```

---

## BridgeClientOptions

```csharp
public sealed class BridgeClientOptions
```

`IBridgeClient` 생성 시 사용하는 옵션 클래스.

### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `HeartbeatCheckInterval` | `TimeSpan` | `2초` | Heartbeat 체크 간격. Editor의 Heartbeat 필드를 주기적으로 확인하는 주기. |
| `HeartbeatTimeout` | `TimeSpan` | `5초` | Heartbeat 타임아웃. 이 시간 동안 Heartbeat이 갱신되지 않으면 `Lost` 상태로 전이한다. |
| `MaxLostDuration` | `TimeSpan` | `30초` | Lost 상태 최대 유지 시간. 초과 시 `Disconnected`로 전이한다. |
| `EventPollInterval` | `TimeSpan` | `1초` | 이벤트 폴링 간격 (Named Event 대기 타임아웃). |
| `AutoRefreshSnapshot` | `bool` | `true` | `SnapshotUpdated` 이벤트 수신 시 Snapshot을 자동으로 다시 읽을지 여부. |
| `AutoRecoverOnOverflow` | `bool` | `true` | Ring Buffer 오버플로 감지 시 자동으로 Snapshot을 재요청할지 여부. |

```csharp
var options = new BridgeClientOptions
{
    HeartbeatCheckInterval = TimeSpan.FromSeconds(1),
    HeartbeatTimeout       = TimeSpan.FromSeconds(3),
    MaxLostDuration        = TimeSpan.FromSeconds(60),
    EventPollInterval      = TimeSpan.FromMilliseconds(500),
    AutoRefreshSnapshot    = true,
    AutoRecoverOnOverflow  = false   // 수동으로 오버플로 처리
};
```

---

## ConnectionState

```csharp
public enum ConnectionState
```

Editor 연결 상태를 나타내는 열거형.

### 멤버

| 멤버 | 값 | 설명 |
|---|---|---|
| `Disconnected` | `0` | 연결되지 않은 초기 상태. |
| `Connecting` | `1` | 연결 시도 중. MMF를 열고 Header를 검증하는 단계. |
| `Connected` | `2` | 정상 연결 상태. Heartbeat이 주기적으로 확인됨. |
| `Lost` | `3` | Heartbeat 미수신. Editor 응답 없음 의심. `MaxLostDuration` 이내에 복구되지 않으면 `Disconnected`로 전이. |
| `VersionMismatch` | `4` | 프로토콜 버전 불일치로 연결 불가. |
| `Error` | `5` | 오류로 인한 연결 실패. |

### 상태 전이 다이어그램

```
Disconnected ──ConnectAsync()──> Connecting
Connecting   ──성공──────────> Connected
Connecting   ──버전 불일치──> VersionMismatch
Connecting   ──오류──────────> Error
Connected    ──Heartbeat 타임아웃──> Lost
Lost         ──Heartbeat 회복──────> Connected
Lost         ──MaxLostDuration 초과──> Disconnected
Connected    ──DisconnectAsync()──> Disconnected
Lost         ──DisconnectAsync()──> Disconnected
Connected    ──EditorShutdown──────> Disconnected
```

---

## EditorInstanceDiscovery

```csharp
public sealed class EditorInstanceDiscovery : IDisposable
```

Discovery MMF를 읽어 현재 실행 중인 UE Editor 인스턴스를 탐색한다. 폴링 기반(2초 간격)으로 인스턴스 목록 변경을 감시할 수 있다.

### 이벤트

| 이벤트 | 타입 | 설명 |
|---|---|---|
| `InstancesChanged` | `EventHandler<EventArgs>?` | 인스턴스 목록이 변경되었을 때 발생한다. |

### 메서드

#### GetActiveInstances

```csharp
public IReadOnlyList<EditorInstanceInfo> GetActiveInstances()
```

Discovery MMF에서 현재 활성 Editor 인스턴스 목록을 읽어 반환한다. Discovery MMF가 존재하지 않으면 빈 목록을 반환한다.

**반환값:** `IReadOnlyList<EditorInstanceInfo>` -- 활성 인스턴스 정보 목록. MMF 열기 실패 또는 매직 넘버 불일치 시 빈 배열 반환.

**동작 세부:**
1. `MemoryMappedFile.OpenExisting("UEB_Discovery")` 로 Discovery MMF을 연다.
2. Header의 매직 넘버(`0x55454244`)를 확인한다.
3. `EntryCount`만큼 엔트리를 순회하며 `ProcessId`가 0이 아닌 유효한 엔트리를 수집한다.
4. 각 엔트리에서 ProcessId, RegisteredAt, LastHeartbeat, ProjectName, MmfName을 파싱한다.

---

#### StartWatching

```csharp
public void StartWatching()
```

인스턴스 목록 변경 감시를 시작한다 (2초 간격 폴링). 기존 타이머가 있으면 재생성한다. 목록이 변경되면 `InstancesChanged` 이벤트를 발생시킨다.

---

#### StopWatching

```csharp
public void StopWatching()
```

인스턴스 목록 변경 감시를 중지한다. 내부 타이머를 해제한다.

---

#### Dispose

```csharp
public void Dispose()
```

리소스를 정리한다. 내부 감시 타이머를 해제한다.

```csharp
using var discovery = new EditorInstanceDiscovery();

// 한 번 조회
var instances = discovery.GetActiveInstances();
foreach (var inst in instances)
{
    Console.WriteLine($"{inst.ProjectName} (PID: {inst.ProcessId}) - {inst.MmfName}");
}

// 변경 감시
discovery.InstancesChanged += (s, e) =>
{
    var updated = discovery.GetActiveInstances();
    Console.WriteLine($"인스턴스 목록 변경: {updated.Count}개");
};
discovery.StartWatching();

// ... 감시 중 ...

discovery.StopWatching();
```

---

## EditorInstanceInfo

```csharp
public sealed class EditorInstanceInfo
```

Discovery MMF에서 읽은 개별 Editor 인스턴스 정보.

### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `ProcessId` | `uint` | - | Editor 프로세스 ID. `init` 접근자. |
| `ProjectName` | `string` | `string.Empty` | Unreal 프로젝트 이름. `init` 접근자. |
| `MmfName` | `string` | `string.Empty` | 이 인스턴스의 MMF 이름 (`IBridgeClient.ConnectAsync()` 호출 시 사용). `init` 접근자. |
| `RegisteredAt` | `DateTime` | - | 인스턴스 등록 시각 (UTC). `init` 접근자. |
| `LastHeartbeat` | `DateTime` | - | 마지막 Heartbeat 시각 (UTC). `init` 접근자. |
| `IsAlive` | `bool` | (계산) | 인스턴스가 활성 상태인지 확인. `LastHeartbeat`이 현재 UTC 시각 기준 5초 이내이면 `true`. 읽기 전용 계산 프로퍼티. |

### 메서드

#### ToString

```csharp
public override string ToString()
```

사용자 표시용 문자열을 반환한다. 형식: `"{ProjectName} (PID: {ProcessId})"`.

```csharp
var inst = new EditorInstanceInfo
{
    ProcessId = 12345,
    ProjectName = "MyGame",
    MmfName = "UEB_MyGame_12345",
    RegisteredAt = DateTime.UtcNow.AddMinutes(-10),
    LastHeartbeat = DateTime.UtcNow.AddSeconds(-2)
};

Console.WriteLine(inst);           // "MyGame (PID: 12345)"
Console.WriteLine(inst.IsAlive);   // true (2초 전 Heartbeat)
Console.WriteLine(inst.MmfName);   // "UEB_MyGame_12345"
```

---

## 모델 (Models)

### AssetSnapshot

```csharp
namespace UnrealEditorBridge.Adapter.Models

public sealed class AssetSnapshot
```

에셋 전체 상태 스냅샷. Snapshot 영역의 JSON 데이터를 역직렬화한 결과이다. 불변(immutable) 객체로 스레드 안전하게 공유할 수 있다.

#### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `Timestamp` | `DateTime` | - | Snapshot 생성 시각 (UTC). `init` 접근자. |
| `AssetCount` | `int` | - | 에셋 총 개수. `init` 접근자. |
| `Assets` | `IReadOnlyList<AssetInfo>` | `Array.Empty<AssetInfo>()` | 에셋 목록. `init` 접근자. |
| `Version` | `uint` | - | Snapshot 버전 카운터 (Header의 `SnapshotVersion` 값과 대응). `init` 접근자. |

```csharp
AssetSnapshot snapshot = await client.RefreshSnapshotAsync();

Console.WriteLine($"Snapshot v{snapshot.Version}");
Console.WriteLine($"시각: {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"에셋 수: {snapshot.AssetCount}");

foreach (var asset in snapshot.Assets)
{
    Console.WriteLine($"  {asset.AssetName} ({asset.ClassName})");
}
```

---

### AssetInfo

```csharp
namespace UnrealEditorBridge.Adapter.Models

public sealed class AssetInfo
```

개별 에셋의 메타데이터 정보. Snapshot JSON의 `assets` 배열 항목을 역직렬화한 결과이다.

#### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `ObjectPath` | `string` | `string.Empty` | 에셋의 전체 오브젝트 경로. 예: `"/Game/Characters/Hero/SK_Hero.SK_Hero"`. `init` 접근자. |
| `PackagePath` | `string` | `string.Empty` | 패키지 디렉토리 경로. 예: `"/Game/Characters/Hero"`. `init` 접근자. |
| `AssetName` | `string` | `string.Empty` | 에셋 이름. 예: `"SK_Hero"`. `init` 접근자. |
| `ClassName` | `string` | `string.Empty` | 에셋 클래스 경로. 예: `"/Script/Engine.SkeletalMesh"`. `init` 접근자. |
| `Tags` | `IReadOnlyDictionary<string, string>` | 빈 Dictionary | 에셋 태그 (키-값 쌍). `init` 접근자. |
| `Dependencies` | `AssetDependencyInfo` | `new()` | 에셋 의존성 정보. `init` 접근자. |

```csharp
AssetInfo asset = snapshot.Assets[0];

Console.WriteLine($"이름:     {asset.AssetName}");
Console.WriteLine($"클래스:   {asset.ClassName}");
Console.WriteLine($"경로:     {asset.ObjectPath}");
Console.WriteLine($"패키지:   {asset.PackagePath}");

// 태그 출력
foreach (var (key, value) in asset.Tags)
{
    Console.WriteLine($"  태그 [{key}] = {value}");
}

// 의존성 출력
Console.WriteLine($"하드 의존성: {asset.Dependencies.Hard.Count}개");
Console.WriteLine($"소프트 의존성: {asset.Dependencies.Soft.Count}개");
```

---

### AssetDependencyInfo

```csharp
namespace UnrealEditorBridge.Adapter.Models

public sealed class AssetDependencyInfo
```

에셋의 의존성(참조) 정보. 하드 레퍼런스와 소프트 레퍼런스 경로 목록을 포함한다.

#### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `Hard` | `IReadOnlyList<string>` | `Array.Empty<string>()` | 하드 레퍼런스 경로 배열. `init` 접근자. 이 에셋이 로드될 때 반드시 함께 로드되어야 하는 에셋 경로. |
| `Soft` | `IReadOnlyList<string>` | `Array.Empty<string>()` | 소프트 레퍼런스 경로 배열. `init` 접근자. 이 에셋이 선택적으로 참조하는 에셋 경로. |

```csharp
AssetDependencyInfo deps = asset.Dependencies;

Console.WriteLine("하드 의존성:");
foreach (string path in deps.Hard)
{
    Console.WriteLine($"  [Hard] {path}");
}

Console.WriteLine("소프트 의존성:");
foreach (string path in deps.Soft)
{
    Console.WriteLine($"  [Soft] {path}");
}
```

---

### AssetEvent

```csharp
namespace UnrealEditorBridge.Adapter.Models

public sealed class AssetEvent
```

Event Ring Buffer에서 읽은 개별 에셋 이벤트.

#### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `SequenceNumber` | `ulong` | - | 전역 시퀀스 번호 (monotonic 증가). `init` 접근자. |
| `Timestamp` | `DateTime` | - | 이벤트 발생 시각 (UTC). `init` 접근자. |
| `EventType` | `AssetEventType` | - | 이벤트 타입. `init` 접근자. |
| `ObjectPath` | `string` | `string.Empty` | 대상 에셋의 오브젝트 경로. `init` 접근자. |
| `AssetName` | `string?` | `null` | 에셋 이름. 이벤트 타입에 따라 `null`일 수 있다. `init` 접근자. |
| `ClassName` | `string?` | `null` | 에셋 클래스명. 이벤트 타입에 따라 `null`일 수 있다. `init` 접근자. |
| `OldObjectPath` | `string?` | `null` | 이전 오브젝트 경로. `AssetRenamed` / `AssetMoved` 이벤트에서만 유효하다. `init` 접근자. |
| `OldAssetName` | `string?` | `null` | 이전 에셋 이름. `AssetRenamed` 이벤트에서만 유효하다. `init` 접근자. |
| `RawPayloadJson` | `string` | `string.Empty` | 원본 JSON 페이로드 문자열. `init` 접근자. |

```csharp
// EventReceived 핸들러에서
void OnEventReceived(object? sender, AssetEventReceivedEventArgs e)
{
    AssetEvent evt = e.Event;

    Console.WriteLine($"[{evt.SequenceNumber}] {evt.EventType}");
    Console.WriteLine($"  경로: {evt.ObjectPath}");
    Console.WriteLine($"  시각: {evt.Timestamp:HH:mm:ss.fff}");

    if (evt.EventType == AssetEventType.AssetRenamed)
    {
        Console.WriteLine($"  이전 경로: {evt.OldObjectPath}");
        Console.WriteLine($"  이전 이름: {evt.OldAssetName}");
        Console.WriteLine($"  새 이름:   {evt.AssetName}");
    }

    // 원본 JSON이 필요하면
    Console.WriteLine($"  Raw JSON: {evt.RawPayloadJson}");
}
```

---

## 이벤트 인자 (EventArgs)

### SnapshotReceivedEventArgs

```csharp
namespace UnrealEditorBridge.Adapter.Events

public sealed class SnapshotReceivedEventArgs : EventArgs
```

새 Snapshot이 수신되었을 때의 이벤트 인자.

#### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `Snapshot` | `AssetSnapshot` | 수신된 에셋 스냅샷 (읽기 전용). |

#### 생성자

```csharp
public SnapshotReceivedEventArgs(AssetSnapshot snapshot)
```

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `snapshot` | `AssetSnapshot` | 수신된 스냅샷. |

---

### AssetEventReceivedEventArgs

```csharp
namespace UnrealEditorBridge.Adapter.Events

public sealed class AssetEventReceivedEventArgs : EventArgs
```

에셋 이벤트가 수신되었을 때의 이벤트 인자.

#### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `Event` | `AssetEvent` | 수신된 에셋 이벤트 (읽기 전용). |

#### 생성자

```csharp
public AssetEventReceivedEventArgs(AssetEvent evt)
```

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `evt` | `AssetEvent` | 수신된 에셋 이벤트. |

---

### ConnectionStateChangedEventArgs

```csharp
namespace UnrealEditorBridge.Adapter.Events

public sealed class ConnectionStateChangedEventArgs : EventArgs
```

연결 상태가 변경되었을 때의 이벤트 인자.

#### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `OldState` | `ConnectionState` | 이전 연결 상태 (읽기 전용). |
| `NewState` | `ConnectionState` | 새 연결 상태 (읽기 전용). |

#### 생성자

```csharp
public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState)
```

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `oldState` | `ConnectionState` | 이전 연결 상태. |
| `newState` | `ConnectionState` | 새 연결 상태. |

---

### EventOverflowEventArgs

```csharp
namespace UnrealEditorBridge.Adapter.Events

public sealed class EventOverflowEventArgs : EventArgs
```

Ring Buffer 오버플로가 감지되었을 때의 이벤트 인자.

#### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `MissedCount` | `ulong` | 누락된 이벤트 수 (추정, 읽기 전용). |

#### 생성자

```csharp
public EventOverflowEventArgs(ulong missedCount)
```

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `missedCount` | `ulong` | 누락된 이벤트 수. |

---

## 사용 예시

### 기본 연결 및 종료

```csharp
using UnrealEditorBridge.Adapter;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;

// 1. 클라이언트 생성
using IBridgeClient client = BridgeClientFactory.Create();

// 2. Editor 인스턴스 탐색
using var discovery = new EditorInstanceDiscovery();
var instances = discovery.GetActiveInstances();

if (instances.Count == 0)
{
    Console.WriteLine("실행 중인 Editor가 없습니다.");
    return;
}

var target = instances[0];
Console.WriteLine($"연결 대상: {target.ProjectName} (PID: {target.ProcessId})");

// 3. 연결
await client.ConnectAsync(target.MmfName);
Console.WriteLine($"연결 상태: {client.State}");

// 4. Snapshot 확인
if (client.CurrentSnapshot != null)
{
    Console.WriteLine($"에셋 수: {client.CurrentSnapshot.AssetCount}");
}

// 5. 종료
await client.DisconnectAsync();
```

---

### 이벤트 핸들링

```csharp
using IBridgeClient client = BridgeClientFactory.Create();

// Snapshot 수신 처리
client.SnapshotReceived += (sender, e) =>
{
    Console.WriteLine($"[Snapshot] v{e.Snapshot.Version}, " +
                      $"{e.Snapshot.AssetCount}개 에셋");
};

// 에셋 이벤트 수신 처리
client.EventReceived += (sender, e) =>
{
    var evt = e.Event;
    switch (evt.EventType)
    {
        case AssetEventType.AssetCreated:
            Console.WriteLine($"[생성] {evt.AssetName} ({evt.ClassName})");
            break;

        case AssetEventType.AssetDeleted:
            Console.WriteLine($"[삭제] {evt.ObjectPath}");
            break;

        case AssetEventType.AssetRenamed:
            Console.WriteLine($"[이름변경] {evt.OldAssetName} -> {evt.AssetName}");
            break;

        case AssetEventType.AssetSaved:
            Console.WriteLine($"[저장] {evt.AssetName}");
            break;

        case AssetEventType.AssetMoved:
            Console.WriteLine($"[이동] {evt.OldObjectPath} -> {evt.ObjectPath}");
            break;

        case AssetEventType.EditorShutdown:
            Console.WriteLine("[Editor 종료]");
            break;
    }
};

// 연결 상태 변경 처리
client.ConnectionStateChanged += (sender, e) =>
{
    Console.WriteLine($"[상태] {e.OldState} -> {e.NewState}");

    if (e.NewState == ConnectionState.Lost)
        Console.WriteLine("  Editor 응답 없음. 재연결 대기 중...");

    if (e.NewState == ConnectionState.Disconnected &&
        e.OldState == ConnectionState.Lost)
        Console.WriteLine("  연결 복구 실패. 종료합니다.");
};

// 오버플로 처리
client.EventOverflow += (sender, e) =>
{
    Console.WriteLine($"[오버플로] 약 {e.MissedCount}건 누락");
};

// 연결
using var discovery = new EditorInstanceDiscovery();
var instances = discovery.GetActiveInstances();
if (instances.Count > 0)
{
    await client.ConnectAsync(instances[0].MmfName);

    // 이벤트 수신 대기
    Console.WriteLine("이벤트 수신 중... Enter 키를 누르면 종료합니다.");
    Console.ReadLine();

    await client.DisconnectAsync();
}
```

---

### Snapshot 처리 및 필터링

```csharp
using IBridgeClient client = BridgeClientFactory.Create();

// ... 연결 후 ...

// 강제 Snapshot 새로고침
AssetSnapshot snapshot = await client.RefreshSnapshotAsync();

// 클래스별 에셋 그룹핑
var grouped = snapshot.Assets
    .GroupBy(a => a.ClassName)
    .OrderByDescending(g => g.Count());

foreach (var group in grouped)
{
    Console.WriteLine($"\n{group.Key}: {group.Count()}개");
    foreach (var asset in group.Take(5))
    {
        Console.WriteLine($"  {asset.AssetName}");
        Console.WriteLine($"    경로: {asset.ObjectPath}");

        // 의존성 확인
        if (asset.Dependencies.Hard.Count > 0)
        {
            Console.WriteLine($"    하드 의존성:");
            foreach (var dep in asset.Dependencies.Hard)
                Console.WriteLine($"      {dep}");
        }
    }
}

// 특정 경로 하위 에셋 검색
var characterAssets = snapshot.Assets
    .Where(a => a.PackagePath.StartsWith("/Game/Characters"))
    .ToList();

Console.WriteLine($"\nCharacters 폴더 에셋: {characterAssets.Count}개");
```

---

### 커스텀 옵션으로 고급 사용

```csharp
// 빠른 반응이 필요한 경우
var fastOptions = new BridgeClientOptions
{
    HeartbeatCheckInterval = TimeSpan.FromSeconds(1),
    HeartbeatTimeout       = TimeSpan.FromSeconds(3),
    MaxLostDuration        = TimeSpan.FromSeconds(10),
    EventPollInterval      = TimeSpan.FromMilliseconds(200),
    AutoRefreshSnapshot    = true,
    AutoRecoverOnOverflow  = true
};

using IBridgeClient client = BridgeClientFactory.Create(fastOptions);

// 수동 오버플로 처리
var manualOptions = new BridgeClientOptions
{
    AutoRecoverOnOverflow = false
};

using IBridgeClient manualClient = BridgeClientFactory.Create(manualOptions);
manualClient.EventOverflow += async (s, e) =>
{
    Console.WriteLine($"오버플로 감지: {e.MissedCount}건 누락");
    // 수동으로 Snapshot을 다시 읽어 상태 동기화
    var snapshot = await manualClient.RefreshSnapshotAsync();
    Console.WriteLine($"수동 동기화 완료: {snapshot.AssetCount}개 에셋");
};
```

---

### EditorInstanceDiscovery 감시 패턴

```csharp
using var discovery = new EditorInstanceDiscovery();

// 자동 연결 패턴: Editor가 실행될 때까지 대기
discovery.InstancesChanged += async (s, e) =>
{
    var list = discovery.GetActiveInstances();
    var alive = list.Where(i => i.IsAlive).ToList();

    Console.WriteLine($"활성 Editor: {alive.Count}개");
    foreach (var inst in alive)
    {
        Console.WriteLine($"  {inst.ProjectName} (PID:{inst.ProcessId})");
        Console.WriteLine($"    MMF: {inst.MmfName}");
        Console.WriteLine($"    등록: {inst.RegisteredAt:HH:mm:ss}");
        Console.WriteLine($"    Heartbeat: {inst.LastHeartbeat:HH:mm:ss}");
        Console.WriteLine($"    활성: {inst.IsAlive}");
    }
};

discovery.StartWatching();
Console.WriteLine("Editor 인스턴스 감시 중... Enter 키를 누르면 종료합니다.");
Console.ReadLine();
discovery.StopWatching();
```
