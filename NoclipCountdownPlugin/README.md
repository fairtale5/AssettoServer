# NoclipCountdownPlugin

Automatically disables collisions at race start and re-enables them after a random delay per car to prevent turn-one pileups.

## Features

- **Automatic collision grace window** at race start
- **Per-car randomization** - each car gets a different countdown timer (40-70 seconds by default)
- **Late joiner support** - players joining after race start get grace window based on race start time
- **Chat notifications** - optional message when collisions re-enable for each driver
- **Qualifying support** - optional grace window for qualifying sessions

## Configuration

Add to `extra_cfg.yml`:

```yaml
EnablePlugins:
  - NoclipCountdownPlugin

---
!NoclipCountdownConfiguration
# Enable automatic collision grace window at race start
Enabled: true
# Enable collision grace window at qualification start
EnableForQualification: false
# Minimum seconds before collisions re-enable (per car)
MinSeconds: 40
# Maximum seconds before collisions re-enable (per car)
MaxSeconds: 70
# Send chat message to driver when collisions re-enable
NotifyDriver: true
```

## How It Works

1. **Race Start**: When a race session begins, collisions are automatically disabled for all cars
2. **Random Countdown**: Each car gets a random timer between `MinSeconds` and `MaxSeconds`
3. **Re-enable**: When the timer expires, collisions are re-enabled for that specific car
4. **Notification**: If `NotifyDriver` is enabled, the driver receives a chat message when collisions re-enable

## Requirements

- AssettoServer 0.9.0 or higher
- CSP 0.2.8 (build 3424) or higher (required for collision control)

## Technical Details

- Uses `EntryCar.SetCollisions()` to toggle collisions per car
- Subscribes to `SessionManager.SessionChanged` for session transitions
- Subscribes to `EntryCarManager.ClientConnected` for late joiners
- Handles cleanup on session end

## Building

See `docs/wiki/assettoserver-compilation-guide.md` for build instructions.

## License

Same as AssettoServer (MIT License)

