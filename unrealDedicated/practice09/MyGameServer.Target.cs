// MyGameServer.Target.cs
using UnrealBuildTool;
using System.Collections.Generic;

public class MyGameServerTarget : TargetRules
{
    public MyGameServerTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Server;
        DefaultBuildSettings = BuildSettingsVersion.V5;          // 엔진 버전에 맞게 확인한다
        ExtraModuleNames.Add("MyGame");
    }
}
