/// <summary>
/// NoclipManagerConfiguration - Unified configuration for all noclip features
/// 
/// Purpose:
/// Combines configuration for all three features:
/// - RaceStart: Race start grace period
/// - CollisionPenalties: Collision-based penalty system
/// - ClientReporter: Off-track detection from client reports
/// 
/// Usage:
/// - AssettoServer automatically loads this class from YAML file
/// - Settings are read from: cfg/plugin_noclip_manager_cfg.yml
/// - Each feature section can be enabled/disabled independently
/// </summary>

using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace NoclipManagerPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoclipManagerConfiguration
{
    [YamlMember(Description = "Race start grace period settings")]
    public RaceStartConfiguration RaceStart { get; init; } = new();

    [YamlMember(Description = "Collision penalty system settings")]
    public CollisionPenaltiesConfiguration CollisionPenalties { get; init; } = new();

    [YamlMember(Description = "Client reporter (off-track detection) settings")]
    public ClientReporterConfiguration ClientReporter { get; init; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RaceStartConfiguration
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

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CollisionPenaltiesConfiguration
{
    [YamlMember(Description = "Enable automatic noclip penalties for rule violations")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Description = "Minimum relative speed (km/h) for collision to count as violation")]
    public float MinCollisionSpeedKph { get; init; } = 20.0f;

    [YamlMember(Description = "Stack 1: First offense noclip duration (seconds)")]
    public int Stack1NoclipSeconds { get; init; } = 10;

    [YamlMember(Description = "Stack 1: Time of clean driving needed to reduce stack from 1 to 0 (seconds)")]
    public int Stack1DecaySeconds { get; init; } = 30;

    [YamlMember(Description = "Stack 2: Second offense noclip duration (seconds)")]
    public int Stack2NoclipSeconds { get; init; } = 20;

    [YamlMember(Description = "Stack 2: Time of clean driving needed to reduce stack from 2 to 1 (seconds)")]
    public int Stack2DecaySeconds { get; init; } = 60;

    [YamlMember(Description = "Stack 3: Third offense noclip duration (seconds)")]
    public int Stack3NoclipSeconds { get; init; } = 40;

    [YamlMember(Description = "Stack 3: Time of clean driving needed to reduce stack from 3 to 2 (seconds)")]
    public int Stack3DecaySeconds { get; init; } = 120;

    [YamlMember(Description = "Stack 4: Fourth offense noclip duration (seconds)")]
    public int Stack4NoclipSeconds { get; init; } = 80;

    [YamlMember(Description = "Stack 4: Time of clean driving needed to reduce stack from 4 to 3 (seconds)")]
    public int Stack4DecaySeconds { get; init; } = 240;

    [YamlMember(Description = "Stack 5: Fifth offense noclip duration (seconds)")]
    public int Stack5NoclipSeconds { get; init; } = 160;

    [YamlMember(Description = "Stack 5: Time of clean driving needed to reduce stack from 5 to 4 (seconds)")]
    public int Stack5DecaySeconds { get; init; } = 480;

    [YamlMember(Description = "Send chat message when a new violation occurs and stack increases")]
    public bool NotifyOnViolation { get; init; } = true;

    [YamlMember(Description = "Send chat message when noclip expires (includes remaining decay time)")]
    public bool NotifyOnNoclipExpire { get; init; } = true;

    [YamlMember(Description = "Send chat message when stack is reduced (decay)")]
    public bool NotifyOnStackDecay { get; init; } = true;

    [YamlMember(Description = "Enable name prefix display with timer and stack")]
    public bool EnableNamePrefix { get; init; } = true;

    [YamlMember(Description = "Name prefix format: 'compact' = [4.7s|3], 'timer' = [4.7s], 'symbols' = [4.7s] ***")]
    public string NamePrefixFormat { get; init; } = "symbols";

    [YamlMember(Description = "Update name prefix interval in milliseconds (100 = 0.1s)")]
    public int NameUpdateIntervalMs { get; init; } = 100;

    [YamlMember(Description = "Minimum time between violations (seconds). Prevents rapid stacking from bouncing or multiple collisions")]
    public int MinimumViolationIntervalSeconds { get; init; } = 10;
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ClientReporterConfiguration
{
    [YamlMember(Description = "Enable automatic noclip based on client reports (wheels off-track, wrong direction)")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Description = "Minimum number of wheels off-track to trigger noclip")]
    public byte WheelsOutThreshold { get; init; } = 2;

    [YamlMember(Description = "Send chat message when noclip is enabled")]
    public bool NotifyOnEnable { get; init; } = false;

    [YamlMember(Description = "Send chat message when noclip is disabled")]
    public bool NotifyOnDisable { get; init; } = false;

    [YamlMember(Description = "Log debug messages for each car state report")]
    public bool DebugLogging { get; init; } = false;
}

