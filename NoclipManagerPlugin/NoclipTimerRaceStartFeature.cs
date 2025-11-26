/// <summary>
/// NoclipTimerRaceStartFeature - Race start grace period feature
/// 
/// Purpose:
/// Provides collision grace period at race start. When a race/qualifying session starts,
/// all cars get collisions disabled for a random period (40-70 seconds by default).
/// This prevents first-lap incidents and gives everyone a fair start.
/// 
/// How it works:
/// 1. Listens for session changes (race/qualifying start)
/// 2. When race starts, applies random grace period to all cars
/// 3. Uses NoclipManager to request noclip (reason: "race_start")
/// 4. After grace period expires, clears the request
/// 
/// Integration:
/// - Uses NoclipManager instead of calling SetCollisions() directly
/// - Works alongside other features (penalties, off-track) without conflicts
/// </summary>

using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using Serilog;
using System.Linq;

namespace NoclipManagerPlugin;

public class NoclipTimerRaceStartFeature
{
    private readonly NoclipManagerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly NoclipManager _noclipManager;
    private readonly Dictionary<byte, CancellationTokenSource> _activeTimers = new();
    private bool _isRaceInProgress = false;
    private long _raceStartTimeMilliseconds = 0;
    private CancellationTokenSource? _raceStartScheduler;

    /// <summary>
    /// Constructor - Initializes the race start feature
    /// 
    /// Input:
    /// - configuration: Plugin configuration
    /// - sessionManager: Manages session state
    /// - entryCarManager: Manages all cars
    /// - noclipManager: Central noclip coordinator
    /// 
    /// Output:
    /// - Subscribes to session changes
    /// - Ready to handle race start events
    /// </summary>
    public NoclipTimerRaceStartFeature(
        NoclipManagerConfiguration configuration,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        NoclipManager noclipManager)
    {
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _noclipManager = noclipManager;

        // Subscribe to session changes
        _sessionManager.SessionChanged += OnSessionChanged;
    }

    /// <summary>
    /// OnSessionChanged - Handles session change events
    /// 
    /// Purpose:
    /// Called when session changes. If race/qualifying starts, schedules grace period.
    /// 
    /// Input:
    /// - sender: SessionManager that triggered the event
    /// - args: Session change event arguments
    /// 
    /// Output:
    /// - If race/qualifying starts: Schedules grace period application
    /// - If leaving race/qualifying: Cleans up timers
    /// </summary>
    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        if (!_configuration.RaceStart.Enabled)
            return;

        var sessionType = args.NextSession.Configuration.Type;
        
        // Check if we should enable grace window for this session type
        bool shouldEnable = sessionType == SessionType.Race ||
                           (sessionType == SessionType.Qualifying && _configuration.RaceStart.EnableForQualification);

        if (!shouldEnable)
        {
            // If we're leaving a race/qualifying session, clean up any active timers
            if (_isRaceInProgress)
            {
                CleanupAllTimers();
                _raceStartScheduler?.Cancel();
                _raceStartScheduler?.Dispose();
                _raceStartScheduler = null;
                _isRaceInProgress = false;
            }
            return;
        }

        // Race or qualifying session announced
        _isRaceInProgress = true;
        _raceStartTimeMilliseconds = args.NextSession.StartTimeMilliseconds;
        
        Log.Information("Race start grace period scheduled for {SessionType} session (starts in {Delay}s)", 
            sessionType, 
            (_raceStartTimeMilliseconds - _sessionManager.ServerTimeMilliseconds) / 1000);

        // Cancel any existing scheduler
        _raceStartScheduler?.Cancel();
        _raceStartScheduler?.Dispose();
        
        // Schedule collision disabling to happen when race actually starts
        _raceStartScheduler = new CancellationTokenSource();
        long delayMs = _raceStartTimeMilliseconds - _sessionManager.ServerTimeMilliseconds;
        
        if (delayMs > 0)
        {
            // Race hasn't started yet - schedule for when it does
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay((int)delayMs, _raceStartScheduler.Token);
                    
                    // Race has started - apply grace window to all cars
                    Log.Information("Race started - applying grace period to all cars");
                    ApplyCollisionGraceWindow();
                }
                catch (OperationCanceledException)
                {
                    // Scheduler was cancelled (session changed)
                }
            }, _raceStartScheduler.Token);
        }
        else
        {
            // Race already started (shouldn't happen, but handle it)
            Log.Warning("Race start time has already passed, applying grace period immediately");
            ApplyCollisionGraceWindow();
        }
    }

    /// <summary>
    /// ApplyCollisionGraceWindow - Applies grace period to all cars
    /// 
    /// Purpose:
    /// Called when race starts. Applies random grace period to each car.
    /// 
    /// Input:
    /// - None (uses _entryCarManager and _configuration)
    /// 
    /// Output:
    /// - Calls ApplyGraceWindowToCar() for each connected car
    /// </summary>
    private void ApplyCollisionGraceWindow()
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client == null)
                continue; // Skip empty slots

            // Generate random timer (40-70 seconds by default)
            int randomSeconds = Random.Shared.Next(_configuration.RaceStart.MinSeconds, _configuration.RaceStart.MaxSeconds + 1);
            
            ApplyGraceWindowToCar(entryCar, randomSeconds);
        }
    }

    /// <summary>
    /// ApplyGraceWindowToCar - Applies grace period to a single car
    /// 
    /// Purpose:
    /// Requests noclip for a car and schedules when to clear it.
    /// 
    /// Input:
    /// - entryCar: The car to apply grace period to
    /// - delaySeconds: How long the grace period should last
    /// 
    /// Output:
    /// - Requests noclip via NoclipManager (reason: "race_start")
    /// - Schedules timer to clear request after delaySeconds
    /// </summary>
    private void ApplyGraceWindowToCar(EntryCar entryCar, int delaySeconds)
    {
        // Cancel any existing timer for this car
        if (_activeTimers.TryGetValue(entryCar.SessionId, out var existingTimer))
        {
            existingTimer.Cancel();
            existingTimer.Dispose();
        }

        // Request noclip via manager (instead of calling SetCollisions directly)
        _noclipManager.RequestNoclip(entryCar, "race_start");
        
        Log.Debug("Race start grace period applied to car {SessionId}, collisions will re-enable in {Seconds} seconds", 
            entryCar.SessionId, delaySeconds);

        // Create new timer for this car
        var cts = new CancellationTokenSource();
        _activeTimers[entryCar.SessionId] = cts;

        // Schedule clearing the request after delay
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delaySeconds * 1000, cts.Token);
                
                // Clear the noclip request (manager will re-enable collisions if no other requests)
                _noclipManager.ClearNoclip(entryCar, "race_start");
                
                // Notify driver if enabled
                if (_configuration.RaceStart.NotifyDriver && entryCar.Client != null)
                {
                    entryCar.Client.SendChatMessage("Collisions are now enabled!");
                }
                
                Log.Debug("Race start grace period expired for car {SessionId}", entryCar.SessionId);
                
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

    /// <summary>
    /// CleanupAllTimers - Cleans up all active timers
    /// 
    /// Purpose:
    /// Called when session ends or feature is disposed.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Clears noclip requests for all cars that had active timers
    /// - Cancels and disposes all active timers
    /// - Clears timer dictionary
    /// </summary>
    private void CleanupAllTimers()
    {
        // Clear noclip requests for all cars that had active timers
        foreach (var sessionId in _activeTimers.Keys.ToList())
        {
            var entryCar = _entryCarManager.EntryCars.FirstOrDefault(car => car.SessionId == sessionId);
            if (entryCar != null)
            {
                _noclipManager.ClearNoclip(entryCar, "race_start");
            }
        }
        
        // Cancel and dispose all timers
        foreach (var timer in _activeTimers.Values)
        {
            timer.Cancel();
            timer.Dispose();
        }
        _activeTimers.Clear();
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when feature is disposed. Cleans up timers and event subscriptions.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Cancels all timers
    /// - Unsubscribes from events
    /// </summary>
    public void Dispose()
    {
        CleanupAllTimers();
        _raceStartScheduler?.Cancel();
        _raceStartScheduler?.Dispose();
        _sessionManager.SessionChanged -= OnSessionChanged;
    }
}

