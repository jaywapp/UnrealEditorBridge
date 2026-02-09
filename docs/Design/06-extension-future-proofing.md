# Extension & Future-Proofing 설계

## 1. 개요

이 문서는 UnrealEditorBridge의 확장성과 하위 호환성 전략을 정의한다. 현재 설계는 단방향(Editor → 외부 툴) 데이터 흐름에 초점을 맞추었으나, 향후 양방향 통신, 다양한 클라이언트 지원, 프로토콜 진화를 대비한 확장 지점을 설계한다.

---

## 2. Request/Response 명령 채널 확장 방안

### 2.1 현재 한계

현재 설계는 **단방향(Editor → Client)** 통신만 지원한다:
- Editor가 Snapshot과 Event를 MMF에 기록
- Client가 MMF에서 읽기만 수행

향후 Client에서 Editor로 명령을 보내야 하는 사용 사례:
- 특정 에셋의 상세 정보 요청
- 에셋 열기/포커스 명령
- 에셋 태그 수정 요청
- 강제 Snapshot 재생성 요청
- 커스텀 Editor 명령 실행

### 2.2 Command Channel 설계

별도의 MMF를 사용하여 Client → Editor 방향의 명령 채널을 추가한다.

```
기존 구조 (유지):
  Editor ──[Snapshot/Event MMF]──▶ Client

확장 구조 (추가):
  Client ──[Command MMF]──▶ Editor
  Editor ──[Response MMF]──▶ Client
```

#### 2.2.1 Command MMF 레이아웃

```
이름: UEB_{ProjectName}_{PID}_Cmd
크기: 256 KB

┌──────────────────────────────────────────────┐
│  Command Header (64 bytes)                    │
│  ─────────────────────────                   │
│  0x00  uint32  Magic (0x55454243 = "UEBC")   │
│  0x04  uint32  Version                       │
│  0x08  uint32  CommandWriteIndex             │
│  0x0C  uint32  CommandSlotSize (4096 bytes)  │
│  0x10  uint32  CommandSlotCount (62)         │
│  0x14  44      Reserved                      │
├──────────────────────────────────────────────┤
│  Command Slots (Ring Buffer)                  │
│  ─────────────────────────                   │
│  Slot[0..61] (각 4096 bytes)                 │
└──────────────────────────────────────────────┘
```

#### 2.2.2 Command Record 구조

```
┌────────────────────────────────────┐
│  Command Slot (4096 bytes)          │
│                                     │
│  0x00  uint64  RequestId            │
│  0x08  int64   Timestamp            │
│  0x10  uint32  CommandType          │
│  0x14  uint32  PayloadSize          │
│  0x18  ~4072   Payload (JSON)       │
└────────────────────────────────────┘
```

#### 2.2.3 Response MMF 레이아웃

```
이름: UEB_{ProjectName}_{PID}_Rsp
크기: 1 MB

구조: Command MMF와 동일한 Ring Buffer 형태
각 Response 슬롯에 RequestId를 포함하여 요청-응답 매칭
```

#### 2.2.4 CommandType 열거값 (확장용)

| 값 | 이름 | 설명 |
|----|------|------|
| 1 | `RequestSnapshotRefresh` | Snapshot 즉시 재생성 요청 |
| 2 | `RequestAssetDetail` | 특정 에셋의 추가 상세 정보 요청 |
| 3 | `RequestFocusAsset` | Editor에서 특정 에셋에 포커스 |
| 4 | `RequestOpenAsset` | Editor에서 에셋 에디터 열기 |
| 5 | `RequestSetTags` | 에셋 태그 수정 요청 |
| 10 | `Ping` | 연결 확인용 핑 |

#### 2.2.5 구현 전략

Editor 측:
```cpp
// GameThread 타이머에서 Command MMF 폴링
bool UBridgeEditorSubsystem::OnCommandPollTick(float DeltaTime)
{
    FBridgeCommand Command;
    while (CommandReader->TryReadNext(Command))
    {
        ProcessCommand(Command);
    }
    return true;
}

void UBridgeEditorSubsystem::ProcessCommand(const FBridgeCommand& Command)
{
    FBridgeResponse Response;
    Response.RequestId = Command.RequestId;

    switch (Command.CommandType)
    {
    case EBridgeCommandType::RequestSnapshotRefresh:
        RebuildSnapshot();
        Response.Success = true;
        break;

    case EBridgeCommandType::RequestFocusAsset:
        // ... Editor API 호출
        break;
    }

    ResponseWriter->WriteResponse(Response);
}
```

Client(Adapter) 측:
```csharp
// IBridgeClient 확장
public interface IBridgeClient
{
    // 기존 API... (유지)

    // 확장: 명령 전송
    Task<BridgeResponse> SendCommandAsync(
        BridgeCommand command,
        CancellationToken ct = default);

    Task RequestSnapshotRefreshAsync(CancellationToken ct = default);
    Task RequestFocusAssetAsync(string objectPath, CancellationToken ct = default);
}
```

### 2.3 활성화 전략

Command Channel은 **옵트인(opt-in)** 방식으로 활성화한다:

1. Editor Plugin에 `bEnableCommandChannel` 설정 추가
2. 비활성 시 Command/Response MMF를 생성하지 않음
3. Client는 Command MMF 존재 여부로 지원 여부를 판단
4. 기존 단방향 기능은 Command Channel 없이도 완전히 동작

---

## 3. WPF 외 다른 클라이언트 지원 전략

### 3.1 현재 지원 구조

```
UnrealEditorBridge.Adapter (.NET)
    └── UnrealEditorBridge.Wpf (WPF 앱)
```

### 3.2 확장 가능한 클라이언트 유형

| 클라이언트 | 기술 | Adapter 사용 방식 |
|-----------|------|------------------|
| WPF 앱 | .NET + WPF | 직접 참조 |
| WinForms 앱 | .NET + WinForms | 직접 참조 |
| MAUI 앱 | .NET MAUI | 직접 참조 |
| Avalonia 앱 | .NET + Avalonia | 직접 참조 |
| 콘솔 도구 | .NET Console | 직접 참조 |
| ASP.NET 서비스 | .NET + ASP.NET | 직접 참조 |
| Python 스크립트 | Python | Protocol 직접 구현 또는 gRPC 게이트웨이 |
| Web 대시보드 | JavaScript | WebSocket 게이트웨이 경유 |
| VS Code 확장 | TypeScript | WebSocket 게이트웨이 경유 |

### 3.3 .NET 클라이언트 (직접 참조)

Adapter가 UI 프레임워크 비종속으로 설계되었으므로, 모든 .NET 기반 UI 프레임워크에서 직접 참조 가능하다.

```
[모든 .NET UI 프레임워크]
    │
    ▼
UnrealEditorBridge.Adapter
    │
    ▼
[MMF IPC]
```

각 UI 프레임워크별 필요 작업:
- UI 스레드 마샬링 래퍼 구현 (프레임워크마다 Dispatcher가 다름)
- ViewModel 레이어 구현
- View 구현

### 3.4 비-.NET 클라이언트를 위한 게이트웨이

비-.NET 클라이언트를 지원하기 위해 **Gateway 서비스**를 도입한다.

```
┌─────────────────────────────────────────────────────┐
│  UnrealEditorBridge.Gateway (.NET Console 서비스)    │
│                                                      │
│  ┌──────────────────────┐                            │
│  │ Adapter (IBridgeClient) │                         │
│  └──────────┬───────────┘                            │
│             │                                        │
│  ┌──────────▼───────────┐  ┌───────────────────┐    │
│  │  WebSocket Server    │  │  gRPC Server      │    │
│  │  (ws://localhost:    │  │  (localhost:       │    │
│  │   9810)              │  │   9811)            │    │
│  └──────────────────────┘  └───────────────────┘    │
└─────────────────────────────────────────────────────┘
         │                           │
         ▼                           ▼
  [Web 브라우저]              [Python/다른 언어]
  [VS Code 확장]              [gRPC 클라이언트]
```

#### Gateway WebSocket 프로토콜

```json
// 구독 메시지 (Client → Gateway)
{
    "type": "subscribe",
    "mmfName": "UEB_MyGame_12345",
    "events": ["snapshot", "assetEvent", "connectionState"]
}

// Snapshot 전달 (Gateway → Client)
{
    "type": "snapshot",
    "data": { /* AssetSnapshot JSON */ }
}

// 이벤트 전달 (Gateway → Client)
{
    "type": "assetEvent",
    "data": { /* AssetEvent JSON */ }
}

// 인스턴스 목록 (Gateway → Client)
{
    "type": "instances",
    "data": [ /* EditorInstanceInfo[] */ ]
}
```

### 3.5 NuGet 배포

Adapter를 NuGet 패키지로 배포하여 외부 개발자가 쉽게 사용할 수 있게 한다.

```xml
<!-- UnrealEditorBridge.Adapter.nuspec -->
<package>
    <metadata>
        <id>UnrealEditorBridge.Adapter</id>
        <version>1.0.0</version>
        <description>UE5 Editor와 외부 .NET 툴 간 IPC 브릿지 클라이언트 라이브러리</description>
        <tags>unreal-engine ipc bridge editor-tooling</tags>
    </metadata>
</package>
```

---

## 4. 프로토콜 확장 및 하위 호환 전략

### 4.1 확장 원칙

| 원칙 | 설명 |
|------|------|
| **추가 우선** | 기존 필드를 변경하지 않고 새 필드를 추가 |
| **기본값 안전** | 새 필드가 없을 때 안전한 기본값으로 동작 |
| **버전 게이트** | 호환 불가 변경은 Major 버전 증가로만 진행 |
| **Reserved 활용** | Header의 Reserved 영역을 새 필드 추가에 사용 |

### 4.2 Minor 버전 확장 예시

#### 예시: 에셋 썸네일 해시 추가 (v1.1)

Header에 새 필드 추가 (Reserved 영역 소비):

```
기존 Reserved 영역: 0xC8 ~ 0xFF (56 bytes)

v1.1 추가:
  0xC8  uint32  ThumbnailMmfEnabled  (0 = 비활성, 1 = 활성)
  0xCC  52      Reserved (축소)

Snapshot JSON 확장:
  assets[].thumbnailHash → 기존 Reader는 이 필드를 무시
```

Reader 측 하위 호환 처리:
```csharp
// v1.0 Reader가 v1.1 데이터를 읽을 때
// - Header의 ThumbnailMmfEnabled 필드는 Reserved 영역이므로 읽지 않음 → 무해
// - Snapshot JSON의 thumbnailHash 필드는 역직렬화 시 무시됨 → 무해

// v1.1 Reader가 v1.0 데이터를 읽을 때
// - ThumbnailMmfEnabled = 0 (Reserved가 0으로 초기화) → 비활성으로 처리
// - thumbnailHash 없음 → null 기본값 → 정상 동작
```

#### 예시: 새 EventType 추가 (v1.2)

```
v1.2 추가:
  EventType 9 = AssetDuplicated (에셋 복제)
  EventType 10 = AssetImported (에셋 임포트)

하위 호환:
  v1.0/v1.1 Reader는 알 수 없는 EventType을 무시하고 스킵
```

Reader 측 처리:
```csharp
// 알 수 없는 EventType 처리
if (!Enum.IsDefined(typeof(AssetEventType), eventTypeValue))
{
    // 로깅 후 스킵 (SequenceNumber는 정상 추적)
    _logger?.LogWarning("알 수 없는 EventType: {Type}", eventTypeValue);
    continue;
}
```

### 4.3 Major 버전 변경 전략

Major 버전 변경은 최소화하되, 불가피한 경우 다음 절차를 따른다:

#### 4.3.1 전환 기간 지원

```
[Editor v2.0]
    │
    ├─ v2.0 MMF 생성 (기본)
    │     이름: UEB_{ProjectName}_{PID}
    │
    └─ v1.x 호환 MMF 생성 (선택적, 설정으로 활성화)
          이름: UEB_{ProjectName}_{PID}_V1
          (v1.x 형식으로 데이터 기록)
```

#### 4.3.2 버전 협상 절차

1. Client가 MMF 열기
2. Magic 확인 → 유효
3. ProtocolVersion의 Major 확인
4. Major 불일치 시:
   - `_V{Major}` 접미사를 붙인 호환 MMF 검색
   - 호환 MMF 존재 시 해당 MMF로 연결
   - 없으면 `VersionMismatch` 오류

### 4.4 데이터 모델 확장 전략

#### JSON 직렬화의 확장성

JSON을 사용하므로 필드 추가가 자연스럽게 하위 호환된다:

```json
// v1.0 Snapshot
{
    "assets": [{
        "objectPath": "...",
        "assetName": "...",
        "className": "..."
    }]
}

// v1.3 Snapshot (확장)
{
    "assets": [{
        "objectPath": "...",
        "assetName": "...",
        "className": "...",
        "fileSize": 102400,        // 신규 필드
        "lastModified": "...",     // 신규 필드
        "customMetadata": {}       // 신규 필드
    }],
    "projectInfo": {               // 신규 섹션
        "engineVersion": "5.4",
        "projectVersion": "1.0"
    }
}
```

역직렬화 측:
- `System.Text.Json`: 기본적으로 알 수 없는 속성을 무시
- C++ `FJsonObject`: `TryGetField`가 없는 필드에 대해 `nullptr` 반환
- 양측 모두 추가 코드 없이 새 필드를 안전하게 무시

### 4.5 성능 확장 경로

향후 성능이 병목이 될 경우의 확장 경로:

| 현재 | 확장 옵션 | 전환 조건 |
|------|-----------|-----------|
| JSON 직렬화 | MessagePack / FlatBuffers | Snapshot > 2MB, 파싱 시간 > 100ms |
| 단일 MMF | Snapshot/Event 별도 MMF | MMF 크기 > 32MB |
| 폴링 기반 Discovery | Named Pipe 기반 알림 | 인스턴스 > 10개 |
| CRC32 체크섬 | xxHash3 | 체크섬 계산이 병목일 때 |

전환 시 Major 버전 증가가 필요하며, 기존 프로토콜과의 호환 MMF를 병행 지원한다.

### 4.6 플러그인 확장 지점

Editor Plugin에 확장 포인트를 제공하여 사용자가 커스텀 데이터를 추가할 수 있게 한다:

```cpp
// 확장 인터페이스 (향후 구현)
class IBridgeDataProvider
{
public:
    virtual ~IBridgeDataProvider() = default;

    // Snapshot에 추가 데이터를 기여
    virtual void ContributeToSnapshot(TSharedRef<FJsonObject> SnapshotJson) = 0;

    // 커스텀 이벤트를 발행
    virtual void OnTick(float DeltaTime, FBridgeIpcWriter& Writer) = 0;
};

// 등록
void UBridgeEditorSubsystem::RegisterDataProvider(
    TSharedRef<IBridgeDataProvider> Provider);
```

이를 통해 다른 Editor Plugin이 UnrealEditorBridge를 활용하여 자신의 데이터를 외부에 노출할 수 있다.

---

## 5. 확장 로드맵

| 단계 | 내용 | 프로토콜 영향 |
|------|------|--------------|
| **v1.0** | 단방향 Snapshot + Event Stream | 초기 버전 |
| **v1.1** | 에셋 썸네일 해시, 파일 크기 정보 | Minor 확장 |
| **v1.2** | 새 EventType 추가 (Duplicate, Import) | Minor 확장 |
| **v1.3** | 프로젝트 메타데이터, Engine 버전 정보 | Minor 확장 |
| **v2.0** | Command/Response 양방향 통신 | Major 변경 (별도 MMF) |
| **v2.1** | Gateway 서비스 표준화 | Adapter 확장 (Protocol 무관) |
| **v3.0** | 바이너리 직렬화 전환 (성능 최적화) | Major 변경 |
