using System.Numerics;
using System.Reflection;
using System.Text.Json;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using FastTravelPlugin.Packets;
using Microsoft.Extensions.Hosting;

namespace FastTravelPlugin;

// 0.0.54 port of upstream master's FastTravelPlugin:
//  - IHostedService -> CriticalBackgroundService + IAssettoServerAutostart (0.0.54 plugin lifecycle)
//  - CSPVersion.V0_2_8 does not exist on 0.0.54 -> raw constant 3424
//  - everything else is unchanged from upstream (script config injection works, AddScript
//    already has the Dictionary overload on 0.0.54)
public class FastTravelPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly AiSpline _aiSpline;

    public FastTravelPlugin(FastTravelConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        IHostApplicationLifetime applicationLifetime,
        AiSpline? aiSpline = null) : base(applicationLifetime)
    {
        _aiSpline = aiSpline ?? throw new ConfigurationException("FastTravelPlugin does not work with AI traffic disabled");

        // CSP 0.2.8 (3424) fixed disabling collisions online; 0.0.54's CSPVersion has no constant for it yet.
        const int cspVersionV0_2_8 = 3424;
        var requiredVersion = configuration.DisableCollisions ? cspVersionV0_2_8 : CSPVersion.V0_2_0;
        var requiredVersionString = configuration.DisableCollisions ? "0.2.8 (3424)" : "0.2.0 (2651)";
        var minimumVersion = serverConfiguration.CSPTrackOptions.MinimumCSPVersion ?? throw new ConfigurationException($"FastTravelPlugin needs a minimum required CSP version of {requiredVersionString}");

        if (minimumVersion < requiredVersion)
        {
            throw new ConfigurationException($"FastTravelPlugin needs a minimum required CSP version of {requiredVersionString}");
        }

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("FastTravelPlugin requires ClientMessages to be enabled");
        }

        var luaPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", "fasttravel.lua");

        using var streamReader = new StreamReader(luaPath);
        var fasttravelScript = streamReader.ReadToEnd();
        scriptProvider.AddScript(fasttravelScript, "fasttravel.lua", new Dictionary<string, object>
        {
            ["mapFixedTargetPosition"] = $"\"{JsonSerializer.Serialize(configuration.MapFixedTargetPosition)}\"",
            ["mapZoomValues"] = $"\"{JsonSerializer.Serialize(configuration.MapZoomValues)}\"",
            ["mapMoveSpeeds"] = $"\"{JsonSerializer.Serialize(configuration.MapMoveSpeeds)}\"",
            ["showMapImg"] = configuration.ShowMapImage ? "true" : "false",
            ["disableCollisions"] = configuration.DisableCollisions ? "true" : "false",
            ["hideUntypedPoints"] = configuration.HideUntypedPoints ? "true" : "false",
            ["useGroupDrawMode"] = configuration.UseGroupDrawMode ? "true" : "false",
            ["distanceModeRange"] = configuration.DistanceModeRange
        });

        cspClientMessageTypeManager.RegisterOnlineEvent<FastTravelPacket>(OnFastTravelPacket);
    }

    private void OnFastTravelPacket(ACTcpClient client, FastTravelPacket packet)
    {
        var (splinePointId, _) = _aiSpline.WorldToSpline(packet.Position);

        var splinePoint = _aiSpline.Points[splinePointId];

        var direction = -_aiSpline.Operations.GetForwardVector(splinePoint.Id);
        if (direction == Vector3.Zero)
            direction = new Vector3(1, 0, 0);

        client.SendPacket(new FastTravelPacket
        {
            Position = packet.Position,
            Direction = direction,
            SessionId = 255
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
