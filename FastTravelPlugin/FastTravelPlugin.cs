using System.Numerics;
using System.Reflection;
using System.Text.Json;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Lua;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using FastTravelPlugin.Packets;
using Microsoft.Extensions.Hosting;

namespace FastTravelPlugin;

public class FastTravelPlugin : IHostedService
{
    private readonly AiSpline _aiSpline;

    public FastTravelPlugin(FastTravelConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        ILocalizationService l10n,
        AiSpline? aiSpline = null)
    {
        _aiSpline = aiSpline ?? throw new ConfigurationException("FastTravelPlugin does not work with AI traffic disabled");

        if (configuration.DisableCollisions && serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_8)
        {
            throw new ConfigurationException("FastTravelPlugin needs a minimum required CSP version of 0.2.8 (3424)");
        }

        if (!configuration.DisableCollisions && serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_0)
        {
            throw new ConfigurationException("FastTravelPlugin needs a minimum required CSP version of 0.2.0 (2651)");
        }

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("FastTravelPlugin requires ClientMessages to be enabled");
        }

        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "fasttravel");

        var luaPath = Path.Join(pluginDir, "lua", "fasttravel.lua");

        using var streamReader = new StreamReader(luaPath);
        // Localize the in-game CSP UI for the active ServerLocale before serving it.
        var fasttravelScript = LuaLocalizer.Inject(streamReader.ReadToEnd(), l10n);
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

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
