using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace NoclipPenaltiesPlugin;

public class EntryCarPenalties
{
    private readonly EntryCar _entryCar;
    private readonly NoclipPenaltiesConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;

    private int _currentStack = 0;
    private long _cooldownUntilMilliseconds = 0;
    private long _noclipUntilMilliseconds = 0;
    private CancellationTokenSource? _noclipTimer;
    private CancellationTokenSource? _decayTimer;
    private CancellationTokenSource? _nameUpdateTimer;
    private long _lastCleanDrivingTimeMilliseconds = 0;
    private long _lastViolationTimeMilliseconds = 0; // Track last violation time to prevent rapid stacking
    private string? _originalName;

    public EntryCarPenalties(
        EntryCar entryCar,
        NoclipPenaltiesConfiguration configuration,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        ACServerConfiguration serverConfiguration)
    {
        _entryCar = entryCar;
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
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
    
    public void OnCollision(CollisionEventArgs args)
    {
        if (!_configuration.Enabled)
            return;

        // Check if collision speed is significant enough
        if (args.Speed < _configuration.MinCollisionSpeedKph)
            return;

        // Check minimum time between violations (prevents rapid stacking from multiple collisions)
        long currentTime = _sessionManager.ServerTimeMilliseconds;
        long timeSinceLastViolation = currentTime - _lastViolationTimeMilliseconds;
        int minimumIntervalMs = _configuration.MinimumViolationIntervalSeconds * 1000;
        if (timeSinceLastViolation < minimumIntervalMs)
        {
            Log.Debug("Collision violation ignored: Player {PlayerName} violated {TimeSinceLastViolation}ms ago (minimum {MinimumInterval}ms)", 
                _entryCar.Client?.Name ?? "Unknown", timeSinceLastViolation, minimumIntervalMs);
            return;
        }

        _lastViolationTimeMilliseconds = currentTime;
        OnViolation();

        // Check if we're in cooldown period
        if (_sessionManager.ServerTimeMilliseconds < _cooldownUntilMilliseconds)
        {
            // Violation during cooldown - increase stack
            IncreaseStack();
        }
        else
        {
            // First violation after cooldown expired - start at stack 1
            SetStack(1);
        }

        ApplyNoclipPenalty();
    }

    private void SetStack(int stack)
    {
        if (_currentStack == stack)
            return;

        int previousStack = _currentStack;
        _currentStack = stack;
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;

        if (_configuration.NotifyOnStackChange && _entryCar.Client != null)
        {
            int noclipDuration = GetNoclipDuration();
            _entryCar.Client.SendChatMessage($"[⚠] Stack {stack} | {noclipDuration}s");
        }

        Log.Debug("Player {PlayerName} stack set to {Stack} (was {PreviousStack})", 
            _entryCar.Client?.Name ?? "Unknown", stack, previousStack);

        UpdateDisplayName();
        StartDecayTimer();
    }

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

        if (_configuration.NotifyOnStackChange && _entryCar.Client != null)
        {
            int noclipDuration = GetNoclipDuration();
            _entryCar.Client.SendChatMessage($"[⚠] Stack {_currentStack} | {noclipDuration}s");
        }

        Log.Debug("Player {PlayerName} stack increased from {PreviousStack} to {Stack}", 
            _entryCar.Client?.Name ?? "Unknown", previousStack, _currentStack);

        UpdateDisplayName();
        StartDecayTimer();
    }

    private void ApplyNoclipPenalty()
    {
        // Cancel existing noclip timer if any
        _noclipTimer?.Cancel();
        _noclipTimer?.Dispose();

        int noclipDuration = GetNoclipDuration();
        int cooldownDuration = GetCooldownDuration();

        // Set cooldown period
        _cooldownUntilMilliseconds = _sessionManager.ServerTimeMilliseconds + (cooldownDuration * 1000);

        // Apply noclip immediately
        _entryCar.SetCollisions(false);
        _noclipUntilMilliseconds = _sessionManager.ServerTimeMilliseconds + (noclipDuration * 1000);

        UpdateDisplayName();

        if (_configuration.NotifyOnViolation && _entryCar.Client != null)
        {
            _entryCar.Client.SendChatMessage($"Noclip penalty: {noclipDuration}s (Stack {_currentStack})");
        }

        Log.Information("Applied noclip penalty to {PlayerName}: {Duration}s, cooldown: {Cooldown}s, stack: {Stack}",
            _entryCar.Client?.Name ?? "Unknown", noclipDuration, cooldownDuration, _currentStack);

        // Schedule re-enabling collisions
        _noclipTimer = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(noclipDuration * 1000, _noclipTimer.Token);
                
                if (_entryCar.EnableCollisions == false)
                {
                    _entryCar.SetCollisions(true);
                    
                    UpdateDisplayName();
                    
                    if (_configuration.NotifyOnExpire && _entryCar.Client != null)
                    {
                        _entryCar.Client.SendChatMessage("Collisions re-enabled");
                    }
                    
                    Log.Debug("Collisions re-enabled for {PlayerName}", 
                        _entryCar.Client?.Name ?? "Unknown");
                }
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

    private int GetNoclipDuration()
    {
        return _currentStack switch
        {
            1 => _configuration.Stack1NoclipSeconds,
            2 => _configuration.Stack2NoclipSeconds,
            3 => _configuration.Stack3NoclipSeconds,
            4 => _configuration.Stack4NoclipSeconds,
            >= 5 => _configuration.MaxStackNoclipSeconds,
            _ => 0
        };
    }

    private int GetCooldownDuration()
    {
        return _currentStack switch
        {
            1 => _configuration.Stack1CooldownSeconds,
            2 => _configuration.Stack2CooldownSeconds,
            3 => _configuration.Stack3CooldownSeconds,
            4 => _configuration.Stack4CooldownSeconds,
            >= 5 => _configuration.MaxStackCooldownSeconds,
            _ => 0
        };
    }

    private int GetDecayCooldownDuration()
    {
        // Use the cooldown duration of the previous stack level
        return _currentStack switch
        {
            5 => _configuration.Stack4CooldownSeconds,  // Stack 5 → 4 uses Stack4 cooldown
            4 => _configuration.Stack3CooldownSeconds,  // Stack 4 → 3 uses Stack3 cooldown
            3 => _configuration.Stack2CooldownSeconds,   // Stack 3 → 2 uses Stack2 cooldown
            2 => _configuration.Stack1CooldownSeconds,   // Stack 2 → 1 uses Stack1 cooldown
            1 => _configuration.Stack0DecaySeconds,      // Stack 1 → 0 uses special decay time
            _ => 0
        };
    }

    private void StartDecayTimer()
    {
        // Cancel existing decay timer
        _decayTimer?.Cancel();
        _decayTimer?.Dispose();

        if (_currentStack == 0)
            return;

        int decayDuration = GetDecayCooldownDuration();
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

                        if (_configuration.NotifyOnStackChange && _entryCar.Client != null)
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

    public void OnViolation()
    {
        // Reset clean driving timer on any violation
        _lastCleanDrivingTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
        
        // Cancel decay timer since we have a new violation
        _decayTimer?.Cancel();
    }

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

    public void ResetNameToOriginal()
    {
        if (_entryCar.Client != null && _originalName != null && _entryCar.Client.Name != _originalName)
        {
            _entryCar.Client.Name = _originalName;
            BroadcastNameUpdate();
        }
    }

    public void Dispose()
    {
        _noclipTimer?.Cancel();
        _noclipTimer?.Dispose();
        _decayTimer?.Cancel();
        _decayTimer?.Dispose();
        _nameUpdateTimer?.Cancel();
        _nameUpdateTimer?.Dispose();
        
        // Restore original name on dispose
        ResetNameToOriginal();
    }
}

