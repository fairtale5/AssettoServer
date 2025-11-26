/// <summary>
/// NoclipManagerModule - Autofac dependency injection module
/// 
/// Purpose:
/// Registers the unified plugin with AssettoServer's dependency injection container.
/// This tells AssettoServer to load and run the plugin when the server starts.
/// 
/// How it works:
/// - AssettoServer scans for classes inheriting from AssettoServerModule
/// - Calls Load() method during server startup
/// - Registers NoclipManagerPlugin as a BackgroundService (runs automatically)
/// - SingleInstance ensures only one instance of the plugin exists
/// </summary>

using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace NoclipManagerPlugin;

public class NoclipManagerModule : AssettoServerModule<NoclipManagerConfiguration>
{
    /// <summary>
    /// Load - Registers plugin components with dependency injection container
    /// 
    /// Input:
    /// - builder: Autofac ContainerBuilder for registering services
    /// 
    /// Output:
    /// - Registers NoclipManagerPlugin as:
    ///   * Self (can be injected as NoclipManagerPlugin)
    ///   * IHostedService (runs as background service)
    ///   * SingleInstance (only one instance exists)
    /// </summary>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NoclipManagerPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}

