# UnrealEditorBridge - 프로젝트 인수인계 문서

> 작성일: 2026-02-09
> 프로토콜 버전: v1.0 (1000)
> 상태: v1.0 구현 완료, 통합 테스트 성공

---

## 1. 프로젝트 개요

**UnrealEditorBridge**는 Unreal Engine 5 Editor와 외부 .NET/WPF 기반 도구를 연결하는 재사용 가능한 IPC(Inter-Process Communication) 브릿지 시스템이다.

### 1.1 핵심 개념

- **방향**: 단방향 (Editor -> 외부 도구). Editor가 데이터 생산자(Producer), .NET 앱이 소비자(Consumer).
- **IPC 방식**: Memory-Mapped File(MMF) + Named Mutex(동기화) + Named Event(알림)
- **직렬화 형식**: JSON (UTF-8). Snapshot과 Event 모두 JSON 페이로드로 전달.
- **데이터 구조**: Snapshot(전체 에셋 목록) + Event Ring Buffer(실시간 변경 이벤트) + Discovery(인스턴스 탐색)

### 1.2 프로젝트 구성 (4개 프로젝트)

| 프로젝트 | 타겟 프레임워크 | 역할 |
|---------|---------------|------|
| `UnrealEditorBridge.Protocol` | .NET 8.0 | 프로토콜 상수, 바이너리 레이아웃 오프셋, 파서 |
| `UnrealEditorBridge.Adapter` | .NET 8.0-windows | MMF 리더, 연결 관리, Heartbeat 모니터, 이벤트 처리 |
| `UnrealEditorBridge.Wpf` | .NET 8.0-windows | WPF MVVM UI (에셋 목록, 이벤트 로그, 연결 관리) |
| UE5 Plugin (C++) | Win64 | UE5 Editor 전용 플러그인 (Snapshot/Event 기록, Discovery 등록) |

### 1.3 솔루션 파일

```
D:\workspace\UnrealEditorBridge\UnrealEditorBridge.sln
```

Visual Studio 2022에서 .sln을 열면 Protocol, Adapter, Wpf 세 .NET 프로젝트가 로드된다. UE5 플러그인은 별도로 Unreal Build System으로 빌드한다.

---

## 2. 프로젝트 구조

```
D:\workspace\UnrealEditorBridge\
|
+-- UnrealEditorBridge.sln              .NET 솔루션 파일
|
+-- src/
|   +-- UnrealEditorBridge.Protocol/    프로토콜 상수, 레이아웃, 파서
|   |   +-- ProtocolConstants.cs        매직넘버, 크기, IPC 이름 빌더
|   |   +-- HeaderLayout.cs             MMF Header 필드 오프셋
|   |   +-- DiscoveryLayout.cs          Discovery MMF 필드 오프셋
|   |   +-- EventSlotLayout.cs          Event Ring Buffer 슬롯 오프셋
|   |   +-- HeaderData.cs               Header 데이터 구조체
|   |   +-- HeaderParser.cs             Header 바이트 파싱
|   |   +-- EventRecordParser.cs        Event 슬롯 바이트 파싱
|   |   +-- Crc32.cs                    CRC32 체크섬 계산
|   |   +-- AssetEventType.cs           이벤트 유형 열거
|   |
|   +-- UnrealEditorBridge.Adapter/     MMF 리더, 연결 관리, 이벤트 처리
|   |   +-- IBridgeClient.cs            퍼블릭 API 인터페이스
|   |   +-- BridgeClientFactory.cs      클라이언트 팩토리
|   |   +-- BridgeClientOptions.cs      클라이언트 옵션
|   |   +-- ConnectionState.cs          연결 상태 열거
|   |   +-- EditorInstanceDiscovery.cs  Discovery MMF에서 인스턴스 탐색
|   |   +-- EditorInstanceInfo.cs       인스턴스 정보 DTO
|   |   +-- Events/                     이벤트 인자 클래스들
|   |   +-- Internal/
|   |   |   +-- BridgeClient.cs         IBridgeClient 핵심 구현체
|   |   |   +-- Connection/
|   |   |   |   +-- ConnectionStateMachine.cs  연결 상태 FSM
|   |   |   |   +-- HeartbeatMonitor.cs        Heartbeat 감시
|   |   |   +-- Ipc/
|   |   |   |   +-- MmfAccessor.cs      MMF 열기/읽기/쓰기
|   |   |   |   +-- HeaderReader.cs     Header 영역 읽기
|   |   |   |   +-- SnapshotReader.cs   Snapshot 영역 읽기
|   |   |   |   +-- EventRingReader.cs  Event Ring Buffer 읽기
|   |   |   +-- Serialization/
|   |   |       +-- JsonSnapshotDeserializer.cs  Snapshot JSON 역직렬화
|   |   |       +-- JsonEventDeserializer.cs     Event JSON 역직렬화
|   |   +-- Models/                     데이터 모델 (AssetInfo, AssetEvent 등)
|   |
|   +-- UnrealEditorBridge.Wpf/         WPF MVVM UI
|       +-- App.xaml.cs                 DI 컨테이너 구성 (Unity Container)
|       +-- Services/
|       |   +-- IBridgeService.cs       UI 서비스 인터페이스
|       |   +-- BridgeService.cs        Dispatcher 마샬링 래퍼
|       +-- ViewModels/
|       |   +-- MainViewModel.cs        전체 화면 조율
|       |   +-- ConnectionViewModel.cs  연결/Discovery UI
|       |   +-- AssetListViewModel.cs   에셋 목록/필터링
|       |   +-- AssetDetailViewModel.cs 에셋 상세 정보
|       |   +-- AssetItemViewModel.cs   에셋 항목 래퍼
|       |   +-- EventLogViewModel.cs    이벤트 로그
|       +-- Views/                      XAML 뷰
|       +-- Converters/                 WPF 값 변환기
|       +-- Models/                     UI 전용 모델
|
+-- ue5-plugin/
|   +-- UnrealEditorBridge/
|       +-- UnrealEditorBridge.uplugin   플러그인 디스크립터 (Editor, PostEngineInit, Win64)
|       +-- Source/UnrealEditorBridge/
|           +-- UnrealEditorBridge.Build.cs  모듈 빌드 규칙
|           +-- Public/
|           |   +-- BridgeEditorSubsystem.h  UEditorSubsystem (라이프사이클 관리)
|           |   +-- BridgeAssetCollector.h   에셋 수집기
|           |   +-- BridgeEventListener.h    Editor 이벤트 리스너
|           |   +-- BridgeTypes.h            C++ 데이터 구조체
|           |   +-- UnrealEditorBridgeModule.h  모듈 정의
|           +-- Private/
|               +-- BridgeEditorSubsystem.cpp
|               +-- BridgeAssetCollector.cpp   태그 필터링 포함 (1024자 제한)
|               +-- BridgeEventListener.cpp
|               +-- UnrealEditorBridgeModule.cpp
|               +-- Ipc/
|               |   +-- BridgeMmfManager.h/cpp    MMF/Mutex/Event 생성 관리
|               |   +-- BridgeIpcWriter.h/cpp      MMF에 데이터 기록
|               |   +-- BridgeDiscoveryRegistrar.h/cpp  Discovery 등록/해제
|               |   +-- HeaderLayout.h             C++ 측 필드 오프셋
|               |   +-- ProtocolConstants.h         C++ 측 프로토콜 상수
|               +-- Serialization/
|                   +-- BridgeJsonSerializer.h/cpp  JSON 직렬화 (Snapshot, Event)
|
+-- docs/
    +-- Design/
    |   +-- 01-architecture-overview.md     아키텍처 개요
    |   +-- 02-protocol-design.md           프로토콜 설계
    |   +-- 03-adapter-design.md            Adapter 설계
    |   +-- 04-wpf-design.md                WPF 설계
    |   +-- 05-ue5-plugin-design.md         UE5 플러그인 설계
    |   +-- 06-extension-future-proofing.md 확장/하위호환 설계 + 로드맵
    +-- Guide/                              (빈 디렉토리)
    +-- Reference/                          (빈 디렉토리)
    +-- Handover/
        +-- Handover.md                     이 문서
```

---

## 3. 현재 상태 (2026-02-09 기준)

### 3.1 구현 완료 상태

**v1.0 구현 완료, 통합 테스트 성공.** 아래 모든 기능이 UE5 Editor와 WPF 앱 간에 정상 동작하는 것을 확인하였다.

| 기능 | 상태 | 비고 |
|------|------|------|
| **Snapshot** | 정상 | 3,817개 에셋 JSON 역직렬화 성공 |
| **Event Stream** | 정상 | 에셋 추가/삭제/이름변경/저장 이벤트 실시간 수신 |
| **Discovery** | 정상 | Discovery MMF에서 Editor 인스턴스 목록 탐색 |
| **Heartbeat** | 정상 | 연결 상태(Connected/Lost/Disconnected) 모니터링 |
| **CRC32 검증** | 정상 | Snapshot 무결성 체크 |
| **Resilient 파싱** | 정상 | 일부 에셋 JSON 오류 시 폴백 파싱으로 복구 |

### 3.2 프로토콜 버전

- 프로토콜 버전: `1000` (Major 1, Minor 0)
- Header Magic: `0x55454221` ("UEB!")
- Discovery Magic: `0x55454244` ("UEBD")

### 3.3 MMF 메모리 레이아웃

```
인스턴스별 MMF (이름: UEB_{ProjectName}_{PID}, 총 16 MB)
+--------------------------------------------------+
| Header (256 bytes, offset 0x000)                  |
|   0x00  uint32  Magic (0x55454221)                |
|   0x04  uint32  ProtocolVersion (1000)            |
|   0x08  uint32  WriterPid                         |
|   0x10  int64   Heartbeat (UTC Ticks)             |
|   0x18  uint32  SnapshotVersion                   |
|   0x1C  uint32  SnapshotSize                      |
|   0x20  uint32  SnapshotCrc32                     |
|   0x28  uint32  EventWriteIndex                   |
|   0x30  uint64  EventSequenceNumber               |
+--------------------------------------------------+
| Snapshot (4 MB, offset 0x100)                     |
|   JSON (UTF-8) 페이로드                           |
+--------------------------------------------------+
| Event Ring Buffer (슬롯 2048B x 6144개)           |
|   offset 0x400100부터 시작                         |
|   각 슬롯:                                        |
|     0x00  uint64  Sequence                        |
|     0x08  uint32  EventType                       |
|     0x0C  uint32  PayloadSize                     |
|     0x10  uint32  PayloadCrc32                    |
|     0x18  ~2024B  Payload (JSON)                  |
+--------------------------------------------------+

Discovery MMF (이름: UEB_Discovery, 약 64 KB)
+--------------------------------------------------+
| Discovery Header (64 bytes)                       |
|   0x00  uint32  Magic (0x55454244)                |
|   0x04  uint32  EntryCount                        |
+--------------------------------------------------+
| Entry[0..127] (각 512 bytes)                      |
|   0x00  uint32  ProcessId                         |
|   0x08  int64   RegisteredAt (UTC Ticks)          |
|   0x10  int64   LastHeartbeat (UTC Ticks)         |
|   0x20  char[128]  ProjectName (UTF-8)            |
|   0xA0  char[256]  MmfName (UTF-8)               |
+--------------------------------------------------+
```

### 3.4 IPC 객체 네이밍 규칙

| 객체 | 이름 패턴 | 예시 |
|------|----------|------|
| MMF | `UEB_{ProjectName}_{PID}` | `UEB_Sample_12345` |
| Mutex | `UEB_{ProjectName}_{PID}_Mtx` | `UEB_Sample_12345_Mtx` |
| Snapshot Event | `UEB_{ProjectName}_{PID}_SnapshotEvt` | `UEB_Sample_12345_SnapshotEvt` |
| Stream Event | `UEB_{ProjectName}_{PID}_StreamEvt` | `UEB_Sample_12345_StreamEvt` |
| Discovery MMF | `UEB_Discovery` | (고정) |

---

## 4. 해결된 주요 이슈

개발 과정에서 해결한 핵심 문제들을 기록한다. 같은 문제 재발 시 참고할 것.

### 4.1 C++ 빌드 오류 4종

| 오류 | 원인 | 해결 |
|------|------|------|
| `TUniquePtr` incomplete type | forward declaration만 있고 full #include 없음 | UCLASS 헤더에서 TUniquePtr로 사용하는 타입의 full #include 추가 |
| `class/struct FAssetData` 불일치 | UE5에서 FAssetData는 struct인데 class로 forward declare | `struct FAssetData;`로 수정 |
| `FObjectPostSaveContext` 미정의 | 해당 타입의 헤더 누락 | 필요한 `#include` 추가 |
| `FALSE` 매크로 미정의 | Windows 매크로인데 UE5 빌드 환경에서 정의 안 됨 | `0`으로 교체 |

**교훈**: UE5 UHT(Unreal Header Tool)가 `.gen.cpp`를 생성하므로, `UCLASS`/`USTRUCT` 헤더에서 `TUniquePtr<T>`를 멤버로 선언할 때 `T`의 full `#include`가 반드시 필요하다. forward declaration으로는 부족하다.

### 4.2 Discovery 바이너리 레이아웃 불일치

C++과 C# 간 Discovery MMF의 바이너리 레이아웃이 불일치하여 인스턴스 탐색이 실패하였다. 양측 10개 파일에 걸쳐 다음 값들을 일괄 수정하였다:

- Magic 값
- HeaderSize (Discovery)
- DiscoveryEntrySize
- 모든 필드의 절대 오프셋 (ProcessId, RegisteredAt, LastHeartbeat, ProjectName, MmfName)

**교훈**: C++(`ProtocolConstants.h`, `HeaderLayout.h`)과 C#(`ProtocolConstants.cs`, `HeaderLayout.cs`, `DiscoveryLayout.cs`)의 바이너리 레이아웃 상수는 반드시 1:1로 일치해야 한다. 한쪽을 변경하면 반대쪽도 동시에 변경해야 한다.

### 4.3 JSON 역직렬화 실패 (대용량 태그)

UE5 AssetRegistry의 일부 에셋 태그(특히 `FiBData`)에 대용량 바이너리 데이터가 포함되어 있었다. 이 데이터가 JSON 이스케이프 과정에서 실패하거나 Snapshot 용량을 초과하는 문제가 발생하였다.

**해결 (양측)**:
- **C++ 측**: `BridgeAssetCollector.cpp`에서 태그 값 길이가 1,024자를 초과하면 해당 태그를 건너뛰도록 필터링 추가
- **C# 측**: `JsonSnapshotDeserializer.cs`에서 1차 고속 역직렬화(`JsonSerializer.Deserialize`)가 실패하면 `JsonDocument` 기반 개별 에셋 단위 resilient 파싱으로 폴백. 개별 에셋이나 태그가 파싱 실패해도 나머지를 정상 복구

### 4.4 초기 Snapshot 미전파

`BridgeClient.ConnectAsync()`에서 초기 Snapshot을 읽었지만 `SnapshotReceived` 이벤트를 발행하지 않았다. 이로 인해 WPF 앱에서 연결 직후 에셋 목록이 비어 보이는 문제가 있었다.

**해결**: `ConnectAsync()`에서 `ConnectionState.Connected` 전이 후 초기 Snapshot에 대한 `SnapshotReceived` 이벤트를 명시적으로 발행하도록 수정하였다.

해당 코드 위치: `src/UnrealEditorBridge.Adapter/Internal/BridgeClient.cs` (150~157행)

```csharp
_stateMachine.TransitionTo(ConnectionState.Connected);

// 초기 Snapshot 이벤트 발행
var initialSnapshot = _currentSnapshot;
if (initialSnapshot != null)
{
    SnapshotReceived?.Invoke(this, new SnapshotReceivedEventArgs(initialSnapshot));
}
```

### 4.5 네임스페이스 스타일 전환

프로젝트 전체(51개 .cs 파일)에서 C# file-scoped namespace(`namespace X;`)를 block-scoped namespace(`namespace X { ... }`)로 전환하였다.

---

## 5. 알려진 제약사항 / 개선 필요 사항

현재 v1.0에서 알려진 제약사항과 잠재적 개선 포인트를 정리한다.

### 5.1 UE5 플러그인 소스 이중 존재

UE5 플러그인 소스 코드가 두 곳에 존재할 수 있다:
- `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\` (이 저장소의 원본)
- `D:\UnrealEngine\Sample\Plugins\UnrealEditorBridge\` (실제 UE5 프로젝트에 배치된 복사본)

양쪽의 동기화가 수동으로 이루어지므로, 한쪽만 수정하고 다른 쪽에 반영하지 않을 위험이 있다. 향후 심볼릭 링크 또는 빌드 스크립트를 통한 자동 동기화를 권장한다.

### 5.2 에러 로깅 미구현

현재 Adapter의 백그라운드 루프(`EventReaderLoop`, `SnapshotWatcherLoop`)에서 예외를 `catch { }` 블록으로 삼키고 있다. 로깅 프레임워크가 도입되지 않아 디버깅이 어렵다.

해당 코드 위치: `src/UnrealEditorBridge.Adapter/Internal/BridgeClient.cs` (288~291행, 316행)

```csharp
catch { /* 예외 삼킴, 루프 계속 */ }
```

### 5.3 단위 테스트 미작성

Protocol 프로젝트(HeaderParser, EventRecordParser, Crc32)와 Adapter 프로젝트(SnapshotReader, EventRingReader, ConnectionStateMachine)에 대한 단위 테스트가 전혀 없다.

### 5.4 Event Ring Reader의 overflow 감지 한계

overflow 감지가 sequence number gap 기반으로만 동작한다. Writer가 Reader보다 한 바퀴 이상 앞서 쓸 경우, 데이터 손상 없이 감지되지만 정확한 손실 수를 계산하기 어렵다.

### 5.5 WPF 앱 설정 저장/로드 미구현

WPF 앱에 마지막 접속 인스턴스 정보를 저장하는 기능이 없다. 앱을 실행할 때마다 Discovery에서 수동으로 인스턴스를 선택해야 한다.

### 5.6 SnapshotReader의 비원자적 읽기

`SnapshotReader`에서 Header를 먼저 읽고 Snapshot 데이터를 나중에 읽는 구조이므로, Header를 읽은 시점과 Snapshot 데이터를 읽는 시점 사이에 Writer가 Snapshot을 갱신하면 stale header를 기반으로 잘못된 크기/CRC의 데이터를 읽을 수 있다. 현재는 CRC32 불일치 시 재시도하는 방식(`ReadSnapshotWithRetry`)으로 회피하고 있다.

---

## 6. 다음 작업 제안

우선순위 순으로 정리한다.

### 6.1 단위 테스트 작성 (높음)

테스트 프로젝트 `UnrealEditorBridge.Protocol.Tests`, `UnrealEditorBridge.Adapter.Tests`를 생성하고 다음을 테스트한다:

- `HeaderParser`: 바이트 배열에서 Header 필드 정확히 파싱하는지
- `EventRecordParser`: 이벤트 슬롯 파싱 및 CRC 검증
- `Crc32`: 알려진 입력에 대한 CRC32 값 검증
- `DiscoveryLayout.GetEntryAbsoluteOffset`: 엔트리 인덱스별 오프셋 계산 검증
- `ProtocolConstants.BuildMmfName`, `SanitizeProjectName`: IPC 이름 생성 규칙 검증
- `ConnectionStateMachine`: 상태 전이 규칙 검증
- `JsonSnapshotDeserializer`: 정상 JSON 및 깨진 JSON에 대한 resilient 파싱 검증

### 6.2 로깅 프레임워크 도입 (높음)

Serilog 또는 NLog를 도입하여 Adapter 내부의 예외, 연결 상태 변화, 성능 지표 등을 기록한다. 현재 모든 `catch { }` 블록에 로깅을 추가해야 한다.

### 6.3 자동 연결 기능 (중간)

WPF 앱에서 마지막 접속 인스턴스(MMF 이름)를 로컬 설정 파일에 저장하고, 앱 재시작 시 해당 인스턴스로 자동 연결을 시도하는 기능을 추가한다.

### 6.4 Git 저장소 초기화 (높음)

현재 Git 저장소가 초기화되어 있지 않다. 다음 순서로 진행한다:

1. `.gitignore` 작성 (`bin/`, `obj/`, `.vs/`, `Intermediate/`, `Binaries/`, `*.user` 등)
2. `git init`
3. 초기 커밋

### 6.5 CI/CD 파이프라인 구성 (중간)

GitHub Actions 또는 Azure DevOps Pipeline을 구성하여 .NET 프로젝트의 빌드 및 테스트를 자동화한다.

### 6.6 확장 로드맵 참고

`docs/Design/06-extension-future-proofing.md`에 v1.1~v3.0까지의 확장 로드맵이 정리되어 있다. 주요 확장 방향:

| 버전 | 내용 |
|------|------|
| v1.1 | 에셋 썸네일 해시, 파일 크기 정보 추가 |
| v1.2 | 새 EventType 추가 (Duplicate, Import) |
| v1.3 | 프로젝트 메타데이터, Engine 버전 정보 |
| v2.0 | Command/Response 양방향 통신 (별도 MMF) |
| v2.1 | Gateway 서비스 (WebSocket/gRPC, 비-.NET 클라이언트 지원) |
| v3.0 | 바이너리 직렬화 전환 (MessagePack/FlatBuffers) |

---

## 7. 핵심 기술 포인트 (다음 작업자를 위한 팁)

### 7.1 C++/C# 바이너리 레이아웃 동기화

**가장 중요한 규칙**: C++과 C#의 바이너리 레이아웃 상수가 반드시 1:1로 일치해야 한다.

수정 시 반드시 함께 확인해야 할 파일 쌍:

| C++ (ue5-plugin) | C# (src/Protocol) |
|-------------------|--------------------|
| `ProtocolConstants.h` | `ProtocolConstants.cs` |
| `HeaderLayout.h` (Header 섹션) | `HeaderLayout.cs` |
| `HeaderLayout.h` (Event Slot 섹션) | `EventSlotLayout.cs` |
| `HeaderLayout.h` (Discovery 섹션) | `DiscoveryLayout.cs` |

한쪽만 수정하면 IPC 통신이 완전히 깨진다. 오프셋, 크기, 매직넘버 모두 바이트 단위로 일치해야 한다.

### 7.2 UE5 UHT와 TUniquePtr

`UCLASS` 또는 `USTRUCT` 헤더에서 `TUniquePtr<T>`를 멤버로 선언할 때, `T`의 forward declaration만으로는 UHT가 생성하는 `.gen.cpp`에서 컴파일 오류가 발생한다. **반드시 full `#include`를 사용해야 한다.**

예시 (`BridgeEditorSubsystem.h`):
```cpp
// forward declaration만으로는 부족
// class FBridgeMmfManager;  // 이렇게 하면 빌드 실패

// full include 필요
#include "Ipc/BridgeMmfManager.h"

UCLASS()
class UBridgeEditorSubsystem : public UEditorSubsystem
{
    TUniquePtr<FBridgeMmfManager> MmfManager;  // OK
};
```

### 7.3 AssetRegistry 대용량 태그 주의

UE5 AssetRegistry의 에셋 태그 중 `FiBData`(FBX Import Data) 등은 수십 KB의 바이너리 데이터를 포함할 수 있다. 이 데이터를 JSON 문자열로 이스케이프하면:
- JSON 파싱 실패 가능 (이스케이프 불가 문자)
- Snapshot 크기 초과 (4MB 제한)
- 직렬화 성능 저하

현재 해결책: C++ 측에서 1,024자 초과 태그를 필터링 (`BridgeAssetCollector.cpp:36~45행`).

### 7.4 WPF UI 스레드 마샬링

Adapter의 `BridgeClient`는 백그라운드 스레드에서 이벤트를 발생시킨다. WPF에서 이를 UI에 반영하려면 `Dispatcher.BeginInvoke`로 UI 스레드에 마샬링해야 한다.

현재 이 역할은 `BridgeService`(`src/UnrealEditorBridge.Wpf/Services/BridgeService.cs`)가 담당한다:

```csharp
_client.SnapshotReceived += (s, e) =>
    _dispatcher.BeginInvoke(() => SnapshotReceived?.Invoke(this, e));
```

새로운 이벤트를 `IBridgeClient`에 추가할 때, `BridgeService`에도 동일한 마샬링 래퍼를 추가해야 한다.

### 7.5 NuGet 패키지 버전

WPF 프로젝트에서 사용하는 NuGet 패키지 버전:

| 패키지 | 버전 | 비고 |
|--------|------|------|
| `Unity.Container` | 5.11.11 | DI 컨테이너 |
| `Prism.Core` | 9.0.537 | MVVM (BindableBase, DelegateCommand) |
| `ReactiveUI` | 20.1.1 | 리액티브 UI 바인딩 |
| `ReactiveUI.WPF` | 20.1.1 | WPF 전용 바인딩 (`NU1701` 경고 억제) |

이 버전들은 테스트 완료된 조합이다. 업그레이드 시 호환성을 확인할 것.

### 7.6 UE5 플러그인 빌드 방법

UE5 플러그인은 Unreal Build System으로 빌드한다. **플러그인 이름이 아닌 에디터 타겟을 지정해야 한다.**

```batch
"D:\Epic Games\UE_5.7\Engine\Build\BatchFiles\Build.bat" ^
    SampleEditor ^
    Win64 ^
    Development ^
    -Project="D:\UnrealEngine\Sample\Sample.uproject"
```

또는 UE5 Editor에서 프로젝트를 열면 플러그인이 자동으로 컴파일된다.

### 7.7 UE5 플러그인 모듈 의존성

`UnrealEditorBridge.Build.cs`에 정의된 의존 모듈:

```
Public:  Core, CoreUObject, Engine, AssetRegistry, UnrealEd, EditorSubsystem
Private: Json, JsonUtilities, Projects
```

`UnrealEd`와 `EditorSubsystem`에 의존하므로 런타임(게임) 빌드에는 포함될 수 없다. `.uplugin`에서 `"Type": "Editor"`로 제한되어 있다.

### 7.8 Snapshot 재생성 임계값

Editor 측에서 이벤트가 100개 누적되면 자동으로 전체 Snapshot을 재생성한다.

```cpp
// BridgeEditorSubsystem.h
static constexpr int32 SnapshotRebuildThreshold = 100;
```

이는 Ring Buffer overflow 전에 전체 상태를 동기화하기 위한 안전장치이다.

---

## 8. 개발 환경

### 8.1 필수 소프트웨어

| 항목 | 버전 | 경로/비고 |
|------|------|----------|
| OS | Windows 10 또는 11 | MMF/Named Mutex는 Windows 전용 API |
| .NET SDK | 8.0 이상 | `dotnet --version`으로 확인 |
| Visual Studio | 2022 | .NET 8 + WPF 개발 워크로드 필요 |
| Unreal Engine | 5.7 | `D:\Epic Games\UE_5.7` |

### 8.2 프로젝트 경로

| 항목 | 경로 |
|------|------|
| 이 프로젝트 (원본) | `D:\workspace\UnrealEditorBridge` |
| .NET 솔루션 | `D:\workspace\UnrealEditorBridge\UnrealEditorBridge.sln` |
| UE5 샘플 프로젝트 | `D:\UnrealEngine\Sample` |
| UE5 엔진 | `D:\Epic Games\UE_5.7` |

### 8.3 빌드 및 실행 순서

1. **UE5 플러그인 배치**: `ue5-plugin/UnrealEditorBridge/` 폴더를 `D:\UnrealEngine\Sample\Plugins\`에 복사 (또는 심볼릭 링크)
2. **UE5 Editor 실행**: Sample 프로젝트를 UE5 Editor로 연다. 플러그인이 자동 로드되어 MMF를 생성한다.
3. **.NET 솔루션 빌드**: Visual Studio에서 `UnrealEditorBridge.sln`을 열고 빌드한다.
4. **WPF 앱 실행**: `UnrealEditorBridge.Wpf`를 시작 프로젝트로 설정하고 실행한다.
5. **연결**: WPF 앱의 Connection 패널에서 Discovery 목록을 확인하고 원하는 인스턴스를 선택하여 연결한다.

### 8.4 디버깅 팁

- **MMF 확인**: Sysinternals의 `WinObj` 도구로 `\Sessions\1\BaseNamedObjects\UEB_*` 아래에 MMF, Mutex, Event가 생성되었는지 확인할 수 있다.
- **UE5 로그 확인**: Editor의 Output Log에서 `[UEB]` 접두사로 필터링하면 플러그인 로그를 볼 수 있다.
- **WPF 디버깅**: Adapter의 `BridgeClient.cs`에 브레이크포인트를 걸어 연결/Snapshot/Event 흐름을 추적할 수 있다.

---

## 9. 관련 문서 링크

| 문서 | 경로 | 내용 |
|------|------|------|
| 아키텍처 개요 | `docs/Design/01-architecture-overview.md` | 전체 시스템 구조, 데이터 흐름 |
| 프로토콜 설계 | `docs/Design/02-protocol-design.md` | MMF 레이아웃, 필드 정의, 동기화 규칙 |
| Adapter 설계 | `docs/Design/03-adapter-design.md` | .NET Adapter 내부 구조 |
| WPF 설계 | `docs/Design/04-wpf-design.md` | WPF MVVM 구조, 뷰-뷰모델 매핑 |
| UE5 플러그인 설계 | `docs/Design/05-ue5-plugin-design.md` | C++ 플러그인 구조, 클래스 책임 |
| 확장/하위호환 | `docs/Design/06-extension-future-proofing.md` | 향후 확장 방향, 버전 전략, 로드맵 |

---

## 10. 핵심 코드 파일 빠른 참조

빠른 탐색을 위한 핵심 파일 목록:

### .NET 측

| 파일 | 절대 경로 | 역할 |
|------|----------|------|
| 프로토콜 상수 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Protocol\ProtocolConstants.cs` | 매직, 크기, IPC 이름 |
| Header 레이아웃 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Protocol\HeaderLayout.cs` | Header 필드 오프셋 |
| Discovery 레이아웃 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Protocol\DiscoveryLayout.cs` | Discovery 필드 오프셋 |
| 클라이언트 인터페이스 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Adapter\IBridgeClient.cs` | 퍼블릭 API |
| 클라이언트 구현 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Adapter\Internal\BridgeClient.cs` | 핵심 로직 |
| Snapshot 역직렬화 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Adapter\Internal\Serialization\JsonSnapshotDeserializer.cs` | JSON 파싱 + 폴백 |
| WPF 서비스 | `D:\workspace\UnrealEditorBridge\src\UnrealEditorBridge.Wpf\Services\BridgeService.cs` | Dispatcher 마샬링 |

### C++ 측

| 파일 | 절대 경로 | 역할 |
|------|----------|------|
| 프로토콜 상수 | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\Source\UnrealEditorBridge\Private\Ipc\ProtocolConstants.h` | 매직, 크기 |
| Header 레이아웃 | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\Source\UnrealEditorBridge\Private\Ipc\HeaderLayout.h` | 필드 오프셋 |
| Editor Subsystem | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\Source\UnrealEditorBridge\Public\BridgeEditorSubsystem.h` | 라이프사이클 |
| 에셋 수집 | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\Source\UnrealEditorBridge\Private\BridgeAssetCollector.cpp` | 태그 필터링 포함 |
| JSON 직렬화 | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\Source\UnrealEditorBridge\Private\Serialization\BridgeJsonSerializer.cpp` | Snapshot/Event 직렬화 |
| 플러그인 디스크립터 | `D:\workspace\UnrealEditorBridge\ue5-plugin\UnrealEditorBridge\UnrealEditorBridge.uplugin` | Editor, PostEngineInit, Win64 |

---

*이 문서는 프로젝트의 현재 상태를 기준으로 작성되었다. 코드 변경 시 관련 섹션을 업데이트할 것을 권장한다.*
