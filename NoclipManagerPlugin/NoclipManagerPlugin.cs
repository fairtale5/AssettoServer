/// <summary>
/// NoclipManagerPlugin - Unified plugin for all noclip features
/// 
/// Purpose:
/// Combines three noclip features into one plugin:
/// - RaceStart: Grace period at race start
/// - CollisionPenalties: Progressive penalty system for collisions
/// - ClientReporter: Off-track detection from client reports
/// 
/// How it works:
/// - Creates NoclipManager (central coordinator)
/// - Initializes all three features
/// - Features request/clear noclip via manager instead of calling SetCollisions() directly
/// - Manager is single source of truth for collision state
/// - No conflicts between features
/// 
/// Benefits:
/// - Single plugin for all noclip functionality
/// - No conflicts (manager coordinates everything)
/// - Easier to maintain (all code in one place)
/// - Simpler for users (one download, one config)
/// </summary>

using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace NoclipManagerPlugin;

public class NoclipManagerPlugin : BackgroundService
{
    private readonly NoclipManagerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly CSPClientMessageTypeManager _cspClientMessageTypeManager;
    
    private readonly NoclipManager _noclipManager;
    private readonly NoclipTimerRaceStartFeature? _raceStartFeature;
    private readonly NoclipCollisionPenaltiesFeature? _collisionPenaltiesFeature;
    private readonly NoclipClientReporterFeature? _clientReporterFeature;

    /// <summary>
    /// Constructor - Initializes the unified plugin
    /// 
    /// Purpose:
    /// Creates NoclipManager and initializes all three features.
    /// Each feature is only created if its configuration is enabled.
    /// 
    /// Input:
    /// - configuration: Unified plugin configuration
    /// - entryCarManager: Manages all cars
    /// - sessionManager: Manages session state
    /// - serverConfiguration: General server configuration
    /// - cspClientMessageTypeManager: Registers OnlineEvent packet handlers
    /// 
    /// Output:
    /// - Creates NoclipManager (always created)
    /// - Creates enabled features
    /// - Logs initialization message
    /// </summary>
    public NoclipManagerPlugin(
        NoclipManagerConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        ACServerConfiguration serverConfiguration,
        CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _serverConfiguration = serverConfiguration;
        _cspClientMessageTypeManager = cspClientMessageTypeManager;

        // Create central noclip manager (always created)
        _noclipManager = new NoclipManager(_entryCarManager, _sessionManager);

        // Initialize race start feature (if enabled)
        if (_configuration.RaceStart.Enabled)
        {
            _raceStartFeature = new NoclipTimerRaceStartFeature(
                _configuration,
                _sessionManager,
                _entryCarManager,
                _noclipManager);
            Log.Information("NoclipManagerPlugin: RaceStart feature enabled");
        }

        // Initialize collision penalties feature (if enabled)
        if (_configuration.CollisionPenalties.Enabled)
        {
            _collisionPenaltiesFeature = new NoclipCollisionPenaltiesFeature(
                _configuration,
                _entryCarManager,
                _sessionManager,
                _noclipManager);
            Log.Information("NoclipManagerPlugin: CollisionPenalties feature enabled");
        }

        // Initialize client reporter feature (if enabled)
        if (_configuration.ClientReporter.Enabled)
        {
            _clientReporterFeature = new NoclipClientReporterFeature(
                _configuration,
                _entryCarManager,
                _sessionManager,
                _noclipManager,
                _cspClientMessageTypeManager);
            Log.Information("NoclipManagerPlugin: ClientReporter feature enabled");
        }

        Log.Information("NoclipManagerPlugin initialized with {FeatureCount} features enabled",
            (_raceStartFeature != null ? 1 : 0) +
            (_collisionPenaltiesFeature != null ? 1 : 0) +
            (_clientReporterFeature != null ? 1 : 0));
    }

    /// <summary>
    /// ExecuteAsync - Background service entry point
    /// 
    /// Purpose:
    /// Runs background monitoring loop for collision penalties feature (name reset monitoring).
    /// 
    /// Input:
    /// - stoppingToken: Cancellation token
    /// 
    /// Output:
    /// - Runs ExecuteAsync() on collision penalties feature if enabled
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run background loop for collision penalties feature (if enabled)
        if (_collisionPenaltiesFeature != null)
        {
            await _collisionPenaltiesFeature.ExecuteAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Dispose - Cleans up resources
    /// 
    /// Purpose:
    /// Called when plugin is disposed. Cleans up all features.
    /// 
    /// Input:
    /// - None
    /// 
    /// Output:
    /// - Disposes all features
    /// </summary>
    public override void Dispose()
    {
        _raceStartFeature?.Dispose();
        _collisionPenaltiesFeature?.Dispose();
        _clientReporterFeature?.Dispose();
        _noclipManager?.Dispose();
        base.Dispose();
    }
}

