// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeAssetCollector.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "AssetRegistry/IAssetRegistry.h"

void FBridgeAssetCollector::CollectFullSnapshot(FBridgeSnapshot& OutSnapshot)
{
    IAssetRegistry& AssetRegistry =
        FModuleManager::LoadModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();

    TArray<FAssetData> AllAssets;
    AssetRegistry.GetAllAssets(AllAssets, true);

    OutSnapshot.Timestamp = FDateTime::UtcNow();
    OutSnapshot.Assets.Reserve(AllAssets.Num());

    for (const FAssetData& AssetData : AllAssets)
    {
        FString PackagePath = AssetData.PackagePath.ToString();

        // Editor 내부 에셋 제외
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

        // 태그 수집 (바이너리/대용량 태그 필터링)
        constexpr int32 MaxTagValueLength = 1024;
        AssetData.TagsAndValues.ForEach(
            [&AssetInfo](TPair<FName, FAssetTagValueRef> TagPair)
            {
                FString Value = TagPair.Value.GetValue();
                if (Value.Len() <= MaxTagValueLength)
                {
                    AssetInfo.Tags.Add(TagPair.Key.ToString(), MoveTemp(Value));
                }
            });

        // 의존성 수집
        CollectDependencies(AssetData, AssetInfo.Dependencies);

        OutSnapshot.Assets.Add(MoveTemp(AssetInfo));
    }

    OutSnapshot.AssetCount = OutSnapshot.Assets.Num();

    UE_LOG(LogTemp, Log, TEXT("[UEB] Snapshot 수집 완료: %d개 에셋"), OutSnapshot.AssetCount);
}

void FBridgeAssetCollector::CollectDependencies(
    const FAssetData& AssetData, FBridgeDependencyInfo& OutDeps)
{
    IAssetRegistry& AssetRegistry =
        FModuleManager::LoadModuleChecked<FAssetRegistryModule>("AssetRegistry").Get();

    TArray<FAssetIdentifier> HardDeps;
    TArray<FAssetIdentifier> SoftDeps;

    AssetRegistry.GetDependencies(
        FAssetIdentifier(AssetData.PackageName),
        HardDeps,
        UE::AssetRegistry::EDependencyCategory::Package,
        UE::AssetRegistry::EDependencyQuery::Hard);

    AssetRegistry.GetDependencies(
        FAssetIdentifier(AssetData.PackageName),
        SoftDeps,
        UE::AssetRegistry::EDependencyCategory::Package,
        UE::AssetRegistry::EDependencyQuery::Soft);

    OutDeps.HardCount = HardDeps.Num();
    OutDeps.SoftCount = SoftDeps.Num();

    // 최대 5개 경로만 포함
    constexpr int32 MaxDepsToInclude = 5;

    for (int32 i = 0; i < FMath::Min(HardDeps.Num(), MaxDepsToInclude); i++)
    {
        OutDeps.HardPaths.Add(HardDeps[i].ToString());
    }

    for (int32 i = 0; i < FMath::Min(SoftDeps.Num(), MaxDepsToInclude); i++)
    {
        OutDeps.SoftPaths.Add(SoftDeps[i].ToString());
    }
}
