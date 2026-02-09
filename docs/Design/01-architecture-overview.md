# UnrealEditorBridge - 아키텍처 개요

## 1. 시스템 개요

UnrealEditorBridge는 Unreal Engine 5 Editor와 외부 .NET/WPF 기반 툴을 연결하는 재사용 가능한 브릿지 시스템이다. Memory-Mapped File(MMF) 기반 IPC를 통해 Editor의 에셋 정보와 이벤트를 외부 프로세스에 전달하며, UI 기술에 종속되지 않는 코어 설계를 지향한다.

---

## 2. 아키텍처 다이어그램

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Unreal Engine 5 Editor (C++)                     │
│                                                                     │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │ Asset Registry  │  │ Editor Subsystem │  │ Editor Delegates  │  │
│  │   Scanning      │──│  (Lifecycle)     │──│  (이벤트 수집)     │  │
│  └────────┬────────┘  └────────┬─────────┘  └────────┬──────────┘  │
│           │                    │                      │             │
│           └────────────┬───────┘──────────────────────┘             │
│                        ▼                                            │
│           ┌────────────────────────┐                                │
│           │   FBridgeIpcWriter     │                                │
│           │  (Snapshot + Event     │                                │
│           │   직렬화 & MMF 기록)   │                                │
│           └────────────┬───────────┘                                │
│                        │                                            │
└────────────────────────┼────────────────────────────────────────────┘
                         │  Memory-Mapped File (IPC)
                         │  Named Event + Named Mutex
                         │
    ═══════════════════════════════════════════════════  OS Kernel
                         │
                         │  UnrealEditorBridge.Protocol
                         │  (공유 바이너리 레이아웃 규약)
                         │
┌────────────────────────┼────────────────────────────────────────────┐
│                        ▼                                            │
│           ┌────────────────────────┐                                │
│           │   BridgeConnection     │                                │
│           │  (MMF Reader,          │                                │
│           │   Heartbeat Monitor)   │                                │
│           └────────────┬───────────┘                                │
│                        │                                            │
│  ┌─────────────────────┼─────────────────────────────────────┐     │
│  │                     ▼                                     │     │
│  │  ┌──────────────────────┐  ┌───────────────────────────┐  │     │
│  │  │  SnapshotReader      │  │  EventStreamReader        │  │     │
│  │  │  (전체 상태 역직렬화) │  │  (Ring Buffer 이벤트 소비) │  │     │
│  │  └──────────┬───────────┘  └──────────┬────────────────┘  │     │
│  │             └──────────┬──────────────┘                   │     │
│  │                        ▼                                  │     │
│  │           ┌────────────────────────┐                      │     │
│  │           │  IBridgeClient         │                      │     │
│  │           │  (Public API)          │                      │     │
│  │           └────────────────────────┘                      │     │
│  │                                                           │     │
│  │              UnrealEditorBridge.Adapter (.NET)             │     │
│  └───────────────────────────────────────────────────────────┘     │
│                        │                                            │
│                        ▼                                            │
│  ┌───────────────────────────────────────────────────────────┐     │
│  │                                                           │     │
│  │  ┌──────────────┐ ┌──────────────┐ ┌─────────────────┐   │     │
│  │  │ MainViewModel│ │AssetListVM   │ │ConnectionVM     │   │     │
│  │  └──────┬───────┘ └──────┬───────┘ └──────┬──────────┘   │     │
│  │         │                │                │              │     │
│  │         ▼                ▼                ▼              │     │
│  │  ┌─────────────────────────────────────────────────┐     │     │
│  │  │              WPF Views (XAML)                    │     │     │
│  │  └─────────────────────────────────────────────────┘     │     │
│  │                                                           │     │
│  │              UnrealEditorBridge.Wpf                        │     │
│  └───────────────────────────────────────────────────────────┘     │
│                                                                     │
│                    외부 .NET 프로세스                                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. 레이어 정의 및 책임

### 3.1 Unreal Engine 5 Editor Plugin (C++)

| 항목 | 내용 |
|------|------|
| **타입** | Editor 전용 플러그인 (`ELoadingPhase::PostEngineInit`) |
| **책임** | Asset Registry 스캔, Editor 이벤트 수집, MMF 기록 |
| **의존성** | Unreal Engine API만 사용, 외부 프로세스 직접 참조 없음 |
| **역할** | IPC 데이터 **생산자(Producer)** |

핵심 구성 요소:
- **`FBridgeEditorSubsystem`**: `UEditorSubsystem`을 상속하며 플러그인 수명 관리
- **`FBridgeAssetCollector`**: Asset Registry에서 에셋 목록 및 메타데이터 수집
- **`FBridgeEventListener`**: Editor 델리게이트를 통해 에셋 변경 이벤트 감지
- **`FBridgeIpcWriter`**: 수집된 데이터를 Protocol 규약에 따라 MMF에 기록

### 3.2 UnrealEditorBridge.Protocol (공유 규약)

| 항목 | 내용 |
|------|------|
| **타입** | 바이너리 레이아웃 규약 (문서 + 상수 정의) |
| **책임** | MMF 메모리 구조, 필드 오프셋, 직렬화 형식 정의 |
| **소비자** | C++ Plugin과 .NET Adapter 양측에서 동일 규약 준수 |

이 규약은 코드 라이브러리가 아닌 **설계 문서 + 각 플랫폼별 상수/구조체 구현**으로 존재한다. C++ 측은 헤더 파일, .NET 측은 클래스 파일로 각각 동일한 레이아웃을 구현한다.

### 3.3 UnrealEditorBridge.Adapter (.NET Class Library)

| 항목 | 내용 |
|------|------|
| **타입** | .NET 8 Class Library (`netstandard2.0` 호환 가능) |
| **책임** | MMF 읽기, 이벤트 스트림 소비, 연결 상태 관리, Public API 제공 |
| **의존성** | `System.IO.MemoryMappedFiles`, `System.Threading` — UI 프레임워크 의존성 없음 |
| **역할** | IPC 데이터 **소비자(Consumer)** + 외부 클라이언트용 API 제공 |

핵심 구성 요소:
- **`IBridgeClient`**: 외부 소비자가 사용하는 Public API 인터페이스
- **`BridgeConnection`**: MMF 연결 수립, Heartbeat 모니터링
- **`SnapshotReader`**: Snapshot 영역 역직렬화
- **`EventStreamReader`**: Event Ring Buffer 소비
- **`EditorInstanceDiscovery`**: 실행 중인 UE Editor 인스턴스 탐색

### 3.4 UnrealEditorBridge.Wpf (WPF 애플리케이션)

| 항목 | 내용 |
|------|------|
| **타입** | WPF 애플리케이션 (.NET 8 Windows) |
| **책임** | 에셋 목록/이벤트/상세 정보 시각화, 연결 상태 표시 |
| **의존성** | `UnrealEditorBridge.Adapter` 참조 |
| **역할** | 최종 사용자 UI |

핵심 구성 요소:
- **`MainViewModel`**: 전체 화면 상태 조율
- **`AssetListViewModel`**: 에셋 목록 및 필터링
- **`AssetDetailViewModel`**: 에셋 상세 정보
- **`EventLogViewModel`**: 이벤트 스트림 로그
- **`ConnectionViewModel`**: 연결 상태 관리 및 인스턴스 선택

---

## 4. 데이터 흐름

### 4.1 초기 연결 및 Snapshot 흐름

```
[UE5 Editor 시작]
      │
      ▼
FBridgeEditorSubsystem.Initialize()
      │
      ├─ MMF 생성 (이름: UEB_{ProjectName}_{PID})
      ├─ Named Mutex 생성
      ├─ Named Event 생성 (Snapshot / EventStream 각각)
      │
      ▼
FBridgeAssetCollector.CollectFullSnapshot()
      │
      ├─ Asset Registry 전체 스캔
      ├─ 에셋 메타데이터 수집 (ObjectPath, PackagePath, AssetName, ClassName, Tags, Dependencies)
      │
      ▼
FBridgeIpcWriter.WriteSnapshot()
      │
      ├─ Mutex 획득
      ├─ Header.SnapshotVersion 증가
      ├─ Snapshot 영역에 JSON 직렬화 데이터 기록
      ├─ Header.SnapshotSize 갱신
      ├─ Header.Heartbeat 타임스탬프 갱신
      ├─ Mutex 해제
      ├─ Snapshot Named Event Signal
      │
      ▼
[외부 .NET 프로세스]
      │
      ▼
BridgeConnection.Connect("UEB_{ProjectName}_{PID}")
      │
      ├─ MMF 열기
      ├─ Header 읽기 → 프로토콜 버전 검증
      ├─ Heartbeat 모니터 시작 (백그라운드 스레드)
      │
      ▼
SnapshotReader.ReadSnapshot()
      │
      ├─ Mutex 획득
      ├─ Header.SnapshotVersion 확인 (변경 시만 읽기)
      ├─ Snapshot 영역 JSON 역직렬화
      ├─ Mutex 해제
      │
      ▼
IBridgeClient.SnapshotReceived 이벤트 발행
      │
      ▼
AssetListViewModel.OnSnapshotReceived()
      │
      ▼
[UI 갱신]
```

### 4.2 실시간 이벤트 흐름

```
[UE5 Editor에서 에셋 저장]
      │
      ▼
FBridgeEventListener.OnAssetSaved()
      │
      ▼
FBridgeIpcWriter.WriteEvent(AssetSaved, ...)
      │
      ├─ Mutex 획득
      ├─ Ring Buffer의 WriteIndex 위치에 이벤트 기록
      ├─ Header.EventWriteIndex 증가 (wrap-around)
      ├─ Header.EventSequenceNumber 증가 (monotonic)
      ├─ Header.Heartbeat 타임스탬프 갱신
      ├─ Mutex 해제
      ├─ Event Named Event Signal
      │
      ▼
EventStreamReader (대기 중 스레드 깨어남)
      │
      ├─ Mutex 획득
      ├─ 로컬 ReadIndex ~ Header.EventWriteIndex 범위 이벤트 읽기
      ├─ SequenceNumber 연속성 검증
      │    └─ Gap 감지 시 → Snapshot 재요청 플래그 설정
      ├─ Mutex 해제
      │
      ▼
IBridgeClient.EventReceived 이벤트 발행
      │
      ▼
EventLogViewModel.OnEventReceived()
      │
      ▼
[UI 갱신]
```

### 4.3 Heartbeat 및 연결 상태 흐름

```
[UE5 Editor]                          [.NET Adapter]
     │                                      │
     │  Header.Heartbeat 주기적 갱신         │
     │  (매 1초)                             │
     │                                      │
     │                                 HeartbeatMonitor
     │                                 (매 2초 체크)
     │                                      │
     │                                      ├─ Heartbeat 정상
     │                                      │  → ConnectionState.Connected 유지
     │                                      │
     │  [Editor 크래시/종료]                 ├─ Heartbeat 5초 이상 미갱신
     │                                      │  → ConnectionState.Lost
     │                                      │  → ConnectionLost 이벤트 발행
     │                                      │
     │                                      ├─ MMF 접근 불가
     │                                      │  → ConnectionState.Disconnected
     │                                      │  → 자원 정리 및 재연결 대기
```

---

## 5. IPC 객체 네이밍 규칙

여러 Unreal 프로젝트와 여러 Editor 인스턴스를 동시에 지원하기 위해 모든 IPC 객체 이름에 식별자를 포함한다.

| IPC 객체 | 이름 패턴 |
|-----------|-----------|
| Memory-Mapped File | `UEB_{ProjectName}_{PID}` |
| Named Mutex | `UEB_{ProjectName}_{PID}_Mtx` |
| Snapshot Event | `UEB_{ProjectName}_{PID}_SnapshotEvt` |
| EventStream Event | `UEB_{ProjectName}_{PID}_StreamEvt` |
| Discovery MMF | `UEB_Discovery` |

- **`ProjectName`**: `FApp::GetProjectName()`에서 획득한 Unreal 프로젝트명
- **`PID`**: Editor 프로세스 ID (10진수 문자열)

`UEB_Discovery`는 모든 활성 Editor 인스턴스 목록을 제공하는 공용 MMF로, Adapter가 연결 가능한 Editor를 탐색할 때 사용한다.

---

## 6. 설계 결정 요약

| 결정 사항 | 선택 | 근거 |
|-----------|------|------|
| IPC 방식 | Memory-Mapped File | 대용량 데이터 전달에 최적, 커널 복사 없음, 양방향 확장 가능 |
| 직렬화 형식 | JSON (UTF-8) | 디버깅 용이, 스키마 유연성, C++/C# 양측 표준 라이브러리 지원 |
| 동기화 메커니즘 | Named Mutex + Named Event | 크로스 프로세스 동기화 표준, Partial Read 방지 |
| 이벤트 전달 | Ring Buffer | 고정 메모리 할당, 오래된 이벤트 자동 폐기, Lock 경합 최소화 |
| Adapter 대상 프레임워크 | .NET 8 (netstandard2.0 호환) | 최신 .NET 지원 + .NET Framework 호환 가능성 유지 |
| WPF 패턴 | MVVM | WPF 표준 패턴, 테스트 용이성, 관심사 분리 |
| Editor Plugin 타입 | Editor Subsystem | Editor 수명에 자동 연동, 모듈 로드 관리 단순화 |
