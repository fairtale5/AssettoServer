# NoclipPenaltyReporterPlugin

Server-side plugin that receives car state reports from the `NoclipPenaltyReporter` client plugin and automatically applies no-clip when players go off-track or drive in the wrong direction.

## Features

- **Automatic No-Clip**: Receives reports from client plugin about wheels off-track and wrong direction
- **Client-Side Detection**: Uses client's `wheelsOutside` detection (more accurate than server-side)
- **Visual Feedback**: Works with client plugin to show ghost visual when no-clip is active
- **Configurable**: Enable/disable notifications and debug logging

## Requirements

- **Client Plugin**: Players must have `NoclipPenaltyReporter` Lua plugin installed
- **CSP Version**: Requires CustomShadersPatch 0.2.0+ (for client plugin)
- **ClientMessages**: Must be enabled in server configuration

## Installation

1. Build the plugin (it will be placed in `out-*/plugins/NoclipPenaltyReporterPlugin/`)
2. The plugin will be automatically loaded by AssettoServer
3. Create configuration file: `cfg/plugin_noclip_penalty_reporter_cfg.yml`

## Configuration

Create `cfg/plugin_noclip_penalty_reporter_cfg.yml`:

```yaml
# Enable automatic noclip based on client reports
Enabled: true

# Minimum number of wheels off-track to trigger noclip (matches client threshold)
WheelsOutThreshold: 2

# Send chat message when noclip is enabled
NotifyOnEnable: false

# Send chat message when noclip is disabled
NotifyOnDisable: false

# Log debug messages for each car state report
DebugLogging: false
```

## How It Works

1. **Client Detection**: Client plugin checks every 0.2 seconds for:
   - Wheels off-track (`wheelsOutside > 2`)
   - Wrong direction (car facing >45° away from AI line)

2. **Client Reports**: When conditions are met, client sends `NoclipPenaltyReporter_CarState` packet to server

3. **Server Action**: Server receives packet and:
   - If `shouldNoClip=true` → Calls `entryCar.SetCollisions(false)`
   - If `shouldNoClip=false` → Calls `entryCar.SetCollisions(true)`

4. **Visual Feedback**: AssettoServer automatically broadcasts `AS_CollisionUpdate` to all clients, which triggers the client plugin to show ghost visual

## Integration with Other No-Clip Plugins

**Important**: This plugin directly calls `entryCar.SetCollisions()`, which may conflict with other no-clip plugins (`NoclipCountdownPlugin`, `NoclipPenaltiesPlugin`).

**Current Behavior**: Last plugin to call `SetCollisions()` wins. This can cause conflicts if multiple plugins try to control collisions simultaneously.

**Future Improvement**: Consider implementing a central `NoclipManager` that all plugins use to coordinate collision state.

## Packet Structure

The plugin receives `NoclipPenaltyReporter_CarState` packets with:
- `wheelsOut` (byte): Number of wheels off-track
- `isWrongDirection` (bool): Car is facing wrong direction
- `shouldNoClip` (bool): Client's recommendation (wheels off-track OR wrong direction)
- `reason` (string, 32 chars): Human-readable reason (e.g., "wheels_off_track", "wrong_direction")

## Logging

- **Info Level**: When collisions are enabled/disabled
- **Debug Level**: Each car state report (if `DebugLogging: true`)

## Troubleshooting

**No no-clip being applied?**
- Check that client plugin is installed and running
- Verify `Enabled: true` in configuration
- Check server logs for packet reception
- Enable `DebugLogging: true` to see all reports

**Visual feedback not working?**
- Ensure client plugin is installed on all clients
- Check CSP version (requires 0.2.0+)
- Verify `AS_CollisionUpdate` packets are being broadcast (check server logs)

**Conflicts with other plugins?**
- This plugin may conflict with `NoclipCountdownPlugin` and `NoclipPenaltiesPlugin`
- Consider disabling one or implementing a central noclip manager

