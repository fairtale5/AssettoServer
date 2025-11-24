using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace RuleViolationNoclipPlugin;

public class RuleViolationNoclipModule : AssettoServerModule<RuleViolationNoclipConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RuleViolationNoclipPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}

