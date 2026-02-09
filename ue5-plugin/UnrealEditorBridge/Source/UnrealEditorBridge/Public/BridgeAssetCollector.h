// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "BridgeTypes.h"

struct FAssetData;

/** Asset Registry에서 에셋 데이터를 수집한다. */
class FBridgeAssetCollector
{
public:
    /** 전체 에셋 스냅샷을 수집한다. */
    void CollectFullSnapshot(FBridgeSnapshot& OutSnapshot);

    /** 에셋의 의존성 정보를 수집한다. 최대 5개 경로까지 포함. */
    void CollectDependencies(const FAssetData& AssetData, FBridgeDependencyInfo& OutDeps);
};
