using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace NoclipCountdownPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoclipCountdownConfiguration
{
    [YamlMember(Description = "Enable automatic collision grace window at race start")]
    public bool Enabled { get; init; } = true;
    
    [YamlMember(Description = "Enable collision grace window at qualification start")]
    public bool EnableForQualification { get; init; } = false;
    
    [YamlMember(Description = "Minimum seconds before collisions re-enable (per car)")]
    public int MinSeconds { get; init; } = 40;
    
    [YamlMember(Description = "Maximum seconds before collisions re-enable (per car)")]
    public int MaxSeconds { get; init; } = 70;
    
    [YamlMember(Description = "Send chat message to driver when collisions re-enable")]
    public bool NotifyDriver { get; init; } = true;
}

