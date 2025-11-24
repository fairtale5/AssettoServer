using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace RuleViolationNoclipPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RuleViolationNoclipConfiguration
{
    [YamlMember(Description = "Enable automatic noclip penalties for rule violations")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Description = "Minimum relative speed (km/h) for collision to count as violation")]
    public float MinCollisionSpeedKph { get; init; } = 20.0f;

    [YamlMember(Description = "Count corner cuts (track limit violations) as violations")]
    public bool EnableCornerCutPenalty { get; init; } = true;

    [YamlMember(Description = "Minimum number of cuts per lap to trigger penalty")]
    public int MinCutsPerLap { get; init; } = 1;

    [YamlMember(Description = "Stack 1: First offense noclip duration (seconds)")]
    public int Stack1NoclipSeconds { get; init; } = 5;

    [YamlMember(Description = "Stack 1: Cooldown period before next violation increases stack (seconds)")]
    public int Stack1CooldownSeconds { get; init; } = 30;

    [YamlMember(Description = "Stack 2: Second offense noclip duration (seconds)")]
    public int Stack2NoclipSeconds { get; init; } = 15;

    [YamlMember(Description = "Stack 2: Cooldown period before next violation increases stack (seconds)")]
    public int Stack2CooldownSeconds { get; init; } = 60;

    [YamlMember(Description = "Stack 3: Third offense noclip duration (seconds)")]
    public int Stack3NoclipSeconds { get; init; } = 30;

    [YamlMember(Description = "Stack 3: Cooldown period before next violation increases stack (seconds)")]
    public int Stack3CooldownSeconds { get; init; } = 120;

    [YamlMember(Description = "Stack 4: Fourth offense noclip duration (seconds)")]
    public int Stack4NoclipSeconds { get; init; } = 60;

    [YamlMember(Description = "Stack 4: Cooldown period before next violation increases stack (seconds)")]
    public int Stack4CooldownSeconds { get; init; } = 300;

    [YamlMember(Description = "Stack 5+: Maximum noclip duration (seconds)")]
    public int MaxStackNoclipSeconds { get; init; } = 120;

    [YamlMember(Description = "Stack 5+: Maximum cooldown period (seconds)")]
    public int MaxStackCooldownSeconds { get; init; } = 600;

    [YamlMember(Description = "Time to drop from stack 1 to 0 (clean driving, seconds)")]
    public int Stack0DecaySeconds { get; init; } = 20;

    [YamlMember(Description = "Send chat message when noclip is applied")]
    public bool NotifyOnViolation { get; init; } = true;

    [YamlMember(Description = "Send chat message when noclip expires")]
    public bool NotifyOnExpire { get; init; } = true;

    [YamlMember(Description = "Send chat message when stack level changes")]
    public bool NotifyOnStackChange { get; init; } = true;

    [YamlMember(Description = "Enable name prefix display with timer and stack")]
    public bool EnableNamePrefix { get; init; } = true;

    [YamlMember(Description = "Name prefix format: 'compact' = [4.7s|3], 'timer' = [4.7s], 'symbols' = [4.7s] ***")]
    public string NamePrefixFormat { get; init; } = "symbols";

    [YamlMember(Description = "Update name prefix interval in milliseconds (100 = 0.1s)")]
    public int NameUpdateIntervalMs { get; init; } = 100;
}

