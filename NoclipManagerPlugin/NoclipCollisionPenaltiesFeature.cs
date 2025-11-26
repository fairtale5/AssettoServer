/// <summary>
/// NoclipCollisionPenaltiesFeature - Collision-based penalty system feature
/// 
/// Purpose:
/// Automatically applies noclip penalties to players who violate server rules through collisions.
/// Uses a progressive stacking system where repeated violations result in longer noclip durations.
/// 
/// How it works:
/// 1. Listens for collision events from clients
/// 2. If collision speed > threshold, counts as violation
/// 3. Tracks violation stack (1-5+) with increasing penalties
/// 4. Uses NoclipManager to request noclip (reason: "penalty")
/// 5. Applies penalty duration based on stack level
/// 6. Stack decays over time if player drives cleanly
/// 
/// Integration:
/// - Uses NoclipManager instead of calling SetCollisions() directly
/// - Works alongside race start and off-track features without conflicts
/// </summary>

using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace NoclipManagerPlugin;

public class NoclipCollisionPenaltiesFeature
{
    private readonly NoclipManagerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly NoclipManager _noclipManager;
    private readonly Dictionary<byte, EntryCarPenalties> _trackers = new();
    private bool _namesResetForCurrentRace = false;

    /// <summary>
    /// Constructor - Initializes the collision penalties feature
    /// 
    /// Input:
    /// - configuration: Plugin configuration
    /// - entryCarManager: Manages all cars
    /// - sessionManager: Manages session state
    /// - noclipManager: Central noclip coordinator
    /// 
    /// Output:
    /// - Subscribes to client connections/disconnections
    /// - Subscribes to session changes
    /// - Ready to handle collision events
    /// </summary>
    public NoclipCollisionPenaltiesFeature(
        NoclipManagerConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        NoclipManager noclipManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _noclipManager = noclipManager;

        // Subscribe to client connections
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        
        // Subscribe to session changes to reset names before leaderboard
        _sessionManager.SessionChanged += OnSessionChanged;
        
        Log.Information("NoclipCollisionPenaltiesFeature initialized: Enabled={Enabled}, MinCollisionSpeedKph={MinCollisionSpeedKph}",
            _configuration.CollisionPenalties.Enabled,
            _configuration.CollisionPenalties.MinCollisionSpeedKph);
    }
    
    /// <summary>
    /// OnSessionChanged - Handles session change events
    /// 
    /// Purpose:
    /// Resets all penalty trackers when session changes. Cancels all timers, clears noclip requests,
    /// and resets stacks to 0. Also resets player names when leaving race session.
    /// 
    /// Input:
    /// - sender: SessionManager
    /// - args: Session change event arguments
    /// 
    /// Output:
    /// - Resets all penalty trackers (cancels timers, clears noclip, resets stacks)
    /// - Resets all player names to original if leaving race session
    /// </summary>
    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        if (!_configuration.CollisionPenalties.Enabled)
            return;

        // Reset all penalty trackers for new session
        foreach (var tracker in _trackers.Values)
        {
            tracker.ResetForNewSession();
        }
        
        Log.Information("NoclipCollisionPenaltiesFeature: Session changed ({PreviousType} → {NextType}), reset all penalties and stacks for {Count} players",
            args.PreviousSession?.Configuration.Type.ToString() ?? "None",
            args.NextSession.Configuration.Type,
            _trackers.Count);

        // Reset names when leaving a Race session (before leaderboard is shown)
        if (_configuration.CollisionPenalties.EnableNamePrefix && args.PreviousSession?.Configuration.Type == SessionType.Race)
        {
            ResetAllNamesToOriginal();
            Log.Debug("Reset all player names to original (race session ended)");
        }
        
        // Reset flag when starting a new race session
        if (args.NextSession.Configuration.Type == SessionType.Race)
        {
            _namesResetForCurrentRace = false;
        }
    }
    
    /// <summary>
    /// ResetAllNamesToOriginal - Resets all player names
    /// 
    /// Purpose:
    /// Called when race ends to restore original names before leaderboard.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Calls ResetNameToOriginal() on all trackers
    /// </summary>
    private void ResetAllNamesToOriginal()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.ResetNameToOriginal();
        }
    }

    /// <summary>
    /// OnClientConnected - Handles new client connections
    /// 
    /// Purpose:
    /// Creates penalty tracker for new client and subscribes to collision events.
    /// 
    /// Input:
    /// - client: The newly connected client
    /// - args: Event arguments
    /// 
    /// Output:
    /// - Creates EntryCarPenalties tracker
    /// - Subscribes to client.Collision event
    /// </summary>
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        if (!_configuration.CollisionPenalties.Enabled)
            return;

        // Create tracker for this car
        var tracker = new EntryCarPenalties(
            client.EntryCar,
            _configuration.CollisionPenalties,
            _sessionManager,
            _entryCarManager,
            _noclipManager);

        _trackers[client.SessionId] = tracker;

        // Subscribe to collision events
        client.Collision += (sender, collisionArgs) =>
        {
            if (_trackers.TryGetValue(sender.SessionId, out var t))
            {
                t.OnCollision(collisionArgs);
            }
        };

        Log.Information("NoclipCollisionPenaltiesFeature: Created tracker for {PlayerName} (Enabled: {Enabled})",
            client.Name,
            _configuration.CollisionPenalties.Enabled);
    }

    /// <summary>
    /// OnClientDisconnected - Handles client disconnections
    /// 
    /// Purpose:
    /// Cleans up penalty tracker when client disconnects.
    /// 
    /// Input:
    /// - client: The disconnected client
    /// - args: Event arguments
    /// 
    /// Output:
    /// - Disposes tracker
    /// - Removes from trackers dictionary
    /// </summary>
    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        if (_trackers.TryGetValue(client.SessionId, out var tracker))
        {
            tracker.Dispose();
            _trackers.Remove(client.SessionId);
            Log.Debug("Removed penalty tracker for {PlayerName}", client.Name);
        }
    }

    /// <summary>
    /// ExecuteAsync - Background monitoring loop
    /// 
    /// Purpose:
    /// Monitors for race end to reset names before leaderboard is shown.
    /// 
    /// Input:
    /// - stoppingToken: Cancellation token
    /// 
    /// Output:
    /// - Periodically checks if race ended and resets names
    /// </summary>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!_configuration.CollisionPenalties.Enabled || !_configuration.CollisionPenalties.EnableNamePrefix)
                    continue;

                // Check if race has ended and names haven't been reset yet
                if (_sessionManager.CurrentSession?.Configuration.Type == SessionType.Race
                    && _sessionManager.CurrentSession.HasSentRaceOverPacket
                    && !_namesResetForCurrentRace)
                {
                    ResetAllNamesToOriginal();
                    _namesResetForCurrentRace = true;
                    Log.Debug("Reset all player names to original (race ended, leaderboard about to show)");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in name reset monitoring");
            }
        }
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when feature is disposed. Cleans up trackers and event subscriptions.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Disposes all trackers
    /// - Unsubscribes from events
    /// </summary>
    public void Dispose()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.Dispose();
        }
        _trackers.Clear();
        _entryCarManager.ClientConnected -= OnClientConnected;
        _entryCarManager.ClientDisconnected -= OnClientDisconnected;
        _sessionManager.SessionChanged -= OnSessionChanged;
    }
}

