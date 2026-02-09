// Copyright UnrealEditorBridge. All Rights Reserved.

#include "BridgeJsonSerializer.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"
#include "Serialization/JsonWriter.h"
#include "Serialization/JsonSerializer.h"

FString FBridgeJsonSerializer::SerializeSnapshot(const FBridgeSnapshot& Snapshot)
{
    TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();

    Root->SetStringField(TEXT("timestamp"), Snapshot.Timestamp.ToIso8601());
    Root->SetNumberField(TEXT("assetCount"), Snapshot.AssetCount);

    TArray<TSharedPtr<FJsonValue>> AssetsArray;
    AssetsArray.Reserve(Snapshot.Assets.Num());

    for (const FBridgeAssetInfo& Asset : Snapshot.Assets)
    {
        TSharedRef<FJsonObject> AssetObj = MakeShared<FJsonObject>();
        AssetObj->SetStringField(TEXT("objectPath"), Asset.ObjectPath);
        AssetObj->SetStringField(TEXT("packagePath"), Asset.PackagePath);
        AssetObj->SetStringField(TEXT("assetName"), Asset.AssetName);
        AssetObj->SetStringField(TEXT("className"), Asset.ClassName);

        // Tags
        TSharedRef<FJsonObject> TagsObj = MakeShared<FJsonObject>();
        for (const auto& Pair : Asset.Tags)
        {
            TagsObj->SetStringField(Pair.Key, Pair.Value);
        }
        AssetObj->SetObjectField(TEXT("tags"), TagsObj);

        // Dependencies
        TSharedRef<FJsonObject> DepsObj = MakeShared<FJsonObject>();

        TArray<TSharedPtr<FJsonValue>> HardArray;
        for (const FString& Path : Asset.Dependencies.HardPaths)
        {
            HardArray.Add(MakeShared<FJsonValueString>(Path));
        }
        DepsObj->SetArrayField(TEXT("hard"), HardArray);

        TArray<TSharedPtr<FJsonValue>> SoftArray;
        for (const FString& Path : Asset.Dependencies.SoftPaths)
        {
            SoftArray.Add(MakeShared<FJsonValueString>(Path));
        }
        DepsObj->SetArrayField(TEXT("soft"), SoftArray);

        AssetObj->SetObjectField(TEXT("dependencies"), DepsObj);

        AssetsArray.Add(MakeShared<FJsonValueObject>(AssetObj));
    }

    Root->SetArrayField(TEXT("assets"), AssetsArray);

    FString OutputString;
    TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
    FJsonSerializer::Serialize(Root, Writer);

    return OutputString;
}

FString FBridgeJsonSerializer::SerializeEvent(const FBridgeAssetEvent& Event)
{
    TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();

    Root->SetNumberField(TEXT("eventType"), static_cast<int32>(Event.EventType));
    Root->SetStringField(TEXT("objectPath"), Event.ObjectPath);
    Root->SetStringField(TEXT("timestamp"), FDateTime::UtcNow().ToIso8601());

    if (!Event.AssetName.IsEmpty())
    {
        Root->SetStringField(TEXT("assetName"), Event.AssetName);
    }

    if (!Event.ClassName.IsEmpty())
    {
        Root->SetStringField(TEXT("className"), Event.ClassName);
    }

    if (!Event.OldObjectPath.IsEmpty())
    {
        Root->SetStringField(TEXT("oldObjectPath"), Event.OldObjectPath);
    }

    if (!Event.OldAssetName.IsEmpty())
    {
        Root->SetStringField(TEXT("oldAssetName"), Event.OldAssetName);
    }

    FString OutputString;
    TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
    FJsonSerializer::Serialize(Root, Writer);

    return OutputString;
}
