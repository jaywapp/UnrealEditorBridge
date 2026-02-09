// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "BridgeTypes.h"

/** Snapshot 및 Event 데이터를 JSON 문자열로 직렬화한다. */
class FBridgeJsonSerializer
{
public:
    /** Snapshot을 JSON 문자열로 직렬화한다. */
    static FString SerializeSnapshot(const FBridgeSnapshot& Snapshot);

    /** Event를 JSON 문자열로 직렬화한다. */
    static FString SerializeEvent(const FBridgeAssetEvent& Event);
};
