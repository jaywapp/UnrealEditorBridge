// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeEditorSubsystem.h"
#include "BridgeAssetCollector.h"
#include "BridgeEventListener.h"
#include "Ipc/BridgeMmfManager.h"
#include "Ipc/BridgeIpcWriter.h"
#include "Ipc/BridgeDiscoveryRegistrar.h"
#include "Ipc/ProtocolConstants.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "AssetRegistry/IAssetRegistry.h"
#include "Misc/App.h"

UBridgeEditorSubsystem::UBridgeEditorSubsystem() = default;
UBridgeEditorSubsystem::~UBridgeEditorSubsystem() = default;

void UBridgeEditorSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);

    FString ProjectName = FApp::GetProjectName();
    uint32 ProcessId = FPlatformProcess::GetCurrentProcessId();

    UE_LOG(LogTemp, Log, TEXT("[UEB] Subsystem 초기화 시작 (Project: %s, PID: %u)"),
        *ProjectName, ProcessId);

    // 1. IPC 자원 생성
    MmfManager = MakeUnique<FBridgeMmfManager>();
    if (!MmfManager->Create(ProjectName, ProcessId, BridgeProtocol::TotalMmfSize))
    {
        UE_LOG(LogTemp, Error, TEXT("[UEB] MMF 생성 실패. 플러그인 비활성화."));
        return;
    }

    // 2. IPC Writer 초기화 + Header 기록
    IpcWriter = MakeUnique<FBridgeIpcWriter>();
    IpcWriter->Initialize(MmfManager.Get());
    IpcWriter->WriteHeader();

    // 3. Discovery 등록
    DiscoveryRegistrar = MakeUnique<FBridgeDiscoveryRegistrar>();
    DiscoveryRegistrar->Register(ProjectName, ProcessId, MmfManager->GetMmfName());

    // 4. Asset Collector 생성
    AssetCollector = MakeUnique<FBridgeAssetCollector>();

    // 5. Event Listener 생성
    EventListener = MakeUnique<FBridgeEventListener>();

    // 6. Asset Registry 준비 확인
    IAssetRegistry& AssetRegistry =
        FModuleManager::LoadModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();

    if (AssetRegistry.IsLoadingAssets())
    {
        AssetRegistry.OnFilesLoaded().AddUObject(
            this, &UBridgeEditorSubsystem::OnAssetRegistryReady);
        UE_LOG(LogTemp, Log, TEXT("[UEB] Asset Registry 스캔 대기 중..."));
    }
    else
    {
        OnAssetRegistryReady();
    }

    // 7. Heartbeat 타이머 시작 (Snapshot 이전에도 연결 감지 가능)
    StartHeartbeatTimer();

    UE_LOG(LogTemp, Log, TEXT("[UEB] Subsystem 초기화 완료"));
}

void UBridgeEditorSubsystem::OnAssetRegistryReady()
{
    UE_LOG(LogTemp, Log, TEXT("[UEB] Asset Registry 준비 완료"));
    CollectAndWriteInitialSnapshot();

    // 이벤트 리스너 시작
    EventListener->Initialize(
        IpcWriter.Get(),
        [this]() { OnEventWritten(); });
}

void UBridgeEditorSubsystem::CollectAndWriteInitialSnapshot()
{
    FBridgeSnapshot Snapshot;
    AssetCollector->CollectFullSnapshot(Snapshot);
    IpcWriter->WriteSnapshot(Snapshot);
    EventsSinceLastSnapshot = 0;
}

void UBridgeEditorSubsystem::StartHeartbeatTimer()
{
    TickDelegateHandle = FTSTicker::GetCoreTicker().AddTicker(
        FTickerDelegate::CreateUObject(
            this, &UBridgeEditorSubsystem::OnHeartbeatTick),
        1.0f);  // 1초 간격
}

void UBridgeEditorSubsystem::StopHeartbeatTimer()
{
    if (TickDelegateHandle.IsValid())
    {
        FTSTicker::GetCoreTicker().RemoveTicker(TickDelegateHandle);
        TickDelegateHandle.Reset();
    }
}

bool UBridgeEditorSubsystem::OnHeartbeatTick(float DeltaTime)
{
    if (IpcWriter)
    {
        IpcWriter->UpdateHeartbeat();
    }

    if (DiscoveryRegistrar)
    {
        DiscoveryRegistrar->UpdateHeartbeat();
        DiscoveryRegistrar->CleanupStaleEntries(
            FTimespan::FromSeconds(30).GetTicks());
    }

    return true;  // 반복 계속
}

void UBridgeEditorSubsystem::OnEventWritten()
{
    EventsSinceLastSnapshot++;

    if (EventsSinceLastSnapshot >= SnapshotRebuildThreshold)
    {
        RebuildSnapshot();
    }
}

void UBridgeEditorSubsystem::RebuildSnapshot()
{
    if (!AssetCollector || !IpcWriter) return;

    FBridgeSnapshot Snapshot;
    AssetCollector->CollectFullSnapshot(Snapshot);
    IpcWriter->WriteSnapshot(Snapshot);
    EventsSinceLastSnapshot = 0;

    UE_LOG(LogTemp, Log, TEXT("[UEB] Snapshot 재생성 완료"));
}

void UBridgeEditorSubsystem::Deinitialize()
{
    UE_LOG(LogTemp, Log, TEXT("[UEB] Subsystem 종료 시작"));

    // 1. EditorShutdown 이벤트 기록
    if (IpcWriter)
    {
        FBridgeAssetEvent ShutdownEvent;
        ShutdownEvent.EventType = EBridgeEventType::EditorShutdown;
        IpcWriter->WriteEvent(ShutdownEvent);
    }

    // 2. Heartbeat 타이머 중지
    StopHeartbeatTimer();

    // 3. 이벤트 리스너 해제
    if (EventListener)
    {
        EventListener->Shutdown();
        EventListener.Reset();
    }

    // 4. Discovery 등록 해제
    if (DiscoveryRegistrar)
    {
        DiscoveryRegistrar->Unregister();
        DiscoveryRegistrar.Reset();
    }

    // 5. IPC 자원 해제
    IpcWriter.Reset();

    if (MmfManager)
    {
        MmfManager->Destroy();
        MmfManager.Reset();
    }

    AssetCollector.Reset();

    UE_LOG(LogTemp, Log, TEXT("[UEB] Subsystem 종료 완료"));

    Super::Deinitialize();
}
