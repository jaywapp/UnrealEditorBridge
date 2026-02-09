// Copyright UnrealEditorBridge. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Modules/ModuleManager.h"

/** UnrealEditorBridge 모듈. Editor 전용. */
class FUnrealEditorBridgeModule : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;
};
