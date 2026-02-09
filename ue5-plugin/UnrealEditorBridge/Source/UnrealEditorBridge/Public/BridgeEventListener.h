// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "UObject/ObjectSaveContext.h"

class FBridgeIpcWriter;
struct FAssetData;
class UPackage;

/** Editor 델리게이트를 감시하여 에셋 이벤트를 IPC에 기록한다. */
class FBridgeEventListener
{
public:
    /** 이벤트 리스너를 초기화하고 델리게이트를 바인딩한다. */
    void Initialize(FBridgeIpcWriter* InWriter, TFunction<void()> OnEventCallback);

    /** 모든 델리게이트를 해제한다. */
    void Shutdown();

private:
    FBridgeIpcWriter* Writer = nullptr;
    TFunction<void()> EventCallback;

    FDelegateHandle OnAssetAddedHandle;
    FDelegateHandle OnAssetRemovedHandle;
    FDelegateHandle OnAssetRenamedHandle;
    FDelegateHandle OnAssetUpdatedHandle;
    FDelegateHandle OnAssetSavedHandle;

    void OnAssetAdded(const FAssetData& AssetData);
    void OnAssetRemoved(const FAssetData& AssetData);
    void OnAssetRenamed(const FAssetData& AssetData, const FString& OldObjectPath);
    void OnAssetUpdated(const FAssetData& AssetData);
    void OnPackageSaved(const FString& PackageFileName, UPackage* Package, FObjectPostSaveContext Context);
};
