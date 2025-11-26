/// <summary>
/// NoclipClientReporterFeature - Client reporting feature for off-track detection
/// 
/// Purpose:
/// Receives car state reports from the NoclipPenaltyReporter client-side Lua plugin.
/// When clients detect wheels off-track or wrong direction, they send a packet.
/// This feature then requests noclip via NoclipManager.
/// 
/// How it works:
/// 1. Registers OnlineEvent handler for NoclipPenaltyReporter_CarState packets
/// 2. Receives reports from clients about their car state
/// 3. If shouldNoClip=true → Requests noclip via NoclipManager (reason: "off_track")
/// 4. If shouldNoClip=false → Clears noclip request
/// 
/// Integration:
/// - Uses NoclipManager instead of calling SetCollisions() directly
/// - Works alongside race start and penalties features without conflicts
/// </summary>

using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using NoclipManagerPlugin.Packets;
using Serilog;

namespace NoclipManagerPlugin;

public class NoclipClientReporterFeature
{
    private readonly NoclipManagerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly NoclipManager _noclipManager;

    /// <summary>
    /// Constructor - Initializes the client reporter feature
    /// 
    /// Input:
    /// - configuration: Plugin configuration
    /// - entryCarManager: Manages all cars
    /// - sessionManager: Manages session state
    /// - noclipManager: Central noclip coordinator
    /// - cspClientMessageTypeManager: Registers OnlineEvent packet handlers
    /// 
    /// Output:
    /// - Registers OnNoclipPenaltyReporterCarState as handler for packets
    /// - Subscribes to session changes to clear all off_track requests
    /// - Ready to receive client reports
    /// </summary>
    public NoclipClientReporterFeature(
        NoclipManagerConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        NoclipManager noclipManager,
        CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _noclipManager = noclipManager;

        // Register the packet handler - this connects the OnlineEvent packet to our handler function
        cspClientMessageTypeManager.RegisterOnlineEvent<NoclipPenaltyReporterCarStatePacket>(OnNoclipPenaltyReporterCarState);

        // Subscribe to session changes to clear all off_track requests
        _sessionManager.SessionChanged += OnSessionChanged;

        Log.Information("NoclipClientReporterFeature initialized: Enabled={Enabled}, WheelsOutThreshold={WheelsOutThreshold}",
            _configuration.ClientReporter.Enabled,
            _configuration.ClientReporter.WheelsOutThreshold);
    }

    /// <summary>
    /// OnSessionChanged - Handles session change events
    /// 
    /// Purpose:
    /// Called when session changes. Clears all off_track noclip requests for all cars.
    /// Clients will re-report if they're still off-track, which is fine.
    /// 
    /// Input:
    /// - sender: SessionManager that triggered the event
    /// - args: Session change event arguments
    /// 
    /// Output:
    /// - Clears all "off_track" noclip requests for all cars
    /// - Logs the session change
    /// </summary>
    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        if (!_configuration.ClientReporter.Enabled)
            return;

        int clearedCount = 0;
        
        // Clear all off_track requests for all cars
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client == null)
                continue; // Skip empty slots
            
            // Check if this car has an off_track request
            var activeRequests = _noclipManager.GetActiveRequests(entryCar);
            if (activeRequests.Contains("off_track"))
            {
                _noclipManager.ClearNoclip(entryCar, "off_track");
                clearedCount++;
            }
        }
        
        if (clearedCount > 0)
        {
            Log.Information("NoclipClientReporterFeature: Session changed ({PreviousType} → {NextType}), cleared off_track requests for {Count} cars",
                args.PreviousSession?.Configuration.Type.ToString() ?? "None",
                args.NextSession.Configuration.Type,
                clearedCount);
        }
    }

    /// <summary>
    /// OnNoclipPenaltyReporterCarState - Handles car state reports from client plugin
    /// 
    /// Purpose:
    /// Called automatically when a client sends a NoclipPenaltyReporter_CarState packet.
    /// Processes the report and requests/clears noclip via NoclipManager.
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
    /// - If shouldNoClip=true: Requests noclip via NoclipManager (reason: "off_track")
    /// - If shouldNoClip=false: Clears noclip request
    /// - Logs info message
    /// - Optionally sends chat notification to player
    /// 
    /// Side Effects:
    /// - NoclipManager handles SetCollisions() calls
    /// - AssettoServer broadcasts AS_CollisionUpdate to all clients
    /// - Client plugin shows ghost visual when collisions are disabled
    /// </summary>
    private void OnNoclipPenaltyReporterCarState(ACTcpClient sender, NoclipPenaltyReporterCarStatePacket packet)
    {
        // Early return if feature is disabled
        if (!_configuration.ClientReporter.Enabled)
            return;

        var entryCar = sender.EntryCar;

        // Debug logging (if enabled) - logs every packet received
        if (_configuration.ClientReporter.DebugLogging)
        {
            Log.Debug("NoclipClientReporter report from {Player}: wheels={WheelsOut} wrongDir={WrongDir} shouldNoClip={ShouldNoClip} reason={Reason}",
                sender.Name, packet.WheelsOut, packet.IsWrongDirection, packet.ShouldNoClip, packet.Reason);
        }

        // Use the client's recommendation (they already checked conditions)
        // The client sends shouldNoClip=true when wheels off-track OR wrong direction
        
        // Case 1: Client wants no-clip enabled
        if (packet.ShouldNoClip)
        {
            // Request noclip via manager (instead of calling SetCollisions directly)
            _noclipManager.RequestNoclip(entryCar, "off_track");
            
            Log.Information("Requested noclip for {Player} - {Reason} (wheels={WheelsOut}, wrongDir={WrongDir})",
                sender.Name, packet.Reason, packet.WheelsOut, packet.IsWrongDirection);
            
            // Optional: Send notification to player (if configured)
            if (_configuration.ClientReporter.NotifyOnEnable && sender != null)
            {
                sender.SendChatMessage($"No-clip enabled: {packet.Reason}");
            }
        }
        // Case 2: Client wants no-clip disabled
        else
        {
            // Clear the noclip request (manager will re-enable collisions if no other requests)
            _noclipManager.ClearNoclip(entryCar, "off_track");
            
            Log.Information("Cleared noclip request for {Player}",
                sender.Name);
            
            // Optional: Send notification to player (if configured)
            if (_configuration.ClientReporter.NotifyOnDisable && sender != null)
            {
                sender.SendChatMessage("Collisions re-enabled");
            }
        }
        
        // Note: The client plugin automatically shows ghost visual when NoclipManager calls
        // SetCollisions(false) because it listens for AS_CollisionUpdate packets
        // AssettoServer automatically broadcasts AS_CollisionUpdate to all clients
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when feature is disposed. Unsubscribes from events.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Unsubscribes from SessionChanged event
    /// </summary>
    public void Dispose()
    {
        _sessionManager.SessionChanged -= OnSessionChanged;
    }
}

