/// <summary>
/// NoclipPenaltyReporterPlugin - Server-side plugin for automatic no-clip based on client reports
/// 
/// Purpose:
/// This plugin receives car state reports from the NoclipPenaltyReporter client-side Lua plugin.
/// When clients detect that their car has wheels off-track or is driving in the wrong direction,
/// they send a packet to the server. This plugin then automatically disables collisions for that car.
/// 
/// How it works:
/// 1. Client plugin detects conditions (wheels off-track > 2, wrong direction)
/// 2. Client sends NoclipPenaltyReporter_CarState packet to server
/// 3. Server receives packet and calls entryCar.SetCollisions(false) if shouldNoClip=true
/// 4. AssettoServer broadcasts AS_CollisionUpdate to all clients
/// 5. Client plugin shows ghost visual when collisions are disabled
/// 
/// Integration:
/// - Works with NoclipPenaltyReporter.lua client plugin
/// - May conflict with other no-clip plugins (NoclipCountdownPlugin, NoclipPenaltiesPlugin)
/// - Event-driven (no background loop needed)
/// </summary>

using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using NoclipPenaltyReporterPlugin.Packets;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace NoclipPenaltyReporterPlugin;

public class NoclipPenaltyReporterPlugin : BackgroundService
{
    private readonly NoclipPenaltyReporterConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _serverConfiguration;

    /// <summary>
    /// Constructor - Initializes the plugin and registers packet handlers
    /// 
    /// Input:
    /// - configuration: Plugin configuration (from YAML file)
    /// - entryCarManager: Manages all cars on the server
    /// - serverConfiguration: General server configuration
    /// - cspClientMessageTypeManager: Registers OnlineEvent packet handlers
    /// 
    /// Output:
    /// - Registers OnNoclipPenaltyReporterCarState as handler for NoclipPenaltyReporter_CarState packets
    /// - Logs initialization message
    /// </summary>
    public NoclipPenaltyReporterPlugin(
        NoclipPenaltyReporterConfiguration configuration,
        EntryCarManager entryCarManager,
        ACServerConfiguration serverConfiguration,
        CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _serverConfiguration = serverConfiguration;

        // Register the packet handler - this connects the OnlineEvent packet to our handler function
        cspClientMessageTypeManager.RegisterOnlineEvent<NoclipPenaltyReporterCarStatePacket>(OnNoclipPenaltyReporterCarState);

        Log.Information("NoclipPenaltyReporterPlugin initialized: Enabled={Enabled}, WheelsOutThreshold={WheelsOutThreshold}",
            _configuration.Enabled,
            _configuration.WheelsOutThreshold);
    }

    /// <summary>
    /// OnNoclipPenaltyReporterCarState - Handles car state reports from client plugin
    /// 
    /// Purpose:
    /// Called automatically when a client sends a NoclipPenaltyReporter_CarState packet.
    /// Processes the report and enables/disables collisions based on client's recommendation.
    /// 
    /// Input:
    /// - sender: The client that sent the packet (contains player info and EntryCar reference)
    /// - packet: Contains car state data:
    ///   * WheelsOut: Number of wheels off-track (0-4)
    ///   * IsWrongDirection: True if car is facing wrong direction
    ///   * ShouldNoClip: Client's recommendation (true if wheels off-track OR wrong direction)
    ///   * Reason: Human-readable reason string (e.g., "wheels_off_track", "wrong_direction")
    /// 
    /// Output:
    /// - If shouldNoClip=true AND collisions currently enabled:
    ///   * Calls entryCar.SetCollisions(false) to disable collisions
    ///   * Logs info message
    ///   * Optionally sends chat notification to player
    /// - If shouldNoClip=false AND collisions currently disabled:
    ///   * Calls entryCar.SetCollisions(true) to re-enable collisions
    ///   * Logs info message
    ///   * Optionally sends chat notification to player
    /// 
    /// Side Effects:
    /// - entryCar.SetCollisions() automatically broadcasts AS_CollisionUpdate to all clients
    /// - Client plugin receives AS_CollisionUpdate and shows/hides ghost visual
    /// </summary>
    private void OnNoclipPenaltyReporterCarState(ACTcpClient sender, NoclipPenaltyReporterCarStatePacket packet)
    {
        // Early return if plugin is disabled
        if (!_configuration.Enabled)
            return;

        var entryCar = sender.EntryCar;

        // Debug logging (if enabled) - logs every packet received
        if (_configuration.DebugLogging)
        {
            Log.Debug("NoclipPenaltyReporter report from {Player}: wheels={WheelsOut} wrongDir={WrongDir} shouldNoClip={ShouldNoClip} reason={Reason}",
                sender.Name, packet.WheelsOut, packet.IsWrongDirection, packet.ShouldNoClip, packet.Reason);
        }

        // Use the client's recommendation (they already checked conditions)
        // The client sends shouldNoClip=true when wheels off-track OR wrong direction
        
        // Case 1: Client wants no-clip enabled AND collisions are currently enabled
        if (packet.ShouldNoClip && entryCar.EnableCollisions)
        {
            // Disable collisions - this makes the car "ghost" through other cars
            entryCar.SetCollisions(false);
            
            Log.Information("Disabled collisions for {Player} - {Reason} (wheels={WheelsOut}, wrongDir={WrongDir})",
                sender.Name, packet.Reason, packet.WheelsOut, packet.IsWrongDirection);
            
            // Optional: Send notification to player (if configured)
            if (_configuration.NotifyOnEnable && sender != null)
            {
                sender.SendChatMessage($"No-clip enabled: {packet.Reason}");
            }
        }
        // Case 2: Client wants no-clip disabled AND collisions are currently disabled
        else if (!packet.ShouldNoClip && !entryCar.EnableCollisions)
        {
            // Re-enable collisions - car can now collide with others again
            entryCar.SetCollisions(true);
            
            Log.Information("Re-enabled collisions for {Player}",
                sender.Name);
            
            // Optional: Send notification to player (if configured)
            if (_configuration.NotifyOnDisable && sender != null)
            {
                sender.SendChatMessage("Collisions re-enabled");
            }
        }
        
        // Note: The client plugin automatically shows ghost visual when you call
        // entryCar.SetCollisions(false) because it listens for AS_CollisionUpdate packets
        // AssettoServer automatically broadcasts AS_CollisionUpdate to all clients
    }

    /// <summary>
    /// ExecuteAsync - Background service entry point (not used for this plugin)
    /// 
    /// Purpose:
    /// Required by BackgroundService base class, but this plugin is event-driven.
    /// All functionality happens in OnNoclipPenaltyReporterCarState when packets are received.
    /// 
    /// Input:
    /// - stoppingToken: Cancellation token (unused, plugin doesn't run a loop)
    /// 
    /// Output:
    /// - Returns completed task immediately (no background processing needed)
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Plugin doesn't need a background loop, it's event-driven
        // All work happens in OnNoclipPenaltyReporterCarState when packets arrive
        return Task.CompletedTask;
    }
}

