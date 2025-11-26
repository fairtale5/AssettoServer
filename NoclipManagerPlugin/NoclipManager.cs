/// <summary>
/// NoclipManager - Central coordinator for all noclip requests
/// 
/// Purpose:
/// Single source of truth for collision state. All features request noclip through this manager
/// instead of calling SetCollisions() directly, preventing conflicts between multiple features.
/// 
/// How it works:
/// - Features call RequestNoclip(car, "reason") when they want collisions disabled
/// - Features call ClearNoclip(car, "reason") when they want collisions re-enabled
/// - Manager tracks all active requests per car in Dictionary<byte, HashSet<string>>
/// - If ANY request is active → collisions disabled
/// - Only when ALL requests cleared → collisions enabled
/// - Manager is the only component that calls SetCollisions()
/// 
/// Benefits:
/// - No conflicts between features (race start, penalties, off-track)
/// - Clear tracking of which feature requested noclip
/// - Easy debugging (see all active requests)
/// - Extensible (add new features without modifying existing code)
/// </summary>

using AssettoServer.Server;
using AssettoServer.Shared.Model;
using Serilog;

namespace NoclipManagerPlugin;

public class NoclipManager
{
    private readonly Dictionary<byte, HashSet<string>> _activeRequests = new();
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;

    /// <summary>
    /// Constructor - Initializes the manager
    /// 
    /// Input:
    /// - entryCarManager: Manages all cars on the server
    /// - sessionManager: Manages session state
    /// 
    /// Output:
    /// - Initialized manager ready to handle requests
    /// - Subscribes to session changes to clear all requests on session change (safety net)
    /// </summary>
    public NoclipManager(EntryCarManager entryCarManager, SessionManager sessionManager)
    {
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        
        // Subscribe to session changes - clear all noclip requests when session changes (safety net)
        _sessionManager.SessionChanged += OnSessionChanged;
    }

    /// <summary>
    /// OnSessionChanged - Handles session change events
    /// 
    /// Purpose:
    /// Called when session changes (practice → qualifying → race, etc.).
    /// Clears ALL noclip requests for ALL cars as a safety net.
    /// Note: Individual features should handle their own cleanup, but this ensures nothing is missed.
    /// 
    /// Input:
    /// - sender: SessionManager that triggered the event
    /// - args: Session change event arguments
    /// 
    /// Output:
    /// - Clears all noclip requests for all cars
    /// - Re-enables collisions for all cars
    /// - Logs the session change
    /// </summary>
    private void OnSessionChanged(SessionManager sender, SessionChangedEventArgs args)
    {
        int clearedCount = 0;
        
        // Clear all requests for all cars
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client == null)
                continue; // Skip empty slots
            
            if (_activeRequests.ContainsKey(entryCar.SessionId))
            {
                ClearAllRequests(entryCar);
                clearedCount++;
            }
        }
        
        if (clearedCount > 0)
        {
            Log.Information("NoclipManager: Session changed ({PreviousType} → {NextType}), cleared all noclip requests for {Count} cars (safety net)",
                args.PreviousSession?.Configuration.Type.ToString() ?? "None",
                args.NextSession.Configuration.Type,
                clearedCount);
        }
    }

    /// <summary>
    /// RequestNoclip - Request collisions to be disabled for a car
    /// 
    /// Purpose:
    /// Called by features when they want to disable collisions for a car.
    /// Multiple features can request noclip simultaneously - collisions stay disabled
    /// until ALL features clear their requests.
    /// 
    /// Input:
    /// - car: The EntryCar to disable collisions for
    /// - reason: Unique identifier for this request (e.g., "race_start", "penalty", "off_track")
    /// 
    /// Output:
    /// - Adds reason to active requests for this car
    /// - Calls UpdateCarCollisions() which may disable collisions if not already disabled
    /// 
    /// Example:
    /// - RaceStartFeature: RequestNoclip(car, "race_start")
    /// - PenaltiesFeature: RequestNoclip(car, "penalty")
    /// - Both active → collisions disabled
    /// </summary>
    public void RequestNoclip(EntryCar car, string reason)
    {
        if (!_activeRequests.TryGetValue(car.SessionId, out var requests))
        {
            requests = new HashSet<string>();
            _activeRequests[car.SessionId] = requests;
        }
        
        if (requests.Add(reason))
        {
            Log.Debug("NoclipManager: Request added for car {SessionId}, reason: {Reason} (total requests: {Count})",
                car.SessionId, reason, requests.Count);
            UpdateCarCollisions(car);
        }
    }

    /// <summary>
    /// ClearNoclip - Clear a noclip request for a car
    /// 
    /// Purpose:
    /// Called by features when they no longer need collisions disabled.
    /// Collisions are only re-enabled when ALL requests are cleared.
    /// 
    /// Input:
    /// - car: The EntryCar to clear the request for
    /// - reason: The unique identifier for the request to clear (must match RequestNoclip reason)
    /// 
    /// Output:
    /// - Removes reason from active requests for this car
    /// - Calls UpdateCarCollisions() which may re-enable collisions if no requests remain
    /// 
    /// Example:
    /// - RaceStartFeature: ClearNoclip(car, "race_start")
    /// - PenaltiesFeature still has active request → collisions stay disabled
    /// - PenaltiesFeature: ClearNoclip(car, "penalty")
    /// - No more requests → collisions re-enabled
    /// </summary>
    public void ClearNoclip(EntryCar car, string reason)
    {
        if (!_activeRequests.TryGetValue(car.SessionId, out var requests))
            return;
            
        if (requests.Remove(reason))
        {
            Log.Debug("NoclipManager: Request cleared for car {SessionId}, reason: {Reason} (remaining requests: {Count})",
                car.SessionId, reason, requests.Count);
            
            // Clean up empty sets
            if (requests.Count == 0)
            {
                _activeRequests.Remove(car.SessionId);
            }
            
            UpdateCarCollisions(car);
        }
    }

    /// <summary>
    /// UpdateCarCollisions - Updates collision state based on active requests
    /// 
    /// Purpose:
    /// Determines if collisions should be enabled or disabled based on active requests.
    /// Only calls SetCollisions() if the state needs to change.
    /// 
    /// Input:
    /// - car: The EntryCar to update
    /// 
    /// Output:
    /// - If ANY request is active → collisions disabled
    /// - If NO requests active → collisions enabled
    /// - Only calls SetCollisions() if state needs to change
    /// 
    /// Logic:
    /// - shouldDisable = (has active requests AND requests.Count > 0)
    /// - If car.EnableCollisions == shouldDisable → call SetCollisions(!shouldDisable)
    /// </summary>
    private void UpdateCarCollisions(EntryCar car)
    {
        bool hasActiveRequests = _activeRequests.TryGetValue(car.SessionId, out var requests) 
                                && requests != null 
                                && requests.Count > 0;
        
        bool shouldDisable = hasActiveRequests;
        
        // Only update if state needs to change
        // If collisions are enabled (true) but should be disabled → disable them
        // If collisions are disabled (false) but should be enabled → enable them
        if (car.EnableCollisions != !shouldDisable)
        {
            car.SetCollisions(!shouldDisable);
            
            if (shouldDisable)
            {
                var reasons = string.Join(", ", requests!);
                Log.Information("NoclipManager: Disabled collisions for car {SessionId} - Reasons: {Reasons}",
                    car.SessionId, reasons);
            }
            else
            {
                Log.Information("NoclipManager: Re-enabled collisions for car {SessionId}",
                    car.SessionId);
            }
        }
    }

    /// <summary>
    /// ClearAllRequests - Clears all noclip requests for a car (cleanup)
    /// 
    /// Purpose:
    /// Used when a car disconnects or session ends to clean up state.
    /// 
    /// Input:
    /// - car: The EntryCar to clear all requests for
    /// 
    /// Output:
    /// - Removes all requests for this car
    /// - Re-enables collisions if they were disabled
    /// </summary>
    public void ClearAllRequests(EntryCar car)
    {
        if (_activeRequests.Remove(car.SessionId))
        {
            UpdateCarCollisions(car);
            Log.Debug("NoclipManager: Cleared all requests for car {SessionId}", car.SessionId);
        }
    }

    /// <summary>
    /// GetActiveRequests - Gets all active request reasons for a car (for debugging)
    /// 
    /// Purpose:
    /// Returns list of active request reasons for debugging/logging purposes.
    /// 
    /// Input:
    /// - car: The EntryCar to get requests for
    /// 
    /// Output:
    /// - List of active request reasons, or empty list if none
    /// </summary>
    public IReadOnlySet<string> GetActiveRequests(EntryCar car)
    {
        if (_activeRequests.TryGetValue(car.SessionId, out var requests))
        {
            return requests;
        }
        return new HashSet<string>();
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when manager is disposed. Unsubscribes from events.
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

