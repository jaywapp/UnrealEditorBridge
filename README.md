# UnrealEditorBridge

UE5 Editor와 외부 .NET/WPF 도구 간의 실시간 IPC 브리지.

Memory-Mapped File(MMF) 기반으로 에셋 스냅샷, 실시간 이벤트 스트림, Heartbeat 모니터링을 제공한다.

---

## 아키텍처

```
┌─────────────────┐    MMF + Mutex + Event    ┌─────────────────┐
│   UE5 Editor    │ ◄──────────────────────── │   .NET / WPF    │
│   (C++ Plugin)  │         IPC (단방향)       │   Application   │
│                 │                            │                 │
│  Writer (생산자) │    ──────────────────►    │  Reader (소비자) │
└─────────────────┘                            └─────────────────┘
```

**데이터 흐름** (UE5 → .NET, 단방향):
- **Snapshot**: 전체 에셋 메타데이터 JSON (최대 4MB)
- **Event Stream**: Ring Buffer 기반 실시간 에셋 이벤트 (6,144 슬롯)
- **Heartbeat**: 1초 간격 생존 확인
- **Discovery**: 활성 Editor 인스턴스 탐색

---

## 프로젝트 구조

```
UnrealEditorBridge/
├── src/
│   ├── UnrealEditorBridge.Protocol/   # 프로토콜 상수, 레이아웃, 파서 (.NET 8.0)
│   ├── UnrealEditorBridge.Adapter/    # MMF 리더, 연결 관리, 이벤트 (.NET 8.0-windows)
│   └── UnrealEditorBridge.Wpf/        # WPF MVVM UI (.NET 8.0-windows)
├── ue5-plugin/
│   └── UnrealEditorBridge/            # UE5 C++ Editor 플러그인 (Win64)
├── docs/
│   ├── Design/                        # 설계 문서 (6개)
│   ├── Guide/                         # 빌드/테스트 가이드 (2개)
│   ├── Reference/                     # API 레퍼런스 (3개)
│   └── Handover/                      # 인수인계 문서
└── UnrealEditorBridge.sln
```

---

## 기술 스택

| 구성 요소 | 기술 |
|-----------|------|
| IPC | Memory-Mapped File + Named Mutex + Named Event |
| 직렬화 | JSON (UTF-8) + CRC32 무결성 검증 |
| .NET | .NET 8.0, ReactiveUI 20.1.1, Prism.Core 9.0.537, Unity.Container 5.11.11 |
| WPF | MVVM (ReactiveObject + DelegateCommand), Dispatcher 마샬링 |
| UE5 | 5.7, Editor-only 플러그인, Win64 |
| 프로토콜 | v1.0 (Major*1000+Minor), 16MB MMF |

---

## 빌드

### 사전 요구 사항

- Windows 10/11
- .NET 8.0 SDK
- UE5 5.7
- Visual Studio 2022 (C++ Game Development + .NET Desktop Development)

### C# 프로젝트

```bash
dotnet build UnrealEditorBridge.sln
```

### UE5 플러그인

1. `ue5-plugin/UnrealEditorBridge/` 폴더를 UE5 프로젝트의 `Plugins/` 폴더에 복사
2. 에디터 빌드:

```bash
"<UE5경로>/Engine/Build/BatchFiles/Build.bat" <프로젝트명>Editor Win64 Development -Project="<프로젝트>.uproject"
```

### 실행

```bash
# WPF 앱 실행
dotnet run --project src/UnrealEditorBridge.Wpf
```

---

## 사용 방법

### 1. UE5 에디터 실행
플러그인이 설치된 UE5 프로젝트를 에디터로 연다. Output Log에 `[UEB]` 로그가 표시되면 플러그인 활성화 완료.

### 2. WPF 앱에서 연결
1. **인스턴스 새로고침** 클릭 → 활성 Editor 인스턴스 표시
2. 인스턴스 선택 → **연결** 클릭
3. 에셋 목록 (3,000+개) 로드 확인

### 3. 실시간 모니터링
- 에셋 생성/삭제/이름변경/저장 시 이벤트 로그에 실시간 표시
- 검색/클래스 필터로 에셋 필터링
- 에셋 선택 시 상세 정보 (태그, 의존성) 표시

---

## MMF 프로토콜 개요

### 메모리 레이아웃 (16MB)

| 영역 | 오프셋 | 크기 |
|------|--------|------|
| Header | 0x0000 | 256 B |
| Snapshot (JSON) | 0x0100 | 4 MB |
| Event Ring Buffer | 0x400100 | ~12 MB (6,144 x 2,048 B) |

### IPC 명명 규칙

| 객체 | 이름 패턴 | 예시 |
|------|-----------|------|
| MMF | `UEB_{프로젝트}_{PID}` | `UEB_Sample_12345` |
| Mutex | `{MMF명}_Mtx` | `UEB_Sample_12345_Mtx` |
| Snapshot Event | `{MMF명}_SnapshotEvt` | `UEB_Sample_12345_SnapshotEvt` |
| Stream Event | `{MMF명}_StreamEvt` | `UEB_Sample_12345_StreamEvt` |
| Discovery MMF | `UEB_Discovery` | `UEB_Discovery` |

### 이벤트 타입

| 값 | 이름 | 설명 |
|----|------|------|
| 1 | AssetCreated | 에셋 생성 |
| 2 | AssetDeleted | 에셋 삭제 |
| 3 | AssetRenamed | 에셋 이름 변경 |
| 4 | AssetSaved | 에셋 저장 |
| 5 | AssetTagsChanged | 태그 변경 |
| 6 | AssetMoved | 에셋 이동 |
| 100 | SnapshotUpdated | 스냅샷 갱신 |
| 200 | EditorShutdown | 에디터 종료 |

---

## Adapter 사용 예시

```csharp
// 인스턴스 탐색
using var discovery = new EditorInstanceDiscovery();
var instances = discovery.GetActiveInstances();

// 연결
using var client = BridgeClientFactory.Create();
client.SnapshotReceived += (s, e) =>
    Console.WriteLine($"에셋 {e.Snapshot.Assets.Count}개 수신");
client.EventReceived += (s, e) =>
    Console.WriteLine($"이벤트: {e.Event.EventType} - {e.Event.ObjectPath}");

await client.ConnectAsync(instances[0].MmfName);

// 3초 대기 후 종료
await Task.Delay(3000);
await client.DisconnectAsync();
```

---

## 문서

| 경로 | 설명 |
|------|------|
| [docs/Design/](docs/Design/) | 아키텍처, 프로토콜, Adapter, WPF, UE5 플러그인 설계 문서 |
| [docs/Guide/](docs/Guide/) | 빌드 가이드, 수동 테스트 가이드 |
| [docs/Reference/](docs/Reference/) | Protocol, Adapter, WPF API 레퍼런스 |
| [docs/Handover/](docs/Handover/) | 인수인계 문서 (현재 상태, 이슈, 다음 작업) |

---

## 라이선스

이 프로젝트는 비공개 프로젝트입니다.
