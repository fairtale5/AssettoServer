using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace NoclipCountdownPlugin;

public class NoclipCountdownPlugin : BackgroundService
{
    private readonly NoclipCountdownConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly Dictionary<byte, CancellationTokenSource> _activeTimers = new();
    private bool _isRaceInProgress = false;
    private long _raceStartTimeMilliseconds = 0;

    public NoclipCountdownPlugin(
        NoclipCountdownConfiguration configuration,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        ACServerConfiguration serverConfiguration)
    {
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _serverConfiguration = serverConfiguration;

        // Subscribe to session changes
        _sessionManager.SessionChanged += OnSessionChanged;
        
        // Subscribe to client connections for late joiners
        _entryCarManager.ClientConnected += OnClientConnected;
    }

    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        if (!_configuration.Enabled)
            return;

        var sessionType = args.NextSession.Configuration.Type;
        
        // Check if we should enable grace window for this session type
        bool shouldEnable = sessionType == SessionType.Race ||
                           (sessionType == SessionType.Qualifying && _configuration.EnableForQualification);

        if (!shouldEnable)
        {
            // If we're leaving a race/qualifying session, clean up any active timers
            if (_isRaceInProgress)
            {
                CleanupAllTimers();
                _isRaceInProgress = false;
            }
            return;
        }

        // Race or qualifying session started
        _isRaceInProgress = true;
        _raceStartTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
        
        Log.Information("Noclip countdown activated for {SessionType} session", sessionType);

        // Apply grace window to all connected cars
        ApplyCollisionGraceWindow();
    }

    private void OnClientConnected(ACTcpClient client, EventArgs eventArgs)
    {
        if (!_configuration.Enabled || !_isRaceInProgress)
            return;

        // Check if we're in a race or qualifying session
        var currentSession = _sessionManager.CurrentSession;
        if (currentSession == null)
            return;

        var sessionType = currentSession.Configuration.Type;
        bool shouldEnable = sessionType == SessionType.Race ||
                           (sessionType == SessionType.Qualifying && _configuration.EnableForQualification);

        if (!shouldEnable)
            return;

        // Late joiner - apply grace window with remaining time
        var elapsedSeconds = (int)((_sessionManager.ServerTimeMilliseconds - _raceStartTimeMilliseconds) / 1000);
        var remainingSeconds = Math.Max(0, _configuration.MaxSeconds - elapsedSeconds);
        
        if (remainingSeconds > 0)
        {
            var randomSeconds = Random.Shared.Next(
                Math.Min(_configuration.MinSeconds, remainingSeconds),
                remainingSeconds + 1
            );
            
            ApplyGraceWindowToCar(client.EntryCar, randomSeconds);
            
            Log.Debug("Applied noclip countdown to late joiner {SessionId}, remaining time: {Seconds}s", 
                client.SessionId, randomSeconds);
        }
    }

    private void ApplyCollisionGraceWindow()
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client == null)
                continue; // Skip empty slots

            // Generate random timer (40-70 seconds by default)
            int randomSeconds = Random.Shared.Next(_configuration.MinSeconds, _configuration.MaxSeconds + 1);
            
            ApplyGraceWindowToCar(entryCar, randomSeconds);
        }
    }

    private void ApplyGraceWindowToCar(EntryCar entryCar, int delaySeconds)
    {
        // Cancel any existing timer for this car
        if (_activeTimers.TryGetValue(entryCar.SessionId, out var existingTimer))
        {
            existingTimer.Cancel();
            existingTimer.Dispose();
        }

        // Disable collisions immediately
        entryCar.SetCollisions(false);
        
        Log.Debug("Noclip countdown applied to car {SessionId}, collisions will re-enable in {Seconds} seconds", 
            entryCar.SessionId, delaySeconds);

        // Create new timer for this car
        var cts = new CancellationTokenSource();
        _activeTimers[entryCar.SessionId] = cts;

        // Schedule re-enabling after delay
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delaySeconds * 1000, cts.Token);
                
                // Re-enable collisions
                entryCar.SetCollisions(true);
                
                // Notify driver if enabled
                if (_configuration.NotifyDriver && entryCar.Client != null)
                {
                    entryCar.Client.SendChatMessage("Collisions are now enabled!");
                }
                
                Log.Debug("Collisions re-enabled for car {SessionId}", entryCar.SessionId);
                
                // Clean up timer
                _activeTimers.Remove(entryCar.SessionId);
                cts.Dispose();
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled (session ended or car disconnected)
                cts.Dispose();
            }
        }, cts.Token);
    }

    private void CleanupAllTimers()
    {
        foreach (var timer in _activeTimers.Values)
        {
            timer.Cancel();
            timer.Dispose();
        }
        _activeTimers.Clear();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Plugin doesn't need a background loop, it's event-driven
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        CleanupAllTimers();
        base.Dispose();
    }
}

