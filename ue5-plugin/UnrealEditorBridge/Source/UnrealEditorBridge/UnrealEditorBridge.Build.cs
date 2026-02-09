using UnrealBuildTool;

public class UnrealEditorBridge : ModuleRules
{
    public UnrealEditorBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "AssetRegistry",
            "UnrealEd",
            "EditorSubsystem"
        });

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Json",
            "JsonUtilities",
            "Projects"
        });
    }
}
