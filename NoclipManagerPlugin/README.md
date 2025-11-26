# NoclipManagerPlugin

Unified plugin for all noclip functionality: race start grace periods, collision penalties, and off-track detection.

## Configuration

The plugin is configured via a YAML file located at:

**`cfg/plugin_noclip_manager_cfg.yml`**

### How It Works

1. **Automatic Generation**: When you first start the server, AssettoServer will automatically generate a reference configuration file at `cfg/plugin_noclip_manager_cfg_reference.yml` with all default values and descriptions.

2. **Manual Creation**: Create `cfg/plugin_noclip_manager_cfg.yml` and copy settings from the reference file, or use the example below.

3. **File Location**: The config file must be in your server's `cfg/` folder (same folder as `server_cfg.ini`).

4. **Hot Reload**: Configuration changes require a server restart to take effect.

## Configuration File Structure

The configuration file has three main sections, one for each feature:

```yaml
RaceStart:
  # Race start grace period settings
  
CollisionPenalties:
  # Collision penalty system settings
  
ClientReporter:
  # Off-track detection settings
```

## Example Configuration File

Create `cfg/plugin_noclip_manager_cfg.yml`:

```yaml
# ============================================
# Race Start Grace Period Settings
# ============================================
RaceStart:
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

# ============================================
# Collision Penalty System Settings
# ============================================
CollisionPenalties:
  # Enable automatic noclip penalties for rule violations
  Enabled: true

  # Minimum relative speed (km/h) for collision to count as violation
  MinCollisionSpeedKph: 20.0

  # Minimum time between violations (seconds). Prevents rapid stacking from bouncing or multiple collisions
  MinimumViolationIntervalSeconds: 10

  # Stack 1: First offense
  Stack1NoclipSeconds: 10
  Stack1DecaySeconds: 30

  # Stack 2: Second offense within cooldown
  Stack2NoclipSeconds: 20
  Stack2DecaySeconds: 60

  # Stack 3: Third offense
  Stack3NoclipSeconds: 40
  Stack3DecaySeconds: 120

  # Stack 4: Fourth offense
  Stack4NoclipSeconds: 80
  Stack4DecaySeconds: 240

  # Stack 5: Fifth offense
  Stack5NoclipSeconds: 160
  Stack5DecaySeconds: 480

  # Chat notifications
  NotifyOnViolation: true
  NotifyOnNoclipExpire: true
  NotifyOnStackDecay: true

  # Enable name prefix display with timer and stack
  EnableNamePrefix: true

  # Name prefix format: 'compact' = [4.7s|3], 'timer' = [4.7s], 'symbols' = [4.7s] ***
  # Options: compact, timer, symbols, exclamations, warnings, minimal
  NamePrefixFormat: symbols

  # Update name prefix interval in milliseconds (100 = 0.1s)
  NameUpdateIntervalMs: 100

# ============================================
# Client Reporter (Off-Track Detection) Settings
# ============================================
ClientReporter:
  # Enable automatic noclip based on client reports (wheels off-track, wrong direction)
  Enabled: true

  # Minimum number of wheels off-track to trigger noclip (0-4)
  WheelsOutThreshold: 2

  # Send chat message when noclip is enabled
  NotifyOnEnable: false

  # Send chat message when noclip is disabled
  NotifyOnDisable: false

  # Log debug messages for each car state report
  DebugLogging: false
```

## Configuration Options Explained

### RaceStart

- **Enabled**: Enable/disable the race start grace period feature
- **EnableForQualification**: Also apply grace period to qualifying sessions
- **MinSeconds / MaxSeconds**: Random grace period duration range (each car gets a random value)
- **NotifyDriver**: Send chat message when grace period expires

### CollisionPenalties

- **Enabled**: Enable/disable collision penalty system
- **MinCollisionSpeedKph**: Minimum collision speed to count as violation (default: 20 km/h)
- **MinimumViolationIntervalSeconds**: Prevents rapid stacking from multiple collisions (default: 10s)
- **Stack1-4**: Penalty duration and decay time for each stack level
- **Stack5**: Penalty duration and decay time for stack 5
- **StackXDecaySeconds**: Time of clean driving needed to reduce stack by 1 (e.g., Stack1DecaySeconds = time to go from stack 1 to 0)
- **NotifyOnViolation**: Send message when a new violation occurs and stack increases
- **NotifyOnNoclipExpire**: Send message when noclip expires (includes remaining decay time)
- **NotifyOnStackDecay**: Send message when stack is reduced (decay)
- **EnableNamePrefix**: Show penalty timer and stack in player name
- **NamePrefixFormat**: How to display penalty info in name
- **NameUpdateIntervalMs**: How often to update the name display

### ClientReporter

- **Enabled**: Enable/disable off-track detection from client reports
- **WheelsOutThreshold**: Number of wheels off-track needed to trigger (default: 2)
- **NotifyOnEnable/Disable**: Chat notifications when noclip is enabled/disabled
- **DebugLogging**: Log every client report (useful for debugging)

## Enabling the Plugin

Add the plugin to your server configuration. In `cfg/extra_cfg.yml` or your preset's `extra_cfg.yml`:

```yaml
EnablePlugins:
  - NoclipManagerPlugin
```

## Notes

- All features can be enabled/disabled independently
- If a feature is disabled, it won't request noclip, but won't interfere with other features
- The NoclipManager coordinates all requests - no conflicts between features
- All penalties and noclip requests are cleared when session changes (practice → qualifying → race, etc.)

