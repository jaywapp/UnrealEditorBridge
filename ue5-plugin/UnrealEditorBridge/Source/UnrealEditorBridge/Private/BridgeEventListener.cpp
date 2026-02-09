// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeEventListener.h"
#include "BridgeTypes.h"
#include "Ipc/BridgeIpcWriter.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "AssetRegistry/IAssetRegistry.h"
#include "UObject/SavePackage.h"

void FBridgeEventListener::Initialize(
    FBridgeIpcWriter* InWriter, TFunction<void()> OnEventCallback)
{
    Writer = InWriter;
    EventCallback = MoveTemp(OnEventCallback);

    IAssetRegistry& AssetRegistry =
        FModuleManager::LoadModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();

    OnAssetAddedHandle = AssetRegistry.OnAssetAdded().AddRaw(
        this, &FBridgeEventListener::OnAssetAdded);

    OnAssetRemovedHandle = AssetRegistry.OnAssetRemoved().AddRaw(
        this, &FBridgeEventListener::OnAssetRemoved);

    OnAssetRenamedHandle = AssetRegistry.OnAssetRenamed().AddRaw(
        this, &FBridgeEventListener::OnAssetRenamed);

    OnAssetUpdatedHandle = AssetRegistry.OnAssetUpdated().AddRaw(
        this, &FBridgeEventListener::OnAssetUpdated);

    OnAssetSavedHandle = UPackage::PackageSavedWithContextEvent.AddRaw(
        this, &FBridgeEventListener::OnPackageSaved);

    UE_LOG(LogTemp, Log, TEXT("[UEB] 이벤트 리스너 등록 완료"));
}

void FBridgeEventListener::Shutdown()
{
    if (FModuleManager::Get().IsModuleLoaded("AssetRegistry"))
    {
        IAssetRegistry& AssetRegistry =
            FModuleManager::GetModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();

        AssetRegistry.OnAssetAdded().Remove(OnAssetAddedHandle);
        AssetRegistry.OnAssetRemoved().Remove(OnAssetRemovedHandle);
        AssetRegistry.OnAssetRenamed().Remove(OnAssetRenamedHandle);
        AssetRegistry.OnAssetUpdated().Remove(OnAssetUpdatedHandle);
    }

    UPackage::PackageSavedWithContextEvent.Remove(OnAssetSavedHandle);

    Writer = nullptr;
    UE_LOG(LogTemp, Log, TEXT("[UEB] 이벤트 리스너 해제 완료"));
}

void FBridgeEventListener::OnAssetAdded(const FAssetData& AssetData)
{
    if (!Writer) return;

    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetCreated;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();
    Event.ClassName = AssetData.AssetClassPath.ToString();

    Writer->WriteEvent(Event);
    if (EventCallback) EventCallback();
}

void FBridgeEventListener::OnAssetRemoved(const FAssetData& AssetData)
{
    if (!Writer) return;

    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetDeleted;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();

    Writer->WriteEvent(Event);
    if (EventCallback) EventCallback();
}

void FBridgeEventListener::OnAssetRenamed(
    const FAssetData& AssetData, const FString& OldObjectPath)
{
    if (!Writer) return;

    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetRenamed;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();
    Event.OldObjectPath = OldObjectPath;

    Writer->WriteEvent(Event);
    if (EventCallback) EventCallback();
}

void FBridgeEventListener::OnAssetUpdated(const FAssetData& AssetData)
{
    if (!Writer) return;

    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetTagsChanged;
    Event.ObjectPath = AssetData.GetObjectPathString();
    Event.AssetName = AssetData.AssetName.ToString();

    Writer->WriteEvent(Event);
    if (EventCallback) EventCallback();
}

void FBridgeEventListener::OnPackageSaved(
    const FString& PackageFileName, UPackage* Package, FObjectPostSaveContext Context)
{
    if (!Writer || !Package) return;

    FBridgeAssetEvent Event;
    Event.EventType = EBridgeEventType::AssetSaved;
    Event.ObjectPath = Package->GetName();
    Event.AssetName = Package->GetFName().ToString();

    Writer->WriteEvent(Event);
    if (EventCallback) EventCallback();
}
