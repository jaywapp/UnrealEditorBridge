// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

/** 에셋 이벤트 타입. .NET 측 AssetEventType과 동일한 값을 사용한다. */
UENUM()
enum class EBridgeEventType : uint32
{
    None                   = 0,
    AssetCreated           = 1,
    AssetDeleted           = 2,
    AssetRenamed           = 3,
    AssetSaved             = 4,
    AssetTagsChanged       = 5,
    AssetMoved             = 6,
    AssetLoaded            = 7,
    AssetDependencyChanged = 8,
    SnapshotUpdated        = 100,
    EditorShutdown         = 200
};

/** 에셋 의존성 정보. */
struct FBridgeDependencyInfo
{
    int32 HardCount = 0;
    int32 SoftCount = 0;
    TArray<FString> HardPaths;
    TArray<FString> SoftPaths;
};

/** 개별 에셋의 메타데이터. */
struct FBridgeAssetInfo
{
    FString ObjectPath;
    FString PackagePath;
    FString AssetName;
    FString ClassName;
    TMap<FString, FString> Tags;
    FBridgeDependencyInfo Dependencies;
};

/** 에셋 전체 스냅샷. */
struct FBridgeSnapshot
{
    FDateTime Timestamp;
    int32 AssetCount = 0;
    TArray<FBridgeAssetInfo> Assets;
};

/** 에셋 이벤트 데이터. */
struct FBridgeAssetEvent
{
    EBridgeEventType EventType = EBridgeEventType::None;
    FString ObjectPath;
    FString AssetName;
    FString ClassName;
    FString OldObjectPath;
    FString OldAssetName;
};
