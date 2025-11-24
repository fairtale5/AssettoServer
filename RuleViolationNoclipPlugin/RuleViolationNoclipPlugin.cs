using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RuleViolationNoclipPlugin;

public class RuleViolationNoclipPlugin : BackgroundService
{
    private readonly RuleViolationNoclipConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly Dictionary<byte, EntryCarRuleViolation> _trackers = new();
    private bool _namesResetForCurrentRace = false;

    public RuleViolationNoclipPlugin(
        RuleViolationNoclipConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;

        // Subscribe to client connections
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        
        // Subscribe to session changes to reset names before leaderboard
        _sessionManager.SessionChanged += OnSessionChanged;
    }
    
    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        if (!_configuration.Enabled || !_configuration.EnableNamePrefix)
            return;

        // Reset names when leaving a Race session (before leaderboard is shown)
        if (args.PreviousSession?.Configuration.Type == SessionType.Race)
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
    
    private void ResetAllNamesToOriginal()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.ResetNameToOriginal();
        }
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        if (!_configuration.Enabled)
            return;

        // Create tracker for this car
        var tracker = new EntryCarRuleViolation(
            client.EntryCar,
            _configuration,
            _sessionManager,
            _entryCarManager);

        _trackers[client.SessionId] = tracker;

        // Subscribe to collision events
        client.Collision += (sender, collisionArgs) =>
        {
            if (_trackers.TryGetValue(sender.SessionId, out var t))
            {
                t.OnCollision(collisionArgs);
            }
        };

        // Subscribe to lap completion events (for corner cutting)
        client.LapCompleted += (sender, lapArgs) =>
        {
            if (_trackers.TryGetValue(sender.SessionId, out var t))
            {
                t.OnLapCompleted(lapArgs);
            }
        };

        Log.Debug("Created rule violation tracker for {PlayerName}", client.Name);
    }

    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        if (_trackers.TryGetValue(client.SessionId, out var tracker))
        {
            tracker.Dispose();
            _trackers.Remove(client.SessionId);
            Log.Debug("Removed rule violation tracker for {PlayerName}", client.Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Monitor for race end to reset names before leaderboard
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!_configuration.Enabled || !_configuration.EnableNamePrefix)
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

    public override void Dispose()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.Dispose();
        }
        _trackers.Clear();
        base.Dispose();
    }
}

