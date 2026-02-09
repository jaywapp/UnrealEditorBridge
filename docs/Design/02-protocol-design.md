# UnrealEditorBridge.Protocol 설계

## 1. 개요

UnrealEditorBridge.Protocol은 Unreal Engine 5 Editor(C++)와 .NET Adapter 간 공유하는 바이너리 메모리 레이아웃 규약이다. 이 규약은 Memory-Mapped File의 구조, 필드 오프셋, 직렬화 형식, IPC 네이밍 규칙을 정의한다.

양측(C++, C#)이 동일한 규약을 각자의 언어로 구현하며, 바이너리 호환성을 보장한다.

---

## 2. MMF 전체 메모리 레이아웃

전체 MMF 크기: **16 MB** (기본값, 구성 가능)

```
Offset          Size            영역
────────────────────────────────────────────────────
0x0000_0000     256 bytes       Header
0x0000_0100     4 MB            Snapshot Area
0x0040_0100     ~11.99 MB       Event Ring Buffer
────────────────────────────────────────────────────
총합: 16,777,216 bytes (16 MB)
```

상세 레이아웃:

```
┌──────────────────────────────────────────────────────────────┐
│                        HEADER (256 bytes)                     │
│  Offset 0x0000 ~ 0x00FF                                      │
├──────────────────────────────────────────────────────────────┤
│                    SNAPSHOT AREA (4 MB)                        │
│  Offset 0x0100 ~ 0x003F_FFFF + 0x0100                        │
│  (4,194,304 bytes)                                            │
├──────────────────────────────────────────────────────────────┤
│                  EVENT RING BUFFER (~11.99 MB)                │
│  Offset 0x0040_0100 ~ MMF 끝                                 │
│  고정 크기 레코드 슬롯 배열                                   │
└──────────────────────────────────────────────────────────────┘
```

구성 상수 (C++ `BridgeProtocol` / C# `ProtocolConstants`에서 정의):

| 항목 | 값 | 설명 |
|------|-----|------|
| `HeaderSize` | 256 bytes | Header 영역 고정 크기 |
| `SnapshotOffset` | 256 (0x0100) | Snapshot 영역 시작 오프셋 |
| `SnapshotMaxSize` | 4,194,304 (4 MB) | Snapshot 영역 최대 크기 |
| `EventRingOffset` | `SnapshotOffset + SnapshotMaxSize` | Event Ring 시작 오프셋 |
| `EventSlotSize` | 2,048 bytes | 개별 이벤트 슬롯 크기 |
| `EventSlotCount` | 6,144 | Ring Buffer 슬롯 총 개수 |
| `TotalMmfSize` | `EventRingOffset + EventSlotSize * EventSlotCount` | MMF 전체 크기 |

> **참고:** 위 상수들은 Header 내에 중복 저장하지 않으며, 양측 코드에 컴파일 타임 상수로 하드코딩되어 있다.

---

## 3. Header 필드 정의 (256 bytes)

모든 정수 필드는 **Little-Endian** 바이트 순서를 사용한다.

Header에는 런타임 변동 값만 포함한다. 고정 구성 값(HeaderSize, TotalMmfSize, SnapshotOffset, SnapshotCapacity, EventRingOffset, EventSlotSize, EventSlotCount, ProjectName 등)은 양측에 컴파일 타임 상수로 정의되어 있으므로 Header에 기록하지 않는다.

| 오프셋 | 크기 | 타입 | 필드명 | 설명 |
|--------|------|------|--------|------|
| 0x00 | 4 | `uint32` | `Magic` | 매직 넘버: `0x55454221` (ASCII `"UEB!"`) |
| 0x04 | 4 | `uint32` | `ProtocolVersion` | 프로토콜 버전 (Major * 1000 + Minor, 예: 1000 = v1.0) |
| 0x08 | 4 | `uint32` | `WriterPid` | Writer 프로세스 ID (Editor PID) |
| 0x0C | 4 | - | (패딩) | 정렬 패딩 (8바이트 경계 정렬) |
| 0x10 | 8 | `int64` | `Heartbeat` | 마지막 Writer 활동 시각 (UTC Ticks, 100ns 단위) |
| 0x18 | 4 | `uint32` | `SnapshotVersion` | Snapshot 버전 카운터 (기록 시마다 증가) |
| 0x1C | 4 | `uint32` | `SnapshotSize` | 현재 Snapshot 데이터 실제 크기 (바이트) |
| 0x20 | 4 | `uint32` | `SnapshotCrc32` | Snapshot 데이터의 CRC32 체크섬 |
| 0x24 | 4 | - | (패딩) | 정렬 패딩 |
| 0x28 | 4 | `uint32` | `EventWriteIndex` | 다음 기록 위치 (0-based, wrap-around) |
| 0x2C | 4 | - | (패딩) | 정렬 패딩 (8바이트 경계 정렬) |
| 0x30 | 8 | `uint64` | `EventSequenceNumber` | 전역 이벤트 시퀀스 번호 (monotonic 증가) |
| 0x38 | 200 | - | `Reserved` | 향후 확장용 예약 영역 (0으로 초기화) |

### 3.1 필드 상세 설명

#### Magic (0x55454221, "UEB!")
MMF가 UnrealEditorBridge 프로토콜 데이터를 포함하는지 빠르게 검증하는 용도. Reader는 이 값이 불일치하면 즉시 연결을 거부한다.

> **주의:** ASCII 표현은 `"UEB!"`이며 `"UEB1"`이 아니다. 바이트열은 `0x21` = `'!'`이다.

#### ProtocolVersion
`Major * 1000 + Minor` 형식. 예를 들어 버전 1.2는 `1002`로 표현한다. Reader는 자신이 지원하는 Major 버전과 불일치하면 연결을 거부한다. Minor 버전 차이는 하위 호환으로 처리한다.

#### WriterPid
Writer(Editor) 프로세스 ID. Reader가 Writer 프로세스의 생존 여부를 확인하는 데 사용할 수 있다.

#### Heartbeat
Writer(Editor)가 매 1초마다 현재 UTC Ticks 값으로 갱신한다. Reader는 이 값이 5초 이상 미갱신되면 Editor가 응답 없음 상태로 판단한다.

- C++: `FDateTime::UtcNow().GetTicks()`
- C#: `DateTime.UtcNow.Ticks`

두 값 모두 동일한 기준(0001-01-01 00:00:00 UTC부터 100ns 단위)을 사용하므로 호환된다.

#### SnapshotVersion
Snapshot이 기록될 때마다 1씩 증가한다. Reader는 마지막으로 읽은 버전과 비교하여 변경 시에만 Snapshot을 다시 읽는다. 이를 통해 불필요한 역직렬화를 방지한다.

#### SnapshotSize
현재 Snapshot 영역에 기록된 JSON 데이터의 실제 크기(바이트). Reader는 이 크기만큼만 데이터를 읽는다.

#### SnapshotCrc32
Snapshot 데이터 영역의 CRC32 체크섬. IEEE 802.3 표준 CRC32를 사용한다 (다항식: `0xEDB88320`, 초기값: `0xFFFFFFFF`, 최종 XOR: `0xFFFFFFFF`). Reader가 Snapshot을 읽은 후 무결성을 검증하는 데 사용한다. Mutex 획득 실패 등으로 인한 Partial Read를 감지할 수 있다.

#### EventWriteIndex
다음 이벤트가 기록될 슬롯 인덱스. `EventWriteIndex % EventSlotCount`로 실제 슬롯을 결정한다.

#### EventSequenceNumber
이벤트가 기록될 때마다 1씩 증가하는 전역 카운터. Ring Buffer의 wrap-around와 무관하게 단조 증가한다. Reader는 이 번호의 연속성을 확인하여 이벤트 유실(Overflow)을 감지한다.

---

## 4. Snapshot Area (4 MB)

### 4.1 구조

Snapshot 영역은 에셋 전체 상태를 JSON(UTF-8)으로 직렬화하여 저장한다.

```
┌─────────────────────────────────────────────┐
│  Snapshot Area (SnapshotMaxSize = 4 MB)     │
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │  JSON Payload (SnapshotSize bytes)     │  │
│  │  UTF-8 인코딩, null-terminated         │  │
│  └────────────────────────────────────────┘  │
│  │  Unused Space (패딩)                   │  │
│  └────────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### 4.2 Snapshot JSON 스키마

```json
{
  "timestamp": "2026-02-06T12:00:00.000Z",
  "assetCount": 1234,
  "assets": [
    {
      "objectPath": "/Game/Characters/Hero/SK_Hero.SK_Hero",
      "packagePath": "/Game/Characters/Hero",
      "assetName": "SK_Hero",
      "className": "/Script/Engine.SkeletalMesh",
      "tags": {
        "Purpose": "Character",
        "LODGroup": "LargeWorld"
      },
      "dependencies": {
        "hard": ["/Game/Characters/Hero/M_Hero"],
        "soft": ["/Game/Characters/Hero/ABP_Hero"]
      }
    }
  ]
}
```

### 4.3 필드 정의

| 필드 | 타입 | 설명 |
|------|------|------|
| `timestamp` | `string` (ISO 8601) | Snapshot 생성 시각 (UTC) |
| `assetCount` | `integer` | 에셋 총 개수 |
| `assets` | `array` | 에셋 배열 |
| `assets[].objectPath` | `string` | 에셋의 전체 오브젝트 경로 |
| `assets[].packagePath` | `string` | 패키지 디렉토리 경로 |
| `assets[].assetName` | `string` | 에셋 이름 |
| `assets[].className` | `string` | 에셋 클래스 경로 |
| `assets[].tags` | `object` | 키-값 태그 (Asset Registry Tags) |
| `assets[].dependencies.hard` | `string[]` | 하드 레퍼런스 경로 배열 |
| `assets[].dependencies.soft` | `string[]` | 소프트 레퍼런스 경로 배열 |

### 4.4 태그 필터링 규칙

에셋의 Asset Registry Tags 수집 시, **값(Value)의 길이가 1024자를 초과하는 태그는 필터링하여 제외**한다. 이는 `FiBData` 등 대용량 바이너리/직렬화 태그가 Snapshot 크기를 불필요하게 증가시키는 것을 방지하기 위함이다.

```cpp
// C++ Editor Plugin 내 구현 (BridgeAssetCollector.cpp)
constexpr int32 MaxTagValueLength = 1024;
// TagValue.Len() > MaxTagValueLength인 태그는 수집하지 않음
```

### 4.5 CRC32 체크섬 사양

Snapshot 및 Event Payload의 무결성 검증에 사용되는 CRC32는 **IEEE 802.3 표준**을 따른다.

| 항목 | 값 |
|------|-----|
| 다항식 (반사) | `0xEDB88320` |
| 초기값 (init) | `0xFFFFFFFF` |
| 최종 XOR (finalXor) | `0xFFFFFFFF` |
| 입력 반사 | Yes |
| 출력 반사 | Yes |

### 4.6 Snapshot 기록 절차 (Writer)

1. Named Mutex 획득 (타임아웃: 100ms)
2. `Header.SnapshotVersion` 증가
3. JSON 직렬화 후 `SnapshotOffset` (= 0x0100) 위치에 기록
4. `Header.SnapshotSize` 갱신
5. `Header.SnapshotCrc32` 갱신 (CRC32)
6. `Header.Heartbeat` 갱신
7. Named Mutex 해제
8. Snapshot Named Event Signal

### 4.7 Snapshot 읽기 절차 (Reader)

1. `Header.SnapshotVersion`을 먼저 읽어 변경 확인
2. 변경이 없으면 스킵
3. 변경 시: Named Mutex 획득 (타임아웃: 50ms)
4. `SnapshotSize` 만큼 데이터 복사
5. Named Mutex 해제
6. CRC32 검증 → 불일치 시 재시도 (최대 3회)
7. JSON 역직렬화

---

## 5. Event Ring Buffer

### 5.1 구조

Ring Buffer는 고정 크기 슬롯 배열로 구성된다.

```
┌──────────────────────────────────────────────────────┐
│  Event Ring Buffer                                    │
│                                                       │
│  Slot[0]  Slot[1]  Slot[2]  ...  Slot[N-1]           │
│  ┌──────┐┌──────┐┌──────┐      ┌──────┐             │
│  │Record││Record││Record│ ...  │Record│             │
│  └──────┘└──────┘└──────┘      └──────┘             │
│                                                       │
│  WriteIndex ──────────────▶ 다음 기록 위치            │
│                                                       │
│  슬롯 크기: EventSlotSize (기본 2048 bytes)           │
│  슬롯 개수: EventSlotCount (기본 6144개)              │
│  총 크기: ~12 MB                                      │
└──────────────────────────────────────────────────────┘
```

### 5.2 기본 구성

| 항목 | 기본값 | 설명 |
|------|--------|------|
| `EventSlotSize` | 2,048 bytes | 단일 이벤트 레코드 최대 크기 |
| `EventSlotCount` | 6,144 | Ring Buffer 슬롯 총 개수 |
| Ring Buffer 총 크기 | 12,582,912 bytes (~12 MB) | `SlotSize * SlotCount` |

### 5.3 Event Record 구조 (개별 슬롯)

각 슬롯은 고정 크기 헤더(24 bytes) + 가변 JSON 페이로드로 구성된다.

```
┌───────────────────────────────────────────┐
│  Event Slot (EventSlotSize = 2048 bytes)  │
│                                            │
│  Offset   Size    필드                     │
│  ─────────────────────────────────         │
│  0x00     8       SequenceNumber (uint64)  │
│  0x08     4       EventType (uint32)       │
│  0x0C     4       PayloadSize (uint32)     │
│  0x10     4       PayloadCrc32 (uint32)    │
│  0x14     4       (패딩, 8바이트 정렬)     │
│  0x18     ~2024   Payload (JSON, UTF-8)    │
│                                            │
│  고정 헤더: 24 bytes (0x00 ~ 0x17)         │
│  최대 페이로드: SlotSize - 24 bytes        │
│             = 2048 - 24 = 2024 bytes       │
└───────────────────────────────────────────┘
```

> **참고:** Event Slot에는 `Timestamp` 필드가 없다. 이벤트 발생 시각이 필요한 경우 페이로드 JSON 내에 포함한다.

> **참고:** `PayloadCrc32`는 `Payload` 영역의 `PayloadSize` 바이트에 대한 CRC32 체크섬이다. Header의 `SnapshotCrc32`와 동일한 IEEE 802.3 CRC32 알고리즘을 사용한다.

### 5.4 EventType 열거값

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | `None` | 빈 슬롯 (초기 상태) |
| 1 | `AssetCreated` | 에셋 생성 |
| 2 | `AssetDeleted` | 에셋 삭제 |
| 3 | `AssetRenamed` | 에셋 이름 변경 |
| 4 | `AssetSaved` | 에셋 저장 |
| 5 | `AssetTagsChanged` | 에셋 태그 변경 |
| 6 | `AssetMoved` | 에셋 경로 이동 |
| 7 | `AssetLoaded` | 에셋 로드 |
| 8 | `AssetDependencyChanged` | 에셋 의존성 변경 |
| 100 | `SnapshotUpdated` | 전체 Snapshot 갱신 알림 |
| 200 | `EditorShutdown` | Editor 정상 종료 알림 |
| 0xFFFF | `Reserved` | 향후 확장용 예약 |

### 5.5 Event Payload JSON 예시

**AssetCreated:**
```json
{
  "objectPath": "/Game/Blueprints/BP_NewActor.BP_NewActor",
  "packagePath": "/Game/Blueprints",
  "assetName": "BP_NewActor",
  "className": "/Script/Engine.Blueprint"
}
```

**AssetRenamed:**
```json
{
  "oldObjectPath": "/Game/Meshes/SM_Old.SM_Old",
  "newObjectPath": "/Game/Meshes/SM_New.SM_New",
  "oldAssetName": "SM_Old",
  "newAssetName": "SM_New"
}
```

**AssetSaved:**
```json
{
  "objectPath": "/Game/Maps/MainLevel.MainLevel",
  "assetName": "MainLevel",
  "className": "/Script/Engine.World"
}
```

### 5.6 Ring Buffer 기록 절차 (Writer)

1. Named Mutex 획득 (타임아웃: 50ms, 실패 시 이벤트 드롭)
2. `Header.EventWriteIndex` 위치의 슬롯 오프셋 계산
   - `offset = EventRingOffset + (EventWriteIndex % EventSlotCount) * EventSlotSize`
3. Event Record 기록 (SequenceNumber, EventType, PayloadSize, PayloadCrc32, Payload)
4. `Header.EventWriteIndex` 증가 (`(EventWriteIndex + 1) % EventSlotCount`)
5. `Header.EventSequenceNumber` 증가
6. `Header.Heartbeat` 갱신
7. Named Mutex 해제
8. EventStream Named Event Signal

### 5.7 Ring Buffer 읽기 절차 (Reader)

1. EventStream Named Event 대기 (타임아웃: 1초)
2. Named Mutex 획득
3. 로컬 `lastReadSequence`와 `Header.EventSequenceNumber` 비교
4. 새 이벤트 존재 시:
   - 로컬 ReadIndex에서 WriteIndex까지 순회
   - 각 슬롯의 SequenceNumber 연속성 검증
   - 각 슬롯의 PayloadCrc32 검증
5. Named Mutex 해제
6. 이벤트 처리 (Mutex 외부에서)

### 5.8 Overflow 대응 전략

Ring Buffer가 가득 차면 Writer는 가장 오래된 슬롯을 덮어쓴다. Reader 측에서의 대응:

| 감지 방법 | 대응 |
|-----------|------|
| SequenceNumber Gap 감지 | `lastReadSequence + 1 != slot.SequenceNumber` |
| 대응 조치 | 1. 누락 이벤트 수 로깅 |
|           | 2. ReadIndex를 현재 WriteIndex로 리셋 |
|           | 3. Snapshot 재요청 (전체 상태 동기화) |
|           | 4. `EventOverflow` 콜백 발행 |

---

## 6. IPC 네이밍 규칙

### 6.1 인스턴스별 IPC 객체

```
패턴: UEB_{ProjectName}_{PID}[_Suffix]

예시 (프로젝트: MyGame, PID: 12345):
  MMF:            UEB_MyGame_12345
  Mutex:          UEB_MyGame_12345_Mtx
  Snapshot Event: UEB_MyGame_12345_SnapshotEvt
  Stream Event:   UEB_MyGame_12345_StreamEvt
```

### 6.2 Discovery MMF

모든 활성 Editor 인스턴스를 등록하는 공용 MMF:

```
이름: UEB_Discovery
크기: 65,600 bytes (DiscoveryHeaderSize + DiscoveryEntrySize * DiscoveryMaxEntries)
```

Discovery MMF 레이아웃:

```
┌──────────────────────────────────────────────────────────────┐
│  Discovery Header (64 bytes)                                  │
│  ────────────────────────────                                │
│  0x00  uint32  Magic (0x55454244 = "UEBD")                   │
│  0x04  uint32  EntryCount (현재 등록된 인스턴스 수)            │
│  0x08  56 bytes Reserved (향후 확장용, 0으로 초기화)           │
├──────────────────────────────────────────────────────────────┤
│  Entry[0] (512 bytes)                                        │
│  ────────────────────────────                                │
│  0x00   uint32    ProcessId                                  │
│  0x04   4 bytes   (패딩, 8바이트 정렬)                        │
│  0x08   int64     RegisteredAt (UTC Ticks)                   │
│  0x10   int64     LastHeartbeat (UTC Ticks)                  │
│  0x18   8 bytes   (패딩)                                     │
│  0x20   char[128] ProjectName (UTF-8, null-terminated)       │
│  0xA0   char[256] MmfName (UTF-8, null-terminated)           │
│  0x1A0  96 bytes  Reserved                                   │
├──────────────────────────────────────────────────────────────┤
│  Entry[1] ...                                                │
│  ...                                                         │
│  Entry[127]                                                  │
└──────────────────────────────────────────────────────────────┘
```

Discovery 구성 상수:

| 항목 | 값 | 설명 |
|------|-----|------|
| `DiscoveryMagic` | `0x55454244` ("UEBD") | Discovery MMF 매직 넘버 |
| `DiscoveryHeaderSize` | 64 bytes | Discovery Header 크기 |
| `DiscoveryEntrySize` | 512 bytes | 개별 엔트리 크기 |
| `DiscoveryMaxEntries` | 128 | 최대 등록 가능 인스턴스 수 |

### 6.3 네이밍 규칙 제약

- `ProjectName`에 포함 불가 문자: `\ / : * ? " < > |`
- 이러한 문자는 `_`로 치환
- 최대 길이: 64자 (초과 시 잘라냄)

---

## 7. 프로토콜 버전 관리 전략

### 7.1 버전 체계

```
ProtocolVersion = Major * 1000 + Minor

Major: 바이너리 레이아웃 변경 (호환 불가)
Minor: 기존 레이아웃 내 확장 (하위 호환)
```

### 7.2 호환성 규칙

| 상황 | 동작 |
|------|------|
| Reader Major == Writer Major | 정상 연결 |
| Reader Major != Writer Major | 연결 거부, 버전 불일치 오류 |
| Reader Minor < Writer Minor | 정상 연결 (알 수 없는 필드 무시) |
| Reader Minor > Writer Minor | 정상 연결 (누락 필드에 기본값 적용) |

### 7.3 Major 버전 변경 기준

- Header 필드 오프셋 변경
- 기존 Header 필드 타입 변경
- Snapshot/Event 영역 기본 오프셋 변경
- EventType 기존 값의 의미 변경

### 7.4 Minor 버전 변경 기준

- Header Reserved 영역에 새 필드 추가
- 새 EventType 추가
- Snapshot JSON에 새 필드 추가
- Event Payload JSON에 새 필드 추가

### 7.5 버전 이력 관리

각 버전의 변경 사항을 문서 내에 이력으로 관리한다:

```
v1.0 (1000) - 초기 버전
  - Header: 256 bytes (런타임 필드 9개, Magic ~ EventSequenceNumber)
  - Magic: 0x55454221 ("UEB!")
  - Snapshot: JSON 기반 에셋 스냅샷 (태그 값 1024자 제한 필터링 적용)
  - Event Ring Buffer: 6144 슬롯 (각 2048 bytes)
  - Event Slot: 24-byte 헤더 (SequenceNumber, EventType, PayloadSize, PayloadCrc32)
  - CRC32: IEEE 802.3 표준 (init=0xFFFFFFFF, finalXor=0xFFFFFFFF)
  - Discovery: 64-byte 헤더, 512-byte 엔트리, 최대 128개
```
