# UnrealEditorBridge.Protocol API 레퍼런스

> **네임스페이스:** `UnrealEditorBridge.Protocol`
> **어셈블리:** `UnrealEditorBridge.Protocol.dll`
> **대상 프레임워크:** .NET 8.0
> **역할:** MMF(Memory-Mapped File) 기반 IPC 프로토콜의 바이너리 레이아웃, 상수, 파싱 유틸리티를 정의한다. C++ Editor Plugin과 .NET Adapter 양쪽에서 동일한 레이아웃을 공유해야 한다.

---

## 목차

1. [ProtocolConstants](#protocolconstants)
2. [HeaderLayout](#headerlayout)
3. [DiscoveryLayout](#discoverylayout)
4. [EventSlotLayout](#eventslotlayout)
5. [HeaderData](#headerdata)
6. [HeaderParser](#headerparser)
7. [EventRecordParser](#eventrecordparser)
8. [AssetEventType](#asseteventtype)
9. [Crc32](#crc32)

---

## ProtocolConstants

```csharp
public static class ProtocolConstants
```

UnrealEditorBridge 프로토콜의 전역 상수를 정의한다. C++ Editor Plugin과 .NET Adapter 양측에서 동일한 값을 사용해야 한다.

### 상수 필드

| 상수 | 타입 | 값 | 설명 |
|---|---|---|---|
| `Magic` | `uint` | `0x55454221` | MMF Header 매직 넘버. ASCII `"UEB!"`. |
| `DiscoveryMagic` | `uint` | `0x55454244` | Discovery MMF 매직 넘버. ASCII `"UEBD"`. |
| `ProtocolVersion` | `uint` | `1000` | 현재 프로토콜 버전. `Major * 1000 + Minor`. v1.0 = 1000. |
| `ProtocolMajor` | `uint` | `1` | 프로토콜 Major 버전. |
| `ProtocolMinor` | `uint` | `0` | 프로토콜 Minor 버전. |
| `HeaderSize` | `int` | `256` | Header 영역 크기 (바이트). |
| `DefaultTotalMmfSize` | `int` | `16,777,216` (16 MB) | MMF 기본 전체 크기. |
| `DefaultSnapshotCapacity` | `int` | `4,194,304` (4 MB) | Snapshot 영역 기본 용량. |
| `DefaultSnapshotOffset` | `int` | `256` (`0x0100`) | Snapshot 영역 기본 시작 오프셋. `HeaderSize`와 동일. |
| `DefaultEventRingOffset` | `int` | `4,194,560` | Event Ring Buffer 기본 시작 오프셋. `DefaultSnapshotOffset + DefaultSnapshotCapacity`. |
| `DefaultEventSlotSize` | `int` | `2048` | 개별 이벤트 슬롯 기본 크기 (바이트). |
| `DefaultEventSlotCount` | `int` | `6144` | Ring Buffer 슬롯 기본 총 개수. |
| `EventRecordHeaderSize` | `int` | `24` | 이벤트 슬롯 내부 고정 헤더 크기 (바이트). |
| `DiscoveryMmfName` | `string` | `"UEB_Discovery"` | Discovery MMF 이름. |
| `DiscoveryMmfSize` | `int` | `65,536` (64 KB) | Discovery MMF 크기. |
| `DiscoveryHeaderSize` | `int` | `64` | Discovery Header 크기 (바이트). |
| `DiscoveryEntrySize` | `int` | `512` | Discovery 개별 엔트리 크기 (바이트). |
| `DiscoveryMaxEntries` | `int` | `128` | Discovery 최대 엔트리 수. |
| `MaxProjectNameLength` | `int` | `64` | ProjectName 최대 길이 (문자). |
| `ProjectNameFieldSize` | `int` | `128` | ProjectName 필드 바이트 크기 (Header 내). |
| `IpcPrefix` | `string` | `"UEB_"` | IPC 이름 프리픽스. |
| `MutexSuffix` | `string` | `"_Mtx"` | Mutex 이름 접미사. |
| `SnapshotEventSuffix` | `string` | `"_SnapshotEvt"` | Snapshot Event 이름 접미사. |
| `StreamEventSuffix` | `string` | `"_StreamEvt"` | Stream Event 이름 접미사. |

### 정적 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `InvalidProjectNameChars` | `char[]` (readonly) | ProjectName에서 치환되어야 하는 문자. `\ / : * ? " < > \|` |

### 메서드

#### BuildMmfName

```csharp
public static string BuildMmfName(string projectName, uint pid)
```

프로젝트 이름과 프로세스 ID로 MMF 이름을 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `projectName` | `string` | Unreal 프로젝트 이름. |
| `pid` | `uint` | Editor 프로세스 ID. |

**반환값:** `string` -- MMF 이름 (예: `"UEB_MyGame_12345"`).

내부적으로 `SanitizeProjectName`을 호출하여 프로젝트 이름을 정제한 후 `"{IpcPrefix}{sanitized}_{pid}"` 형식으로 조합한다.

```csharp
// 사용 예시
string mmfName = ProtocolConstants.BuildMmfName("MyGame", 12345);
// 결과: "UEB_MyGame_12345"

string mmfName2 = ProtocolConstants.BuildMmfName("My:Game/Test", 9999);
// 결과: "UEB_My_Game_Test_9999"  (특수문자 '_'로 치환)
```

---

#### BuildMutexName

```csharp
public static string BuildMutexName(string mmfName)
```

MMF 이름으로부터 Mutex 이름을 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `mmfName` | `string` | MMF 이름. |

**반환값:** `string` -- Mutex 이름 (예: `"UEB_MyGame_12345_Mtx"`).

```csharp
string mutexName = ProtocolConstants.BuildMutexName("UEB_MyGame_12345");
// 결과: "UEB_MyGame_12345_Mtx"
```

---

#### BuildSnapshotEventName

```csharp
public static string BuildSnapshotEventName(string mmfName)
```

MMF 이름으로부터 Snapshot Event 이름을 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `mmfName` | `string` | MMF 이름. |

**반환값:** `string` -- Snapshot Event 이름 (예: `"UEB_MyGame_12345_SnapshotEvt"`).

```csharp
string evtName = ProtocolConstants.BuildSnapshotEventName("UEB_MyGame_12345");
// 결과: "UEB_MyGame_12345_SnapshotEvt"
```

---

#### BuildStreamEventName

```csharp
public static string BuildStreamEventName(string mmfName)
```

MMF 이름으로부터 Stream Event 이름을 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `mmfName` | `string` | MMF 이름. |

**반환값:** `string` -- Stream Event 이름 (예: `"UEB_MyGame_12345_StreamEvt"`).

---

#### SanitizeProjectName

```csharp
public static string SanitizeProjectName(string raw)
```

프로젝트 이름에서 허용되지 않는 문자를 `'_'`로 치환하고 최대 길이(`MaxProjectNameLength` = 64)를 제한한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `raw` | `string` | 원본 프로젝트 이름. |

**반환값:** `string` -- 안전한 프로젝트 이름. `null` 또는 빈 문자열이면 `"Unknown"` 반환.

```csharp
string safe = ProtocolConstants.SanitizeProjectName("My:Game/Test");
// 결과: "My_Game_Test"

string unknown = ProtocolConstants.SanitizeProjectName(null);
// 결과: "Unknown"
```

---

#### GetMajorVersion

```csharp
public static uint GetMajorVersion(uint version)
```

프로토콜 버전 값에서 Major 버전을 추출한다 (`version / 1000`).

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `version` | `uint` | 프로토콜 버전 값. |

**반환값:** `uint` -- Major 버전 번호.

---

#### GetMinorVersion

```csharp
public static uint GetMinorVersion(uint version)
```

프로토콜 버전 값에서 Minor 버전을 추출한다 (`version % 1000`).

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `version` | `uint` | 프로토콜 버전 값. |

**반환값:** `uint` -- Minor 버전 번호.

---

#### IsMajorCompatible

```csharp
public static bool IsMajorCompatible(uint readerVersion, uint writerVersion)
```

두 버전의 Major 버전이 호환되는지 확인한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `readerVersion` | `uint` | Reader(Adapter) 측 프로토콜 버전. |
| `writerVersion` | `uint` | Writer(Editor) 측 프로토콜 버전. |

**반환값:** `bool` -- Major 버전이 동일하면 `true`.

```csharp
bool ok = ProtocolConstants.IsMajorCompatible(1000, 1001); // true (Major 1 == 1)
bool ng = ProtocolConstants.IsMajorCompatible(1000, 2000); // false (Major 1 != 2)
```

---

## HeaderLayout

```csharp
public static class HeaderLayout
```

MMF Header 영역의 필드 오프셋 및 크기를 정의한다. 모든 정수 필드는 **Little-Endian** 바이트 순서를 사용한다. C++ `BridgeHeaderLayout`과 동일한 레이아웃을 유지해야 한다.

### Header 메모리 맵

```
Offset  Size    Type     필드
------  -----   ------   -------------------------
0x00    4       uint32   Magic
0x04    4       uint32   ProtocolVersion
0x08    4       uint32   WriterPid
0x0C    4       -        (예약)
0x10    8       int64    Heartbeat (UTC Ticks)
0x18    4       uint32   SnapshotVersion
0x1C    4       uint32   SnapshotSize
0x20    4       uint32   SnapshotCrc32
0x24    4       -        (예약)
0x28    4       uint32   EventWriteIndex
0x2C    4       -        (예약)
0x30    8       uint64   EventSequenceNumber
0x38 ~ 0xFF    -        (예약, 총 Header 256 바이트)
```

### 상수 필드

| 상수 | 타입 | 값 | 설명 |
|---|---|---|---|
| `MagicOffset` | `int` | `0x00` | Magic 필드 오프셋 (uint32, 4 bytes). |
| `ProtocolVersionOffset` | `int` | `0x04` | ProtocolVersion 필드 오프셋 (uint32, 4 bytes). |
| `WriterPidOffset` | `int` | `0x08` | WriterPid 필드 오프셋 (uint32, 4 bytes). |
| `HeartbeatOffset` | `int` | `0x10` | Heartbeat 필드 오프셋 (int64, 8 bytes). UTC Ticks. |
| `SnapshotVersionOffset` | `int` | `0x18` | SnapshotVersion 필드 오프셋 (uint32, 4 bytes). |
| `SnapshotSizeOffset` | `int` | `0x1C` | SnapshotSize 필드 오프셋 (uint32, 4 bytes). Snapshot 데이터 실제 크기. |
| `SnapshotCrc32Offset` | `int` | `0x20` | SnapshotCrc32 필드 오프셋 (uint32, 4 bytes). CRC32 체크섬. |
| `EventWriteIndexOffset` | `int` | `0x28` | EventWriteIndex 필드 오프셋 (uint32, 4 bytes). |
| `EventSequenceNumberOffset` | `int` | `0x30` | EventSequenceNumber 필드 오프셋 (uint64, 8 bytes). |

---

## DiscoveryLayout

```csharp
public static class DiscoveryLayout
```

Discovery MMF의 Header 및 Entry 필드 오프셋을 정의한다. C++ `BridgeHeaderLayout`의 Discovery 영역과 동일한 레이아웃을 유지해야 한다.

### Discovery MMF 메모리 맵

```
[Discovery Header - 64 bytes]
Offset  Size    Type     필드
------  -----   ------   --------------------------
0x00    4       uint32   DiscoveryMagic ("UEBD")
0x04    4       uint32   EntryCount
0x08 ~ 0x3F    -        (예약)

[Entry #N - 각 512 bytes, 엔트리 시작 기준 상대 오프셋]
Offset  Size    Type           필드
------  -----   ------------   --------------------------
0x00    4       uint32         ProcessId
0x04    4       -              (예약)
0x08    8       int64          RegisteredAt (UTC Ticks)
0x10    8       int64          LastHeartbeat (UTC Ticks)
0x18    8       -              (예약)
0x20    128     char[128]      ProjectName (UTF-8, null-terminated)
0xA0    256     char[256]      MmfName (UTF-8, null-terminated)
0x1A0 ~ 0x1FF  -              (예약, 총 엔트리 512 바이트)
```

### Header 상수 필드

| 상수 | 타입 | 값 | 설명 |
|---|---|---|---|
| `MagicOffset` | `int` | `0x00` | Discovery Magic 오프셋 (uint32, 4 bytes). |
| `EntryCountOffset` | `int` | `0x04` | EntryCount 오프셋 (uint32, 4 bytes). 현재 등록된 인스턴스 수. |

### Entry 상수 필드 (엔트리 시작 기준 상대 오프셋)

| 상수 | 타입 | 값 | 설명 |
|---|---|---|---|
| `EntryProcessIdOffset` | `int` | `0x00` | ProcessId 오프셋 (uint32, 4 bytes). |
| `EntryRegisteredAtOffset` | `int` | `0x08` | RegisteredAt 오프셋 (int64, 8 bytes). UTC Ticks. |
| `EntryLastHeartbeatOffset` | `int` | `0x10` | LastHeartbeat 오프셋 (int64, 8 bytes). UTC Ticks. |
| `EntryProjectNameOffset` | `int` | `0x20` | ProjectName 오프셋 (char[128], UTF-8). |
| `EntryProjectNameSize` | `int` | `128` | ProjectName 필드 크기 (바이트). |
| `EntryMmfNameOffset` | `int` | `0xA0` | MmfName 오프셋 (char[256], UTF-8). |
| `EntryMmfNameSize` | `int` | `256` | MmfName 필드 크기 (바이트). |

### 메서드

#### GetEntryAbsoluteOffset

```csharp
public static int GetEntryAbsoluteOffset(int entryIndex)
```

Discovery MMF 내에서 특정 엔트리의 절대 오프셋을 계산한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `entryIndex` | `int` | 엔트리 인덱스 (0-based). |

**반환값:** `int` -- MMF 내 절대 오프셋. `DiscoveryHeaderSize + entryIndex * DiscoveryEntrySize`.

```csharp
int offset0 = DiscoveryLayout.GetEntryAbsoluteOffset(0); // 64
int offset1 = DiscoveryLayout.GetEntryAbsoluteOffset(1); // 576
int offset2 = DiscoveryLayout.GetEntryAbsoluteOffset(2); // 1088
```

---

## EventSlotLayout

```csharp
public static class EventSlotLayout
```

Event Ring Buffer 내 개별 슬롯의 필드 오프셋을 정의한다. C++ `BridgeHeaderLayout`의 Slot 영역과 동일한 레이아웃을 유지해야 한다.

### 슬롯 메모리 맵

```
[Event Slot - 기본 2048 bytes, 슬롯 시작 기준 상대 오프셋]
Offset  Size       Type       필드
------  ---------  ---------  -------------------------
0x00    8          uint64     SequenceNumber
0x08    4          uint32     EventType (AssetEventType)
0x0C    4          uint32     PayloadSize
0x10    4          uint32     PayloadCrc32
0x14    4          -          (예약)
0x18    가변       byte[]     Payload (JSON, UTF-8)
```

### 상수 필드

| 상수 | 타입 | 값 | 설명 |
|---|---|---|---|
| `SequenceNumberOffset` | `int` | `0x00` | SequenceNumber 필드 오프셋 (uint64, 8 bytes). 슬롯 시작 기준 상대 오프셋. |
| `EventTypeOffset` | `int` | `0x08` | EventType 필드 오프셋 (uint32, 4 bytes). |
| `PayloadSizeOffset` | `int` | `0x0C` | PayloadSize 필드 오프셋 (uint32, 4 bytes). |
| `PayloadCrc32Offset` | `int` | `0x10` | PayloadCrc32 필드 오프셋 (uint32, 4 bytes). CRC32 체크섬. |
| `PayloadOffset` | `int` | `0x18` | Payload 시작 오프셋. JSON 데이터 (UTF-8). |
| `RecordHeaderSize` | `int` | `24` | 슬롯 내 고정 헤더 크기 (바이트). `PayloadOffset`과 동일. |

### 메서드

#### GetMaxPayloadSize

```csharp
public static int GetMaxPayloadSize(int slotSize)
```

지정된 슬롯 크기에서 페이로드 최대 크기를 계산한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotSize` | `int` | 슬롯 전체 크기 (바이트). |

**반환값:** `int` -- 페이로드 최대 크기. `slotSize - RecordHeaderSize`.

```csharp
int maxPayload = EventSlotLayout.GetMaxPayloadSize(2048);
// 결과: 2024  (2048 - 24)
```

---

#### GetSlotAbsoluteOffset

```csharp
public static long GetSlotAbsoluteOffset(int ringBufferOffset, int slotIndex, int slotSize)
```

Ring Buffer 내에서 특정 슬롯의 절대 오프셋을 계산한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `ringBufferOffset` | `int` | Ring Buffer 시작 오프셋 (Header로부터). |
| `slotIndex` | `int` | 슬롯 인덱스 (0-based). |
| `slotSize` | `int` | 슬롯 크기 (바이트). |

**반환값:** `long` -- MMF 내 절대 오프셋. `ringBufferOffset + (long)slotIndex * slotSize`.

```csharp
long offset = EventSlotLayout.GetSlotAbsoluteOffset(
    ProtocolConstants.DefaultEventRingOffset, 0, 2048);
// 결과: 4194560

long offset3 = EventSlotLayout.GetSlotAbsoluteOffset(
    ProtocolConstants.DefaultEventRingOffset, 3, 2048);
// 결과: 4200704  (4194560 + 3 * 2048)
```

---

## HeaderData

```csharp
public sealed class HeaderData
```

MMF Header 영역을 구조화된 형태로 읽어 담는 데이터 클래스. C++ `BridgeIpcWriter`가 기록하는 필드만 포함한다. 모든 프로퍼티는 `init` 접근자를 사용한다.

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|---|---|---|
| `Magic` | `uint` | 매직 넘버. 유효한 값은 `ProtocolConstants.Magic` (`0x55454221`). |
| `ProtocolVersion` | `uint` | 프로토콜 버전 (`Major * 1000 + Minor`). |
| `WriterPid` | `uint` | Writer(Editor) 프로세스 ID. |
| `Heartbeat` | `long` | 마지막 Writer 활동 시각 (UTC Ticks). |
| `SnapshotVersion` | `uint` | Snapshot 버전 카운터. |
| `SnapshotSize` | `uint` | 현재 Snapshot 데이터 실제 크기 (바이트). |
| `SnapshotCrc32` | `uint` | Snapshot 데이터의 CRC32 체크섬. |
| `EventWriteIndex` | `uint` | 다음 이벤트 기록 위치 (0-based, wrap-around). |
| `EventSequenceNumber` | `ulong` | 전역 이벤트 시퀀스 번호 (monotonic 증가). |

### 메서드

#### IsValidMagic

```csharp
public bool IsValidMagic()
```

Magic 필드가 유효한지 확인한다.

**반환값:** `bool` -- `Magic == ProtocolConstants.Magic`이면 `true`.

---

#### IsMajorCompatible

```csharp
public bool IsMajorCompatible()
```

현재 Adapter가 지원하는 Major 버전과 Header의 Major 버전이 호환되는지 확인한다.

**반환값:** `bool` -- Major 버전이 동일하면 `true`.

---

#### IsHeartbeatAlive

```csharp
public bool IsHeartbeatAlive(TimeSpan timeout)
```

Heartbeat이 지정 시간 이내인지 확인한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `timeout` | `TimeSpan` | 타임아웃 시간. |

**반환값:** `bool` -- 현재 UTC 시각 기준으로 Heartbeat과의 차이가 `timeout` 이하이면 `true`.

```csharp
HeaderData header = HeaderParser.Parse(buffer);

if (!header.IsValidMagic())
    throw new InvalidDataException("유효하지 않은 MMF 매직 넘버");

if (!header.IsMajorCompatible())
    throw new InvalidOperationException("프로토콜 Major 버전 불일치");

bool alive = header.IsHeartbeatAlive(TimeSpan.FromSeconds(5));
Console.WriteLine($"Editor PID={header.WriterPid}, Alive={alive}");
Console.WriteLine($"Snapshot v{header.SnapshotVersion}, Size={header.SnapshotSize} bytes");
Console.WriteLine($"Event SeqNo={header.EventSequenceNumber}, WriteIdx={header.EventWriteIndex}");
```

---

## HeaderParser

```csharp
public static class HeaderParser
```

바이트 배열로부터 Header 데이터를 파싱하는 유틸리티. Little-Endian 바이트 순서를 사용한다.

### 메서드

#### Parse

```csharp
public static HeaderData Parse(ReadOnlySpan<byte> buffer)
```

바이트 버퍼에서 Header 전체를 파싱하여 `HeaderData`를 생성한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `buffer` | `ReadOnlySpan<byte>` | 최소 `ProtocolConstants.HeaderSize` (256) 바이트 이상의 버퍼. |

**반환값:** `HeaderData` -- 파싱된 Header 데이터.

**예외:**

| 예외 타입 | 조건 |
|---|---|
| `ArgumentException` | 버퍼가 Header 크기 (256 바이트)보다 작은 경우. |

```csharp
byte[] rawHeader = new byte[256];
// MMF에서 rawHeader를 읽었다고 가정

HeaderData data = HeaderParser.Parse(rawHeader);
Console.WriteLine($"Magic: 0x{data.Magic:X8}");
Console.WriteLine($"Protocol: v{ProtocolConstants.GetMajorVersion(data.ProtocolVersion)}" +
                  $".{ProtocolConstants.GetMinorVersion(data.ProtocolVersion)}");
```

---

#### ReadHeartbeat

```csharp
public static long ReadHeartbeat(ReadOnlySpan<byte> buffer)
```

바이트 버퍼에서 Heartbeat 값만 빠르게 읽는다. 전체 Header 파싱 없이 Heartbeat만 확인할 때 사용한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `buffer` | `ReadOnlySpan<byte>` | 최소 `0x18` (24) 바이트 이상의 버퍼. |

**반환값:** `long` -- Heartbeat UTC Ticks 값.

---

#### ReadSnapshotVersion

```csharp
public static uint ReadSnapshotVersion(ReadOnlySpan<byte> buffer)
```

바이트 버퍼에서 SnapshotVersion 값만 빠르게 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `buffer` | `ReadOnlySpan<byte>` | 최소 `0x1C` (28) 바이트 이상의 버퍼. |

**반환값:** `uint` -- SnapshotVersion 값.

---

#### ReadEventWriteIndex

```csharp
public static uint ReadEventWriteIndex(ReadOnlySpan<byte> buffer)
```

바이트 버퍼에서 EventWriteIndex 값만 빠르게 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `buffer` | `ReadOnlySpan<byte>` | 최소 `0x2C` (44) 바이트 이상의 버퍼. |

**반환값:** `uint` -- EventWriteIndex 값.

---

#### ReadEventSequenceNumber

```csharp
public static ulong ReadEventSequenceNumber(ReadOnlySpan<byte> buffer)
```

바이트 버퍼에서 EventSequenceNumber 값만 빠르게 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `buffer` | `ReadOnlySpan<byte>` | 최소 `0x38` (56) 바이트 이상의 버퍼. |

**반환값:** `ulong` -- EventSequenceNumber 값.

```csharp
// 성능이 중요한 폴링 루프에서 전체 파싱 대신 개별 필드만 읽기
byte[] buf = new byte[256];
// MMF에서 buf를 읽었다고 가정

long heartbeat = HeaderParser.ReadHeartbeat(buf);
uint snapVer = HeaderParser.ReadSnapshotVersion(buf);
ulong seqNo = HeaderParser.ReadEventSequenceNumber(buf);

Console.WriteLine($"Heartbeat Ticks: {heartbeat}");
Console.WriteLine($"Snapshot Version: {snapVer}");
Console.WriteLine($"Event SeqNo: {seqNo}");
```

---

## EventRecordParser

```csharp
public static class EventRecordParser
```

Event Ring Buffer의 개별 슬롯 데이터를 파싱하는 유틸리티. C++ `BridgeIpcWriter`가 기록하는 슬롯 레이아웃에 맞춰 파싱한다.

### 메서드

#### ReadSequenceNumber

```csharp
public static ulong ReadSequenceNumber(ReadOnlySpan<byte> slotData)
```

이벤트 슬롯 바이트 데이터에서 SequenceNumber를 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `ulong` -- SequenceNumber 값.

---

#### ReadEventType

```csharp
public static AssetEventType ReadEventType(ReadOnlySpan<byte> slotData)
```

이벤트 슬롯 바이트 데이터에서 EventType을 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `AssetEventType` -- 이벤트 타입 열거 값.

---

#### ReadPayloadSize

```csharp
public static uint ReadPayloadSize(ReadOnlySpan<byte> slotData)
```

이벤트 슬롯 바이트 데이터에서 PayloadSize를 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `uint` -- 페이로드 크기 (바이트).

---

#### ReadPayloadCrc32

```csharp
public static uint ReadPayloadCrc32(ReadOnlySpan<byte> slotData)
```

이벤트 슬롯 바이트 데이터에서 PayloadCrc32를 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `uint` -- CRC32 체크섬 값.

---

#### ReadPayloadJson

```csharp
public static string ReadPayloadJson(ReadOnlySpan<byte> slotData)
```

이벤트 슬롯 바이트 데이터에서 JSON 페이로드 문자열을 읽는다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `string` -- JSON 페이로드 문자열 (UTF-8 디코딩). 페이로드 크기가 0이면 `string.Empty` 반환. PayloadSize가 슬롯 잔여 공간보다 크면 잔여 공간까지만 읽는다.

---

#### IsValidSlot

```csharp
public static bool IsValidSlot(ReadOnlySpan<byte> slotData)
```

슬롯이 유효한 이벤트 데이터를 포함하는지 확인한다. `SequenceNumber`가 0이 아니고 `EventType`이 `None`이 아니면 유효하다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `slotData` | `ReadOnlySpan<byte>` | 슬롯 바이트 데이터. |

**반환값:** `bool` -- 유효한 이벤트 데이터를 포함하면 `true`.

```csharp
byte[] slotData = new byte[2048];
// Ring Buffer에서 슬롯 데이터를 읽었다고 가정

if (EventRecordParser.IsValidSlot(slotData))
{
    ulong seqNo      = EventRecordParser.ReadSequenceNumber(slotData);
    var eventType     = EventRecordParser.ReadEventType(slotData);
    uint payloadSize  = EventRecordParser.ReadPayloadSize(slotData);
    uint payloadCrc   = EventRecordParser.ReadPayloadCrc32(slotData);
    string json       = EventRecordParser.ReadPayloadJson(slotData);

    // CRC 무결성 검증
    var payloadBytes = Encoding.UTF8.GetBytes(json);
    uint computedCrc = Crc32.Compute(payloadBytes);
    bool isValid = (computedCrc == payloadCrc);

    Console.WriteLine($"Seq={seqNo}, Type={eventType}, Size={payloadSize}, CRC OK={isValid}");
    Console.WriteLine($"JSON: {json}");
}
```

---

## AssetEventType

```csharp
public enum AssetEventType : uint
```

에셋 이벤트 타입을 정의하는 열거형. Event Ring Buffer의 `EventType` 필드에 기록되는 값과 일치한다.

### 멤버

| 멤버 | 값 | 설명 |
|---|---|---|
| `None` | `0` | 빈 슬롯 (초기 상태). |
| `AssetCreated` | `1` | 에셋이 새로 생성되었다. |
| `AssetDeleted` | `2` | 에셋이 삭제되었다. |
| `AssetRenamed` | `3` | 에셋 이름이 변경되었다. |
| `AssetSaved` | `4` | 에셋이 저장되었다. |
| `AssetTagsChanged` | `5` | 에셋 태그가 변경되었다. |
| `AssetMoved` | `6` | 에셋이 다른 경로로 이동되었다. |
| `AssetLoaded` | `7` | 에셋이 메모리에 로드되었다. |
| `AssetDependencyChanged` | `8` | 에셋의 의존성이 변경되었다. |
| `SnapshotUpdated` | `100` | 전체 Snapshot이 갱신되었음을 알린다. |
| `EditorShutdown` | `200` | Editor가 정상 종료됨을 알린다. |
| `Reserved` | `0xFFFF` | 향후 확장용 예약 값. |

### 사용 예시

```csharp
AssetEventType type = EventRecordParser.ReadEventType(slotData);

switch (type)
{
    case AssetEventType.AssetCreated:
        Console.WriteLine("새 에셋 생성됨");
        break;
    case AssetEventType.AssetDeleted:
        Console.WriteLine("에셋 삭제됨");
        break;
    case AssetEventType.AssetRenamed:
        Console.WriteLine("에셋 이름 변경됨");
        break;
    case AssetEventType.SnapshotUpdated:
        Console.WriteLine("Snapshot 갱신 알림");
        break;
    case AssetEventType.EditorShutdown:
        Console.WriteLine("Editor 종료");
        break;
}
```

---

## Crc32

```csharp
public static class Crc32
```

CRC32 체크섬 계산 유틸리티. Snapshot 데이터 및 이벤트 페이로드의 무결성 검증에 사용한다. IEEE 802.3 (표준 CRC32) 다항식 `0xEDB88320`을 사용한다.

### 메서드

#### Compute (ReadOnlySpan 오버로드)

```csharp
public static uint Compute(ReadOnlySpan<byte> data)
```

바이트 스팬의 CRC32 체크섬을 계산한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `data` | `ReadOnlySpan<byte>` | 체크섬을 계산할 데이터. |

**반환값:** `uint` -- CRC32 체크섬 값.

---

#### Compute (배열 오버로드)

```csharp
public static uint Compute(byte[] data, int offset, int length)
```

바이트 배열의 특정 범위에 대한 CRC32 체크섬을 계산한다.

| 매개변수 | 타입 | 설명 |
|---|---|---|
| `data` | `byte[]` | 체크섬을 계산할 데이터 배열. |
| `offset` | `int` | 시작 오프셋. |
| `length` | `int` | 데이터 길이. |

**반환값:** `uint` -- CRC32 체크섬 값.

내부적으로 `data.AsSpan(offset, length)`를 `Compute(ReadOnlySpan<byte>)` 오버로드에 위임한다.

```csharp
// Snapshot CRC 검증 예시
byte[] snapshotData = /* MMF에서 읽은 Snapshot 바이트 */;
uint computed = Crc32.Compute(snapshotData);
uint expected = header.SnapshotCrc32;

if (computed != expected)
{
    Console.WriteLine($"CRC 불일치! 계산={computed:X8}, 기대={expected:X8}");
}

// 배열 오버로드 사용
byte[] largeBuffer = new byte[8192];
uint partial = Crc32.Compute(largeBuffer, 100, 500);
```

---

## 전체 프로토콜 워크플로 예시

아래 코드는 Protocol 라이브러리만으로 MMF 바이너리 데이터를 파싱하는 전체 흐름을 보여준다.

```csharp
using System.Text;
using UnrealEditorBridge.Protocol;

// 1. Header 파싱
byte[] headerBuf = new byte[ProtocolConstants.HeaderSize];
// ... MMF에서 headerBuf를 읽는다 ...

HeaderData header = HeaderParser.Parse(headerBuf);

// 2. 유효성 검증
if (!header.IsValidMagic())
    throw new InvalidDataException("잘못된 매직 넘버");

if (!header.IsMajorCompatible())
    throw new InvalidOperationException(
        $"버전 불일치: Adapter={ProtocolConstants.ProtocolVersion}, " +
        $"Editor={header.ProtocolVersion}");

// 3. Heartbeat 확인
if (!header.IsHeartbeatAlive(TimeSpan.FromSeconds(5)))
    Console.WriteLine("Editor가 응답하지 않음");

// 4. Snapshot CRC 검증
byte[] snapshotBuf = new byte[header.SnapshotSize];
// ... MMF Snapshot 영역에서 snapshotBuf를 읽는다 ...
uint crc = Crc32.Compute(snapshotBuf);
if (crc != header.SnapshotCrc32)
    Console.WriteLine("Snapshot CRC 불일치");

// 5. Event Ring Buffer 읽기
for (uint i = 0; i < ProtocolConstants.DefaultEventSlotCount; i++)
{
    long slotOffset = EventSlotLayout.GetSlotAbsoluteOffset(
        ProtocolConstants.DefaultEventRingOffset,
        (int)i,
        ProtocolConstants.DefaultEventSlotSize);

    byte[] slotBuf = new byte[ProtocolConstants.DefaultEventSlotSize];
    // ... MMF에서 slotBuf를 읽는다 ...

    if (!EventRecordParser.IsValidSlot(slotBuf))
        continue;

    ulong seqNo = EventRecordParser.ReadSequenceNumber(slotBuf);
    AssetEventType type = EventRecordParser.ReadEventType(slotBuf);
    string json = EventRecordParser.ReadPayloadJson(slotBuf);

    Console.WriteLine($"[{seqNo}] {type}: {json}");
}
```
