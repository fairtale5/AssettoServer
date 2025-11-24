# NoclipPenaltiesPlugin

Automatically applies noclip penalties to players who violate server rules. Uses a progressive stacking system where repeated violations result in longer noclip durations and cooldown periods.

## Features

- **Progressive Penalty System**: Violations stack up, with each stack level having longer noclip duration and cooldown
- **Collision Detection**: Tracks car-to-car and car-to-environment collisions above a speed threshold
- **Stack Decay**: Clean driving reduces stack level over time
- **Configurable Timers**: Fully customizable noclip durations and cooldown periods per stack level
- **Chat Notifications**: Optional messages when penalties are applied, expire, or stack changes

## How It Works

### Violation Types

1. **Collisions**: Car-to-car or car-to-environment collisions above the configured speed threshold

### Stack System

When a player violates a rule:

1. **If in cooldown period**: Stack increases (e.g., Stack 2 → Stack 3)
2. **If cooldown expired**: Stack resets to 1

Each stack level has:
- **Noclip Duration**: How long collisions are disabled
- **Cooldown Period**: Time window where next violation increases stack

### Stack Decay

Players can reduce their stack by driving clean:
- **Stack 5 → 4**: Drive clean for Stack4 cooldown duration (300s default)
- **Stack 4 → 3**: Drive clean for Stack3 cooldown duration (120s default)
- **Stack 3 → 2**: Drive clean for Stack2 cooldown duration (60s default)
- **Stack 2 → 1**: Drive clean for Stack1 cooldown duration (30s default)
- **Stack 1 → 0**: Drive clean for Stack0 decay duration (20s default)

Any violation during decay resets the decay timer.

## Name Display Features

The plugin can display violation information directly in player names:

- **Timer Display**: Shows remaining noclip time (e.g., `[4.7s]`)
- **Stack Display**: Shows current violation stack level
- **Real-time Updates**: Updates every 0.1 seconds (configurable)
- **Multiple Formats**: Choose from compact, timer-only, symbols, or custom formats

**Example name formats:**
- `[4.7s|3] Erik Jung` - Compact format (timer + stack)
- `[4.7s] Erik Jung ***` - Timer + asterisks for stack count
- `⚠[4.7s] Erik Jung [3]` - Warning symbol + timer + stack number

## Configuration

Add to `extra_cfg.yml`:

```yaml
EnablePlugins:
  - NoclipPenaltiesPlugin
```

Create `cfg/plugin_noclip_penalties_cfg.yml`:

```yaml
# Enable automatic noclip penalties for rule violations
Enabled: true

# Minimum relative speed (km/h) for collision to count as violation
MinCollisionSpeedKph: 20.0

# Minimum time between violations (seconds). Prevents rapid stacking from bouncing or multiple collisions
MinimumViolationIntervalSeconds: 10

# Stack 1: First offense
Stack1NoclipSeconds: 5
Stack1CooldownSeconds: 30

# Stack 2: Second offense within cooldown
Stack2NoclipSeconds: 15
Stack2CooldownSeconds: 60

# Stack 3: Third offense
Stack3NoclipSeconds: 30
Stack3CooldownSeconds: 120

# Stack 4: Fourth offense
Stack4NoclipSeconds: 60
Stack4CooldownSeconds: 300

# Stack 5+: Maximum punishment
MaxStackNoclipSeconds: 120
MaxStackCooldownSeconds: 600

# Time to drop from stack 1 to 0 (clean driving)
Stack0DecaySeconds: 20

# Chat notifications
NotifyOnViolation: true
NotifyOnExpire: true
NotifyOnStackChange: true

# Enable name prefix display with timer and stack
EnableNamePrefix: true

# Name prefix format: 'compact' = [4.7s|3], 'timer' = [4.7s], 'symbols' = [4.7s] ***
NamePrefixFormat: symbols

# Update name prefix interval in milliseconds (100 = 0.1s)
NameUpdateIntervalMs: 100
```

## Configuration Options

### Basic Settings

- **Enabled**: Enable/disable the plugin
- **MinCollisionSpeedKph**: Minimum relative speed for collisions to count (default: 20 km/h)
- **MinimumViolationIntervalSeconds**: Minimum time between violations to prevent rapid stacking (default: 10 seconds)

### Stack Configuration

Each stack level (1-4) has two settings:
- **StackXNoclipSeconds**: Duration of noclip penalty
- **StackXCooldownSeconds**: Cooldown period where next violation increases stack

Stack 5+ uses:
- **MaxStackNoclipSeconds**: Maximum noclip duration
- **MaxStackCooldownSeconds**: Maximum cooldown period

### Decay Configuration

- **Stack0DecaySeconds**: Time to drop from stack 1 to 0 (clean driving)

### Notifications

- **NotifyOnViolation**: Send chat message when penalty is applied
- **NotifyOnExpire**: Send chat message when noclip expires
- **NotifyOnStackChange**: Send chat message when stack level changes

### Name Display

- **EnableNamePrefix**: Enable/disable name prefix display (default: true)
- **NamePrefixFormat**: Format style for name prefix:
  - `symbols`: `[4.7s] PlayerName ***` - Timer + asterisks for stack (default)
  - `compact`: `[4.7s|3] PlayerName` - Timer and stack in brackets
  - `timer`: `[4.7s] PlayerName` - Timer only
  - `exclamations`: `[4.7s] PlayerName !!!` - Timer + exclamation marks
  - `warnings`: `⚠[4.7s] PlayerName [3]` - Warning symbol + timer + stack
  - `minimal`: Same as compact
- **NameUpdateIntervalMs**: Update frequency in milliseconds (default: 100ms = 0.1s)

## Example Scenarios

### Scenario 1: First-Time Offender

1. Player collides with another car (speed > 20 km/h)
2. **Stack 1** applied: 5s noclip, 30s cooldown
3. Player drives clean for 20s → Stack drops to 0
4. No further penalties

### Scenario 2: Repeat Offender

1. Player collides → **Stack 1**: 5s noclip, 30s cooldown
2. Player collides again within 30s → **Stack 2**: 15s noclip, 60s cooldown
3. Player collides again within 60s → **Stack 3**: 30s noclip, 120s cooldown
4. Player collides again within 120s → **Stack 4**: 60s noclip, 240s cooldown
5. Player collides again within 240s → **Stack 5**: 120s noclip, 360s cooldown

### Scenario 3: Clean Driving Recovery

1. Player at **Stack 3** (30s noclip, 120s cooldown)
2. Player drives clean for 60s → Still Stack 3 (needs 120s to drop to Stack 2)
3. Player drives clean for another 60s → **Stack 2** (120s total clean driving)
4. Player drives clean for 30s → **Stack 1** (30s more)
5. Player drives clean for 20s → **Stack 0** (recovered)

## Requirements

- AssettoServer 0.9.0 or higher
- CSP 0.2.8 (build 3424) or higher (required for collision control)

## Technical Details

- Uses `EntryCar.SetCollisions()` to toggle collisions per car
- Subscribes to `ACTcpClient.Collision` for collision detection
- Tracks violations per car using `EntryCarPenalties` instances
- Implements progressive stacking with decay system
- Enforces minimum time interval between violations to prevent rapid stacking

## Building

See `docs/wiki/assettoserver-compilation-guide.md` for build instructions.

## License

Same as AssettoServer (MIT License)

