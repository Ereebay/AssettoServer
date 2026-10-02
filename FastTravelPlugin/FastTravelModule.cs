using AssettoServer.Server.Plugin;
using Autofac;

namespace FastTravelPlugin;

// 0.0.54 port: registers as IAssettoServerAutostart (0.0.54 lifecycle) instead of IHostedService,
// and drops the ReferenceConfiguration override (not available in 0.0.54's AssettoServerModule).
public class FastTravelModule : AssettoServerModule<FastTravelConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<FastTravelPlugin>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
