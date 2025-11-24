using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace NoclipCountdownPlugin;

public class NoclipCountdownModule : AssettoServerModule<NoclipCountdownConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NoclipCountdownPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}

