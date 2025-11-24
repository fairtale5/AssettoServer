using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace NoclipPenaltiesPlugin;

public class NoclipPenaltiesModule : AssettoServerModule<NoclipPenaltiesConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<NoclipPenaltiesPlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<EntryCarPenalties>().AsSelf();
    }
}

