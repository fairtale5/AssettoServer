/// <summary>
/// EntryCarPenalties - Tracks penalty state for a single car
/// 
/// Purpose:
/// Manages penalty stacking, timers, and name display for one car.
/// Handles collision violations and applies progressive penalties.
/// 
/// How it works:
/// - Tracks violation stack (0-5+)
/// - Applies noclip penalty based on stack level
/// - Uses NoclipManager to request/clear noclip (reason: "penalty")
/// - Manages decay timers for stack reduction
/// - Updates player name with penalty info
/// 
/// Integration:
/// - Uses NoclipManager instead of calling SetCollisions() directly
/// - Works alongside other features without conflicts
/// </summary>

using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace NoclipManagerPlugin;

public class EntryCarPenalties
{
    private readonly EntryCar _entryCar;
    private readonly CollisionPenaltiesConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly NoclipManager _noclipManager;

    private int _currentStack = 0;
    private long _noclipUntilMilliseconds = 0;
    private CancellationTokenSource? _noclipTimer;
    private CancellationTokenSource? _decayTimer;
    private CancellationTokenSource? _nameUpdateTimer;
    private long _lastCleanDrivingTimeMilliseconds = 0;
    private long _lastViolationTimeMilliseconds = 0; // Track last violation time to prevent rapid stacking
    private string? _originalName;

    /// <summary>
    /// Constructor - Initializes penalty tracker for a car
    /// 
    /// Input:
    /// - entryCar: The car to track penalties for
    /// - configuration: Collision penalties configuration
    /// - sessionManager: Manages session state
    /// - entryCarManager: Manages all cars
    /// - noclipManager: Central noclip coordinator
    /// 
    /// Output:
    /// - Stores original player name
    /// - Starts name update timer if enabled
    /// </summary>
    public EntryCarPenalties(
        EntryCar entryCar,
        CollisionPenaltiesConfiguration configuration,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        NoclipManager noclipManager)
    {
        _entryCar = entryCar;
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _noclipManager = noclipManager;
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
        
        // Store original name
        if (_entryCar.Client != null)
        {
            _originalName = _entryCar.Client.Name;
        }
        
        // Start name update timer if enabled
        if (_configuration.EnableNamePrefix)
        {
            StartNameUpdateTimer();
        }
    }
    
    /// <summary>
    /// OnCollision - Handles collision events
    /// 
    /// Purpose:
    /// Called when car collides with another car. Checks if collision is significant
    /// enough to count as violation, then applies penalty.
    /// 
    /// Input:
    /// - args: Collision event arguments (contains speed, etc.)
    /// 
    /// Output:
    /// - If collision speed > threshold: Applies penalty
    /// - Increases stack if in cooldown period
    /// - Starts at stack 1 if cooldown expired
    /// - Calls ApplyNoclipPenalty()
    /// </summary>
    public void OnCollision(CollisionEventArgs args)
    {
        if (!_configuration.Enabled)
            return;

        // Check if collision speed is significant enough
        if (args.Speed < _configuration.MinCollisionSpeedKph)
            return;

        // Cooldown check: time since last stack increase (prevents multiple stacks from one incident)
        long currentTime = _sessionManager.ServerTimeMilliseconds;
        long timeSinceLastViolation = currentTime - _lastViolationTimeMilliseconds;
        int cooldownMs = _configuration.MinimumViolationIntervalSeconds * 1000;
        if (timeSinceLastViolation < cooldownMs)
        {
            Log.Debug("Collision violation ignored (cooldown): Player {PlayerName} violated {TimeSinceLastViolation}ms ago (cooldown {Cooldown}ms)", 
                _entryCar.Client?.Name ?? "Unknown", timeSinceLastViolation, cooldownMs);
            return;
        }

        _lastViolationTimeMilliseconds = currentTime;
        OnViolation();

        // Simple stacking: always increase stack (or set to 1 if currently 0)
        if (_currentStack == 0)
        {
            SetStack(1);
        }
        else
        {
            IncreaseStack();
        }

        ApplyNoclipPenalty();
    }

    /// <summary>
    /// SetStack - Sets the violation stack level
    /// 
    /// Purpose:
    /// Sets stack to a specific level (usually 1 for first violation).
    /// 
    /// Input:
    /// - stack: Stack level to set (1-5)
    /// 
    /// Output:
    /// - Updates _currentStack
    /// - Sends notification if enabled
    /// - Updates display name
    /// - Starts decay timer
    /// </summary>
    private void SetStack(int stack)
    {
        if (_currentStack == stack)
            return;

        int previousStack = _currentStack;
        _currentStack = stack;
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;

        // NotifyOnViolation: only when stack increases (going from 0 to 1, or any increase)
        if (_configuration.NotifyOnViolation && _entryCar.Client != null && previousStack < stack)
        {
            int noclipDuration = GetNoclipDuration();
            _entryCar.Client.SendChatMessage($"[⚠] Stack {stack} | {noclipDuration}s");
        }

        Log.Debug("Player {PlayerName} stack set to {Stack} (was {PreviousStack})", 
            _entryCar.Client?.Name ?? "Unknown", stack, previousStack);

        UpdateDisplayName();
        StartDecayTimer();
    }

    /// <summary>
    /// IncreaseStack - Increases violation stack level
    /// 
    /// Purpose:
    /// Called when violation occurs during cooldown period.
    /// Increases stack (max 5) and applies longer penalty.
    /// 
    /// Input:
    /// - None (uses _currentStack)
    /// 
    /// Output:
    /// - Increments _currentStack (max 5)
    /// - Sends notification if enabled
    /// - Updates display name
    /// - Starts decay timer
    /// </summary>
    private void IncreaseStack()
    {
        int previousStack = _currentStack;
        
        if (_currentStack >= 5)
        {
            // Already at max stack
            _currentStack = 5;
        }
        else
        {
            _currentStack++;
        }

        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;

        // NotifyOnViolation: only when stack increases
        if (_configuration.NotifyOnViolation && _entryCar.Client != null)
        {
            int noclipDuration = GetNoclipDuration();
            _entryCar.Client.SendChatMessage($"[⚠] Stack {_currentStack} | {noclipDuration}s");
        }

        Log.Debug("Player {PlayerName} stack increased from {PreviousStack} to {Stack}", 
            _entryCar.Client?.Name ?? "Unknown", previousStack, _currentStack);

        UpdateDisplayName();
        StartDecayTimer();
    }

    /// <summary>
    /// ApplyNoclipPenalty - Applies noclip penalty based on current stack
    /// 
    /// Purpose:
    /// Requests noclip via NoclipManager and schedules when to clear it.
    /// 
    /// Input:
    /// - None (uses _currentStack and _configuration)
    /// 
    /// Output:
    /// - Requests noclip via NoclipManager (reason: "penalty")
    /// - Schedules timer to clear request after penalty duration
    /// - Updates display name with penalty info
    /// - Sends notification when noclip expires (if enabled)
    /// </summary>
    private void ApplyNoclipPenalty()
    {
        // Cancel existing noclip timer if any
        _noclipTimer?.Cancel();
        _noclipTimer?.Dispose();

        int noclipDuration = GetNoclipDuration();

        // Request noclip via manager (instead of calling SetCollisions directly)
        _noclipManager.RequestNoclip(_entryCar, "penalty");
        _noclipUntilMilliseconds = _sessionManager.ServerTimeMilliseconds + (noclipDuration * 1000);

        UpdateDisplayName();

        Log.Information("Applied noclip penalty to {PlayerName}: {Duration}s, stack: {Stack}",
            _entryCar.Client?.Name ?? "Unknown", noclipDuration, _currentStack);

        // Schedule clearing the request after penalty duration
        // Capture noclipDuration for the notification (stack might change if another violation occurs)
        int capturedNoclipDuration = noclipDuration;
        _noclipTimer = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(capturedNoclipDuration * 1000, _noclipTimer.Token);
                
                // Clear the noclip request (manager will re-enable collisions if no other requests)
                _noclipManager.ClearNoclip(_entryCar, "penalty");
                
                UpdateDisplayName();
                
                // NotifyOnNoclipExpire: when noclip expires, show remaining decay time
                if (_configuration.NotifyOnNoclipExpire && _entryCar.Client != null && _currentStack > 0)
                {
                    // Calculate remaining decay time: decayDuration - noclipDuration
                    // Use the decay duration for the current stack (should be same as when penalty was applied,
                    // unless another violation occurred, but then this timer would have been cancelled)
                    int decayDuration = GetDecayDurationForCurrentStack();
                    int remainingDecaySeconds = Math.Max(0, decayDuration - capturedNoclipDuration);
                    if (remainingDecaySeconds > 0)
                    {
                        _entryCar.Client.SendChatMessage($"Collisions re-enabled | {remainingDecaySeconds}s until stack decay");
                    }
                    else
                    {
                        _entryCar.Client.SendChatMessage("Collisions re-enabled");
                    }
                }
                
                Log.Debug("Collisions re-enabled for {PlayerName}", 
                    _entryCar.Client?.Name ?? "Unknown");
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled
            }
            finally
            {
                _noclipTimer?.Dispose();
                _noclipTimer = null;
            }
        }, _noclipTimer.Token);
    }

    /// <summary>
    /// GetNoclipDuration - Gets penalty duration for current stack level
    /// 
    /// Purpose:
    /// Returns how long noclip should last based on stack level.
    /// 
    /// Input:
    /// - None (uses _currentStack and _configuration)
    /// 
    /// Output:
    /// - Returns duration in seconds based on stack level
    /// </summary>
    private int GetNoclipDuration()
    {
        return _currentStack switch
        {
            1 => _configuration.Stack1NoclipSeconds,
            2 => _configuration.Stack2NoclipSeconds,
            3 => _configuration.Stack3NoclipSeconds,
            4 => _configuration.Stack4NoclipSeconds,
            5 => _configuration.Stack5NoclipSeconds,
            _ => 0
        };
    }

    /// <summary>
    /// GetDecayDurationForCurrentStack - Gets decay duration for current stack level
    /// 
    /// Purpose:
    /// Returns how long player must drive cleanly to reduce stack by 1.
    /// This is the decay timer - if you drive cleanly for this duration, stack decreases by 1.
    /// Uses the decay duration of the current stack level (Stack1DecaySeconds, etc.).
    /// 
    /// Input:
    /// - None (uses _currentStack and _configuration)
    /// 
    /// Output:
    /// - Returns decay duration in seconds based on current stack level
    /// </summary>
    private int GetDecayDurationForCurrentStack()
    {
        // Use the decay duration of the current stack level
        return _currentStack switch
        {
            5 => _configuration.Stack5DecaySeconds,   // Stack 5 → 4 uses Stack5 decay
            4 => _configuration.Stack4DecaySeconds,   // Stack 4 → 3 uses Stack4 decay
            3 => _configuration.Stack3DecaySeconds,   // Stack 3 → 2 uses Stack3 decay
            2 => _configuration.Stack2DecaySeconds,   // Stack 2 → 1 uses Stack2 decay
            1 => _configuration.Stack1DecaySeconds,   // Stack 1 → 0 uses Stack1 decay
            _ => 0
        };
    }

    /// <summary>
    /// StartDecayTimer - Starts timer to reduce stack if player drives cleanly
    /// 
    /// Purpose:
    /// If player drives without violations for decay duration, reduces stack by 1.
    /// 
    /// Input:
    /// - None (uses _currentStack and _configuration)
    /// 
    /// Output:
    /// - Schedules timer to check if stack should decay
    /// - If clean driving continues, reduces stack and continues decay
    /// </summary>
    private void StartDecayTimer()
    {
        // Cancel existing decay timer
        _decayTimer?.Cancel();
        _decayTimer?.Dispose();

        if (_currentStack == 0)
            return;

        int decayDuration = GetDecayDurationForCurrentStack();
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;

        _decayTimer = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(decayDuration * 1000, _decayTimer.Token);
                
                // Check if still clean (no new violations)
                if (_sessionManager.ServerTimeMilliseconds - _lastCleanDrivingTimeMilliseconds >= decayDuration * 1000)
                {
                    if (_currentStack > 0)
                    {
                        int previousStack = _currentStack;
                        _currentStack--;
                        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;

                        // NotifyOnStackDecay: only when stack is reduced
                        if (_configuration.NotifyOnStackDecay && _entryCar.Client != null)
                        {
                            if (_currentStack == 0)
                            {
                                _entryCar.Client.SendChatMessage($"[Ok] Cleared!");
                            }
                            else
                            {
                                int noclipDuration = GetNoclipDuration();
                                _entryCar.Client.SendChatMessage($"[Ok] Stack {_currentStack} | {noclipDuration}s");
                            }
                        }

                        Log.Debug("Stack decayed for {PlayerName} from {PreviousStack} to {Stack}", 
                            _entryCar.Client?.Name ?? "Unknown", previousStack, _currentStack);

                        UpdateDisplayName();

                        // Continue decay if still above 0
                        if (_currentStack > 0)
                        {
                            StartDecayTimer();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled
            }
            finally
            {
                _decayTimer?.Dispose();
                _decayTimer = null;
            }
        }, _decayTimer.Token);
    }

    /// <summary>
    /// OnViolation - Called when any violation occurs
    /// 
    /// Purpose:
    /// Resets clean driving timer and cancels decay timer.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Resets _lastCleanDrivingTimeMilliseconds
    /// - Cancels decay timer
    /// </summary>
    public void OnViolation()
    {
        // Reset clean driving timer on any violation
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
        
        // Cancel decay timer since we have a new violation
        _decayTimer?.Cancel();
    }

    /// <summary>
    /// StartNameUpdateTimer - Starts periodic name update timer
    /// 
    /// Purpose:
    /// Updates player name with penalty info (timer, stack) periodically.
    /// 
    /// Input:
    /// - None (uses _configuration)
    /// 
    /// Output:
    /// - Starts background timer that calls UpdateDisplayName() periodically
    /// </summary>
    private void StartNameUpdateTimer()
    {
        if (!_configuration.EnableNamePrefix || _entryCar.Client == null)
            return;

        _nameUpdateTimer?.Cancel();
        _nameUpdateTimer?.Dispose();

        _nameUpdateTimer = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                while (!_nameUpdateTimer.Token.IsCancellationRequested)
                {
                    UpdateDisplayName();
                    await Task.Delay(_configuration.NameUpdateIntervalMs, _nameUpdateTimer.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled
            }
            finally
            {
                _nameUpdateTimer?.Dispose();
                _nameUpdateTimer = null;
            }
        }, _nameUpdateTimer.Token);
    }

    /// <summary>
    /// UpdateDisplayName - Updates player name with penalty info
    /// 
    /// Purpose:
    /// Formats and updates player name to show remaining penalty time and stack level.
    /// 
    /// Input:
    /// - None (uses _currentStack, _noclipUntilMilliseconds, _configuration)
    /// 
    /// Output:
    /// - Updates _entryCar.Client.Name with formatted penalty info
    /// - Broadcasts name update to all clients
    /// </summary>
    private void UpdateDisplayName()
    {
        if (!_configuration.EnableNamePrefix || _entryCar.Client == null || _originalName == null)
            return;

        // Restore original name if no active noclip
        if (_currentStack == 0 || _sessionManager.ServerTimeMilliseconds >= _noclipUntilMilliseconds)
        {
            if (_entryCar.Client.Name != _originalName)
            {
                _entryCar.Client.Name = _originalName;
                BroadcastNameUpdate();
            }
            return;
        }

        // Calculate remaining time
        long remainingMs = Math.Max(0, _noclipUntilMilliseconds - _sessionManager.ServerTimeMilliseconds);
        double remainingSeconds = remainingMs / 1000.0;

        // Format name based on configuration
        string displayName = _configuration.NamePrefixFormat.ToLower() switch
        {
            "compact" => $"[{remainingSeconds:F1}s|{_currentStack}] {_originalName}",
            "timer" => $"[{remainingSeconds:F1}s] {_originalName}",
            "symbols" => $"[{remainingSeconds:F1}s] {_originalName} {new string('*', _currentStack)}",
            "exclamations" => $"[{remainingSeconds:F1}s] {_originalName} {new string('!', _currentStack)}",
            "warnings" => $"⚠[{remainingSeconds:F1}s] {_originalName} [{_currentStack}]",
            "minimal" => $"[{remainingSeconds:F1}s|{_currentStack}] {_originalName}",
            _ => $"[{remainingSeconds:F1}s|{_currentStack}] {_originalName}" // Default to compact
        };

        // Update name if changed
        if (_entryCar.Client.Name != displayName)
        {
            _entryCar.Client.Name = displayName;
            BroadcastNameUpdate();
        }
    }

    /// <summary>
    /// BroadcastNameUpdate - Broadcasts name update to all clients
    /// 
    /// Purpose:
    /// Sends DriverInfoUpdate packet to refresh player names for all clients.
    /// 
    /// Input:
    /// - None (uses _entryCar and _entryCarManager)
    /// 
    /// Output:
    /// - Broadcasts DriverInfoUpdate packet to all clients
    /// </summary>
    private void BroadcastNameUpdate()
    {
        if (_entryCar.Client == null || !_entryCar.Client.HasSentFirstUpdate)
            return;

        // Broadcast DriverInfoUpdate to refresh names for all clients
        var connectedCars = _entryCarManager.EntryCars
            .Where(car => car.Client?.HasSentFirstUpdate == true)
            .Cast<AssettoServer.Shared.Model.IEntryCar<AssettoServer.Shared.Model.IClient>>();

        var packet = new DriverInfoUpdate { ConnectedCars = connectedCars };
        _entryCarManager.BroadcastPacket(packet, _entryCar.Client);
    }

    /// <summary>
    /// ResetNameToOriginal - Resets player name to original
    /// 
    /// Purpose:
    /// Called when race ends or feature is disposed.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Restores original name
    /// - Broadcasts name update
    /// </summary>
    public void ResetNameToOriginal()
    {
        if (_entryCar.Client != null && _originalName != null && _entryCar.Client.Name != _originalName)
        {
            _entryCar.Client.Name = _originalName;
            BroadcastNameUpdate();
        }
    }

    /// <summary>
    /// ResetForNewSession - Resets penalty state for a new session
    /// 
    /// Purpose:
    /// Called when session changes. Cancels all timers, clears noclip request, and resets stack to 0.
    /// This ensures clean state for the new session.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Cancels all active timers (noclip, decay, name update)
    /// - Clears noclip request via manager
    /// - Resets stack to 0
    /// - Resets cooldown and noclip timestamps
    /// - Restores original name
    /// </summary>
    public void ResetForNewSession()
    {
        // Cancel all timers
        _noclipTimer?.Cancel();
        _noclipTimer?.Dispose();
        _noclipTimer = null;
        
        _decayTimer?.Cancel();
        _decayTimer?.Dispose();
        _decayTimer = null;
        
        _nameUpdateTimer?.Cancel();
        _nameUpdateTimer?.Dispose();
        _nameUpdateTimer = null;
        
        // Clear noclip request (manager will re-enable collisions if no other requests)
        _noclipManager.ClearNoclip(_entryCar, "penalty");
        
        // Reset stack and timestamps
        _currentStack = 0;
        _noclipUntilMilliseconds = 0;
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
        _lastViolationTimeMilliseconds = 0;
        
        // Restore original name
        ResetNameToOriginal();
        
        Log.Debug("EntryCarPenalties: Reset for new session - cleared stack and all timers for {PlayerName}",
            _entryCar.Client?.Name ?? "Unknown");
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when tracker is disposed. Cleans up timers and restores name.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Cancels all timers
    /// - Clears noclip request via manager
    /// - Restores original name
    /// </summary>
    public void Dispose()
    {
        _noclipTimer?.Cancel();
        _noclipTimer?.Dispose();
        _decayTimer?.Cancel();
        _decayTimer?.Dispose();
        _nameUpdateTimer?.Cancel();
        _nameUpdateTimer?.Dispose();
        
        // Clear noclip request (manager will re-enable collisions if no other requests)
        _noclipManager.ClearNoclip(_entryCar, "penalty");
        
        // Restore original name on dispose
        ResetNameToOriginal();
    }
}

