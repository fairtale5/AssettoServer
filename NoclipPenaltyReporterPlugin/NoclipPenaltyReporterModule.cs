/// <summary>
/// NoclipPenaltyReporterModule - Autofac dependency injection module
/// 
/// Purpose:
/// Registers the plugin with AssettoServer's dependency injection container.
/// This tells AssettoServer to load and run the plugin when the server starts.
/// 
/// How it works:
/// - AssettoServer scans for classes inheriting from AssettoServerModule
/// - Calls Load() method during server startup
/// - Registers NoclipPenaltyReporterPlugin as a BackgroundService (runs automatically)
/// - SingleInstance ensures only one instance of the plugin exists
/// </summary>

using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace NoclipPenaltyReporterPlugin;

public class NoclipPenaltyReporterModule : AssettoServerModule<NoclipPenaltyReporterConfiguration>
{
    /// <summary>
    /// Load - Registers plugin components with dependency injection container
    /// 
    /// Input:
    /// - builder: Autofac ContainerBuilder for registering services
    /// 
    /// Output:
    /// - Registers NoclipPenaltyReporterPlugin as:
    ///   * Self (can be injected as NoclipPenaltyReporterPlugin)
    ///   * IHostedService (runs as background service)
    ///   * SingleInstance (only one instance exists)
    /// </summary>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NoclipPenaltyReporterPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}

