# Unreal Engine 5 Editor Plugin 설계

## 1. 개요

UnrealEditorBridge의 UE5 측 구현체로, Editor 전용 플러그인으로 동작한다. Asset Registry에서 에셋 메타데이터를 수집하고 Editor 이벤트를 감지하여 MMF에 기록하는 IPC 데이터 생산자(Producer) 역할을 수행한다.

**플러그인 정보:**
- 플러그인 이름: `UnrealEditorBridge`
- 모듈 타입: `Editor` (Runtime 빌드에 포함되지 않음)
- 로딩 단계: `PostEngineInit`
- 지원 플랫폼: `Win64` (MMF가 Windows API 기반)

---

## 2. 플러그인 타입 및 라이프사이클

### 2.1 모듈 구성

```
UnrealEditorBridge/ (Plugin Root)
├── UnrealEditorBridge.uplugin
├── Source/
│   └── UnrealEditorBridge/
│       ├── UnrealEditorBridge.Build.cs
│       ├── Public/
│       │   ├── UnrealEditorBridgeModule.h
│       │   ├── BridgeEditorSubsystem.h
│       │   ├── BridgeAssetCollector.h
│       │   ├── BridgeEventListener.h
│       │   └── BridgeTypes.h
│       └── Private/
│           ├── UnrealEditorBridgeModule.cpp
│           ├── BridgeEditorSubsystem.cpp
│           ├── BridgeAssetCollector.cpp
│           ├── BridgeEventListener.cpp
│           ├── Ipc/
│           │   ├── BridgeIpcWriter.h
│           │   ├── BridgeIpcWriter.cpp
│           │   ├── BridgeMmfManager.h
│           │   ├── BridgeMmfManager.cpp
│           │   ├── BridgeDiscoveryRegistrar.h
│           │   ├── BridgeDiscoveryRegistrar.cpp
│           │   ├── ProtocolConstants.h
│           │   └── HeaderLayout.h
│           └── Serialization/
│               ├── BridgeJsonSerializer.h
│               └── BridgeJsonSerializer.cpp
```

### 2.2 .uplugin 정의

```json
{
    "FileVersion": 3,
    "Version": 1,
    "VersionName": "1.0",
    "FriendlyName": "Unreal Editor Bridge",
    "Description": "UE5 Editor와 외부 .NET 툴 간 IPC 브릿지",
    "Category": "Editor",
    "CreatedBy": "UnrealEditorBridge",
    "EnabledByDefault": true,
    "CanContainContent": false,
    "IsBetaVersion": false,
    "Modules": [
        {
            "Name": "UnrealEditorBridge",
            "Type": "Editor",
            "LoadingPhase": "PostEngineInit",
            "PlatformAllowList": ["Win64"]
        }
    ]
}
```

### 2.3 Build.cs 의존성

```csharp
// UnrealEditorBridge.Build.cs
public class UnrealEditorBridge : ModuleRules
{
    public UnrealEditorBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "AssetRegistry",
            "UnrealEd",
            "EditorSubsystem"
        });

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Json",
            "JsonUtilities",
            "Projects"
        });
    }
}
```

### 2.4 라이프사이클

```
[Editor 시작]
      │
      ▼
FUnrealEditorBridgeModule::StartupModule()
      │  (모듈 로드 확인 로깅)
      │
      ▼
UBridgeEditorSubsystem::Initialize()
      │
      ├─ 1. IPC 자원 생성
      │     ├─ FBridgeMmfManager: MMF + Mutex + Events 생성
      │     └─ FBridgeDiscoveryRegistrar: Discovery MMF에 등록
      │
      ├─ 2. 초기 Snapshot 수집 및 기록
      │     ├─ FBridgeAssetCollector: Asset Registry 전체 스캔
      │     └─ FBridgeIpcWriter: Snapshot 영역에 기록
      │
      ├─ 3. 이벤트 리스너 등록
      │     └─ FBridgeEventListener: Editor 델리게이트 바인딩
      │
      └─ 4. Heartbeat 타이머 시작 (1초 간격)
            └─ FBridgeIpcWriter: Header.Heartbeat 갱신

[Editor 사용 중]
      │
      ├─ 에셋 이벤트 발생 → FBridgeEventListener → FBridgeIpcWriter
      ├─ 주기적 Heartbeat 갱신
      └─ Snapshot 재생성 (선택적: Asset Registry 변경 누적 시)

[Editor 종료]
      │
      ▼
UBridgeEditorSubsystem::Deinitialize()
      │
      ├─ 1. EditorShutdown 이벤트 기록
      ├─ 2. Heartbeat 타이머 중지
      ├─ 3. 이벤트 리스너 해제
      ├─ 4. Discovery MMF에서 등록 해제
      └─ 5. IPC 자원 해제 (MMF, Mutex, Events)
```

---

## 3. Asset Registry 연동 지점

### 3.1 사용하는 Asset Registry API

```cpp
// IAssetRegistry 인터페이스 (UE5)
IAssetRegistry& AssetRegistry = FModuleManager::LoadModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();
```

### 3.2 데이터 수집 시점

| 시점 | API | 용도 |
|------|-----|------|
| 초기 Snapshot | `GetAllAssets()` | 전체 에셋 목록 수집 |
| 에셋 생성 | `OnAssetAdded` 델리게이트 | 새 에셋 감지 |
| 에셋 삭제 | `OnAssetRemoved` 델리게이트 | 에셋 제거 감지 |
| 에셋 이름 변경 | `OnAssetRenamed` 델리게이트 | 이름/경로 변경 감지 |
| 에셋 갱신 | `OnAssetUpdated` 델리게이트 | 메타데이터 변경 감지 |

### 3.3 수집 데이터 매핑

`FAssetData`에서 Protocol 모델로의 매핑:

| FAssetData 필드 | Protocol 필드 | 변환 |
|-----------------|---------------|------|
| `GetObjectPathString()` | `objectPath` | 직접 사용 |
| `PackagePath` | `packagePath` | `FName::ToString()` |
| `AssetName` | `assetName` | `FName::ToString()` |
| `AssetClassPath` | `className` | `FTopLevelAssetPath::ToString()` |
| `TagsAndValues` | `tags` | `TMap<FName, FAssetTagValueRef>` → `key:value` |

### 3.4 의존성 정보 수집

```cpp
// 에셋 의존성 수집
TArray<FAssetIdentifier> HardDeps;
TArray<FAssetIdentifier> SoftDeps;

AssetRegistry.GetDependencies(
    FAssetIdentifier(AssetData.PackageName),
    HardDeps,
    UE::AssetRegistry::EDependencyCategory::Package,
    UE::AssetRegistry::EDependencyQuery::Hard
);

AssetRegistry.GetDependencies(
    FAssetIdentifier(AssetData.PackageName),
    SoftDeps,
    UE::AssetRegistry::EDependencyCategory::Package,
    UE::AssetRegistry::EDependencyQuery::Soft
);
```

### 3.5 Asset Registry 준비 상태 처리

Editor 시작 시 Asset Registry가 아직 스캔 중일 수 있다. 이를 처리하기 위해:

```cpp
void UBridgeEditorSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    IAssetRegistry& AssetRegistry = ...;

    if (AssetRegistry.IsLoadingAssets())
    {
        // 스캔 완료 대기
        AssetRegistry.OnFilesLoaded().AddUObject(
            this, &UBridgeEditorSubsystem::OnAssetRegistryReady
        );
    }
    else
    {
        OnAssetRegistryReady();
    }

    // Heartbeat 타이머는 즉시 시작 (Snapshot이 없어도 연결 상태 감지 가능)
    StartHeartbeatTimer();
}

void UBridgeEditorSubsystem::OnAssetRegistryReady()
{
    CollectAndWriteInitialSnapshot();
    RegisterEventListeners();
}
```

---

## 4. Editor 이벤트 수집 방식

### 4.1 FBridgeEventListener 클래스

```cpp
class FBridgeEventListener
{
public:
    void Initialize(FBridgeIpcWriter* InWriter);
    void Shutdown();

private:
    FBridgeIpcWriter* Writer = nullptr;

    // 델리게이트 핸들
    FDelegateHandle OnAssetAddedHandle;
    FDelegateHandle OnAssetRemovedHandle;
    FDelegateHandle OnAssetRenamedHandle;
    FDelegateHandle OnAssetUpdatedHandle;
    FDelegateHandle OnAssetSavedHandle;

    // 콜백 함수
    void OnAssetAdded(const FAssetData& AssetData);
    void OnAssetRemoved(const FAssetData& AssetData);
    void OnAssetRenamed(const FAssetData& AssetData, const FString& OldObjectPath);
    void OnAssetUpdated(const FAssetData& AssetData);
    void OnAssetSaved(const FString& PackageName, UPackage* Package);
};
```

### 4.2 델리게이트 바인딩

```cpp
void FBridgeEventListener::Initialize(FBridgeIpcWriter* InWriter)
{
    Writer = InWriter;

    IAssetRegistry& AssetRegistry = ...;

    OnAssetAddedHandle = AssetRegistry.OnAssetAdded().AddRaw(
        this, &FBridgeEventListener::OnAssetAdded
    );

    OnAssetRemovedHandle = AssetRegistry.OnAssetRemoved().AddRaw(
        this, &FBridgeEventListener::OnAssetRemoved
    );

    OnAssetRenamedHandle = AssetRegistry.OnAssetRenamed().AddRaw(
        this, &FBridgeEventListener::OnAssetRenamed
    );

    OnAssetUpdatedHandle = AssetRegistry.OnAssetUpdated().AddRaw(
        this, &FBridgeEventListener::OnAssetUpdated
    );

    // 에셋 저장 이벤트 (UPackage::PackageSavedEvent)
    OnAssetSavedHandle = UPackage::PackageSavedWithContextEvent.AddRaw(
        this, &FBridgeEventListener::OnAssetSaved
    );
}

void FBridgeEventListener::Shutdown()
{
    IAssetRegistry& AssetRegistry = ...;
    AssetRegistry.OnAssetAdded().Remove(OnAssetAddedHandle);
    AssetRegistry.OnAssetRemoved().Remove(OnAssetRemovedHandle);
    AssetRegistry.OnAssetRenamed().Remove(OnAssetRenamedHandle);
    AssetRegistry.OnAssetUpdated().Remove(OnAssetUpdatedHandle);
    UPackage::PackageSavedWithContextEvent.Remove(OnAssetSavedHandle);
}
```

### 4.3 이벤트 콜백 구현

```cpp
void FBridgeEventListener::OnAssetAdded(const FAssetData& AssetData)
{
    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetCreated;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();
    Event.ClassName = AssetData.AssetClassPath.ToString();

    Writer->WriteEvent(Event);
}

void FBridgeEventListener::OnAssetRenamed(
    const FAssetData& AssetData, const FString& OldObjectPath)
{
    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetRenamed;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();
    Event.OldObjectPath = OldObjectPath;

    Writer->WriteEvent(Event);
}
```

### 4.4 이벤트 스로틀링

대량 에셋 임포트 등의 상황에서 이벤트 폭주를 방지한다.

```cpp
// 동일 에셋에 대한 연속 이벤트를 병합
// 100ms 내 동일 ObjectPath에 대한 같은 타입 이벤트는 마지막 것만 유지
class FEventThrottler
{
public:
    void EnqueueEvent(const FBridgeAssetEvent& Event);
    void Flush();  // 타이머 콜백에서 호출

private:
    TMap<FString, FBridgeAssetEvent> PendingEvents;
    FCriticalSection Lock;
    FTimerHandle FlushTimerHandle;
    static constexpr float FlushIntervalSeconds = 0.1f;  // 100ms
};
```

---

## 5. Snapshot 생성 전략

### 5.1 초기 Snapshot (전체 스캔)

Editor 시작 시(Asset Registry 준비 완료 후) 전체 에셋 목록을 수집한다.

```cpp
void FBridgeAssetCollector::CollectFullSnapshot(FBridgeSnapshot& OutSnapshot)
{
    IAssetRegistry& AssetRegistry = ...;

    TArray<FAssetData> AllAssets;
    AssetRegistry.GetAllAssets(AllAssets, true /* bIncludeOnlyOnDiskAssets */);

    OutSnapshot.Timestamp = FDateTime::UtcNow();
    OutSnapshot.Assets.Reserve(AllAssets.Num());

    for (const FAssetData& AssetData : AllAssets)
    {
        // /Engine, /Script 경로 필터링 (Editor 내부 에셋 제외)
        FString PackagePath = AssetData.PackagePath.ToString();
        if (PackagePath.StartsWith(TEXT("/Engine")) ||
            PackagePath.StartsWith(TEXT("/Script")))
        {
            continue;
        }

        FBridgeAssetInfo AssetInfo;
        AssetInfo.ObjectPath = AssetData.GetObjectPathString();
        AssetInfo.PackagePath = PackagePath;
        AssetInfo.AssetName = AssetData.AssetName.ToString();
        AssetInfo.ClassName = AssetData.AssetClassPath.ToString();

        // 태그 수집
        for (const auto& TagPair : AssetData.TagsAndValues)
        {
            AssetInfo.Tags.Add(TagPair.Key.ToString(), TagPair.Value.GetValue());
        }

        // 의존성 수집
        CollectDependencies(AssetData, AssetInfo.Dependencies);

        OutSnapshot.Assets.Add(MoveTemp(AssetInfo));
    }

    OutSnapshot.AssetCount = OutSnapshot.Assets.Num();
}
```

### 5.2 증분 Snapshot 갱신

이벤트 누적 시 전체 Snapshot을 재생성하는 전략을 사용한다.

| 조건 | 동작 |
|------|------|
| 이벤트 100건 누적 | Snapshot 재생성 |
| 마지막 Snapshot 이후 60초 경과 + 이벤트 존재 | Snapshot 재생성 |
| 외부 요청 (향후 확장) | Snapshot 즉시 재생성 |

```cpp
void UBridgeEditorSubsystem::OnEventWritten()
{
    EventsSinceLastSnapshot++;

    if (EventsSinceLastSnapshot >= SnapshotRebuildThreshold)  // 100
    {
        RebuildSnapshot();
    }
    else if (!SnapshotRebuildTimerHandle.IsValid())
    {
        // 60초 후 재생성 타이머 설정
        GetWorld()->GetTimerManager().SetTimer(
            SnapshotRebuildTimerHandle,
            this,
            &UBridgeEditorSubsystem::RebuildSnapshot,
            60.0f,
            false
        );
    }
}

void UBridgeEditorSubsystem::RebuildSnapshot()
{
    FBridgeSnapshot Snapshot;
    AssetCollector->CollectFullSnapshot(Snapshot);
    IpcWriter->WriteSnapshot(Snapshot);

    EventsSinceLastSnapshot = 0;
    SnapshotRebuildTimerHandle.Invalidate();
}
```

### 5.3 Snapshot 크기 관리

4 MB 제한을 초과할 수 있는 대형 프로젝트에 대한 대응:

| 전략 | 설명 |
|------|------|
| 의존성 요약 | 전체 의존성 대신 개수만 포함하는 경량 모드 |
| 태그 제한 | 주요 태그만 포함 (최대 10개/에셋) |
| 분할 Snapshot | Snapshot이 용량 초과 시 `SnapshotPartIndex` 필드로 분할 (향후 확장) |
| 동적 용량 | MMF 크기를 에셋 수 기반으로 동적 계산 |

기본 구현에서는 의존성을 개수 + 주요 5개 경로로 제한하여 용량을 관리한다:

```cpp
void FBridgeAssetCollector::CollectDependencies(
    const FAssetData& AssetData,
    FBridgeDependencyInfo& OutDeps)
{
    TArray<FAssetIdentifier> HardDeps, SoftDeps;
    // ... 수집 코드 ...

    // 최대 5개까지만 경로 포함, 나머지는 개수만
    OutDeps.HardCount = HardDeps.Num();
    OutDeps.SoftCount = SoftDeps.Num();

    int32 MaxDepsToInclude = 5;
    for (int32 i = 0; i < FMath::Min(HardDeps.Num(), MaxDepsToInclude); i++)
    {
        OutDeps.HardPaths.Add(HardDeps[i].ToString());
    }
    for (int32 i = 0; i < FMath::Min(SoftDeps.Num(), MaxDepsToInclude); i++)
    {
        OutDeps.SoftPaths.Add(SoftDeps[i].ToString());
    }
}
```

---

## 6. IPC Writer 책임 분리

### 6.1 모듈 구조

IPC 관련 코드를 3개 클래스로 분리한다:

```
┌───────────────────────────────────┐
│  FBridgeMmfManager                │
│  (OS 자원 관리)                   │
│                                    │
│  - MMF 생성/해제                   │
│  - Named Mutex 생성/해제           │
│  - Named Event 생성/해제           │
│  - 메모리 뷰 매핑                  │
└──────────────┬────────────────────┘
               │ 소유
               ▼
┌───────────────────────────────────┐
│  FBridgeIpcWriter                 │
│  (데이터 기록)                    │
│                                    │
│  - Header 초기화 및 갱신           │
│  - Snapshot 직렬화 및 기록         │
│  - Event 직렬화 및 Ring Buffer 기록│
│  - Heartbeat 갱신                 │
│  - Mutex 획득/해제 관리            │
└──────────────┬────────────────────┘
               │ 사용
               ▼
┌───────────────────────────────────┐
│  FBridgeDiscoveryRegistrar        │
│  (Discovery 등록)                 │
│                                    │
│  - Discovery MMF에 인스턴스 등록   │
│  - 종료 시 등록 해제               │
│  - 비활성 인스턴스 정리            │
└───────────────────────────────────┘
```

### 6.2 FBridgeMmfManager

```cpp
class FBridgeMmfManager
{
public:
    bool Create(const FString& ProjectName, uint32 ProcessId, uint32 TotalSize);
    void Destroy();

    uint8* GetBasePointer() const;
    uint32 GetTotalSize() const;
    FString GetMmfName() const;

    // 동기화 객체 접근
    bool AcquireMutex(uint32 TimeoutMs = 100);
    void ReleaseMutex();
    void SignalSnapshotEvent();
    void SignalStreamEvent();

private:
    HANDLE MmfHandle = nullptr;
    HANDLE MutexHandle = nullptr;
    HANDLE SnapshotEventHandle = nullptr;
    HANDLE StreamEventHandle = nullptr;
    uint8* MappedView = nullptr;
    uint32 TotalSize = 0;
    FString MmfName;
};
```

### 6.3 FBridgeIpcWriter

```cpp
class FBridgeIpcWriter
{
public:
    void Initialize(FBridgeMmfManager* InMmfManager);

    void WriteHeader();
    void WriteSnapshot(const FBridgeSnapshot& Snapshot);
    void WriteEvent(const FBridgeAssetEvent& Event);
    void UpdateHeartbeat();

private:
    FBridgeMmfManager* MmfManager = nullptr;
    uint64 EventSequenceNumber = 0;
    uint32 EventWriteIndex = 0;
    uint32 SnapshotVersion = 0;

    // JSON 직렬화
    FBridgeJsonSerializer Serializer;

    // CRC32 계산
    uint32 CalculateCrc32(const uint8* Data, uint32 Size);
};
```

### 6.4 FBridgeDiscoveryRegistrar

```cpp
class FBridgeDiscoveryRegistrar
{
public:
    bool Register(const FString& ProjectName, uint32 ProcessId,
                  const FString& MmfName);
    void Unregister();

    // 비활성 인스턴스 정리 (Heartbeat 기준)
    void CleanupStaleEntries(int64 MaxAgeTicks);

private:
    HANDLE DiscoveryMmfHandle = nullptr;
    HANDLE DiscoveryMutexHandle = nullptr;
    uint8* DiscoveryView = nullptr;
    int32 RegisteredEntryIndex = -1;
};
```

### 6.5 Heartbeat 타이머

GameThread 타이머를 사용하여 1초마다 Heartbeat를 갱신한다.

```cpp
void UBridgeEditorSubsystem::StartHeartbeatTimer()
{
    // Editor Tick에 바인딩 (GameThread 보장)
    TickDelegateHandle = FTSTicker::GetCoreTicker().AddTicker(
        FTickerDelegate::CreateUObject(
            this, &UBridgeEditorSubsystem::OnHeartbeatTick
        ),
        1.0f  // 1초 간격
    );
}

bool UBridgeEditorSubsystem::OnHeartbeatTick(float DeltaTime)
{
    IpcWriter->UpdateHeartbeat();
    DiscoveryRegistrar->CleanupStaleEntries(
        FTimespan::FromSeconds(30).GetTicks()  // 30초 이상 비활성 인스턴스 정리
    );
    return true;  // 계속 반복
}
```

### 6.6 스레드 안전성

| 항목 | 전략 |
|------|------|
| Snapshot 기록 | GameThread에서만 실행 (Asset Registry 콜백이 GameThread) |
| Event 기록 | GameThread에서만 실행 (Editor 델리게이트가 GameThread) |
| Heartbeat 갱신 | GameThread 타이머에서 실행 |
| MMF 접근 | Named Mutex로 프로세스 간 동기화 |

모든 Writer 동작이 GameThread에서 실행되므로, 내부적으로 추가 스레드 동기화는 불필요하다. Named Mutex는 외부 프로세스(.NET Reader)와의 동기화에만 사용된다.
