/// <summary>
/// NoclipPenaltyReporterConfiguration - Configuration class for NoclipPenaltyReporterPlugin
/// 
/// Purpose:
/// Defines all configurable settings for the plugin, loaded from YAML configuration file.
/// Settings are read from: cfg/plugin_noclip_penalty_reporter_cfg.yml
/// 
/// Usage:
/// - AssettoServer automatically loads this class from the YAML file
/// - Properties marked with [YamlMember] are read from the config file
/// - Default values are used if properties are not specified in YAML
/// </summary>

using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace NoclipPenaltyReporterPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class NoclipPenaltyReporterConfiguration
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

