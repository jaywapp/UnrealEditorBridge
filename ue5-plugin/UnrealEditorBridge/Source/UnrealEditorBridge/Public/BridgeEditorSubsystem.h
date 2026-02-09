// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "EditorSubsystem.h"
#include "Containers/Ticker.h"
#include "Ipc/BridgeMmfManager.h"
#include "Ipc/BridgeIpcWriter.h"
#include "Ipc/BridgeDiscoveryRegistrar.h"
#include "BridgeAssetCollector.h"
#include "BridgeEventListener.h"
#include "BridgeEditorSubsystem.generated.h"

/**
 * Editor Subsystem. IPC 자원 생성, Snapshot/Event 기록,
 * Heartbeat 관리 등 전체 플러그인 라이프사이클을 조율한다.
 */
UCLASS()
class UBridgeEditorSubsystem : public UEditorSubsystem
{
    GENERATED_BODY()

public:
    UBridgeEditorSubsystem();
    ~UBridgeEditorSubsystem();

    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

private:
    TUniquePtr<FBridgeMmfManager> MmfManager;
    TUniquePtr<FBridgeIpcWriter> IpcWriter;
    TUniquePtr<FBridgeDiscoveryRegistrar> DiscoveryRegistrar;
    TUniquePtr<FBridgeAssetCollector> AssetCollector;
    TUniquePtr<FBridgeEventListener> EventListener;

    FTSTicker::FDelegateHandle TickDelegateHandle;
    int32 EventsSinceLastSnapshot = 0;
    static constexpr int32 SnapshotRebuildThreshold = 100;

    /** Asset Registry 준비 완료 콜백. */
    void OnAssetRegistryReady();

    /** 초기 전체 Snapshot 수집 및 기록. */
    void CollectAndWriteInitialSnapshot();

    /** Heartbeat 타이머를 시작한다. */
    void StartHeartbeatTimer();

    /** Heartbeat 타이머를 중지한다. */
    void StopHeartbeatTimer();

    /** 1초마다 호출되는 Heartbeat 틱 콜백. */
    bool OnHeartbeatTick(float DeltaTime);

    /** 이벤트 기록 후 호출. Snapshot 재생성 조건 검사. */
    void OnEventWritten();

    /** Snapshot을 전체 재생성한다. */
    void RebuildSnapshot();
};
