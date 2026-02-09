// Copyright UnrealEditorBridge. All Rights Reserved.

#include "UnrealEditorBridgeModule.h"

#define LOCTEXT_NAMESPACE "FUnrealEditorBridgeModule"

void FUnrealEditorBridgeModule::StartupModule()
{
    UE_LOG(LogTemp, Log, TEXT("[UnrealEditorBridge] 모듈 로드됨."));
}

void FUnrealEditorBridgeModule::ShutdownModule()
{
    UE_LOG(LogTemp, Log, TEXT("[UnrealEditorBridge] 모듈 언로드됨."));
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FUnrealEditorBridgeModule, UnrealEditorBridge)
