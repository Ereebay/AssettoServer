using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Weather;
using Qmmands;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather.Implementation;
using AssettoServer.Server.Whitelist;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AdminModule : ACModuleBase
{
    private readonly IWeatherImplementation _weatherImplementation;
    private readonly WeatherManager _weatherManager;
    private readonly DefaultWeatherProvider _weatherProvider;
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly IWhitelistService _whitelist;
    private readonly ILocalizationService _l10n;

    public AdminModule(IWeatherImplementation weatherImplementation, WeatherManager weatherManager, DefaultWeatherProvider weatherProvider, ACServerConfiguration configuration, SessionManager sessionManager, EntryCarManager entryCarManager, IWhitelistService whitelist, ILocalizationService l10n)
    {
        _weatherImplementation = weatherImplementation;
        _weatherManager = weatherManager;
        _weatherProvider = weatherProvider;
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _whitelist = whitelist;
        _l10n = l10n;
    }

    [Command("kick", "kick_id")]
    public Task KickAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Client?.SessionId)
            Reply(_l10n.Get("cmd.kick.cannot_self"));
        else if (player.IsAdministrator)
            Reply(_l10n.Get("cmd.kick.cannot_admin"));
        else
        {
            Reply(_l10n.Get("cmd.kick.steam_profile", new { name = player.Name, guid = player.Guid }));
            return _entryCarManager.KickAsync(player, reason, Client);
        }

        return Task.CompletedTask;
    }

    [Command("ban", "ban_id")]
    public Task BanAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Client?.SessionId)
            Reply(_l10n.Get("cmd.ban.cannot_self"));
        else if (player.IsAdministrator)
            Reply(_l10n.Get("cmd.ban.cannot_admin"));
        else
        {
            Reply(_l10n.Get("cmd.ban.steam_profile", new { name = player.Name, guid = player.Guid }));
            if (player.OwnerGuid.HasValue && player.Guid != player.OwnerGuid)
            {
                Reply(_l10n.Get("cmd.ban.family_sharing_notice", new { name = player.Name, guid = player.OwnerGuid }));
            }
            return _entryCarManager.BanAsync(player, reason, Client);
        }

        return Task.CompletedTask;
    }

    [Command("pit")]
    public void TeleportToPits([Remainder] ACTcpClient player)
    {
        _sessionManager.SendCurrentSession(player);
        player.SendPacket(new ChatMessage { SessionId = 255, Message = _l10n.Get("cmd.pit.self") });

        if (player.SessionId != Client?.SessionId)
            Reply(_l10n.Get("cmd.pit.broadcast", new { name = player.Name }));
    }

    [Command("settime")]
    public void SetTime(string time)
    {
        if (DateTime.TryParseExact(time, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            _weatherManager.SetTime((int)dateTime.TimeOfDay.TotalSeconds);
            Broadcast(_l10n.Get("cmd.settime.success"));
        }
        else
        {
            Reply(_l10n.Get("cmd.settime.invalid_format"));
        }
    }

    [Command("setweather")]
    public void SetWeather(int weatherId)
    {
        if (_weatherProvider.SetWeatherConfiguration(weatherId))
        {
            Reply(_l10n.Get("cmd.setweather.success"));
        }
        else
        {
            Reply(_l10n.Get("cmd.setweather.not_found"));
        }
    }

    [Command("cspweather")]
    public void CspWeather()
    {
        Reply(_l10n.Get("cmd.cspweather.list_header"));
        foreach (WeatherFxType weather in Enum.GetValues<WeatherFxType>())
        {
            Reply(_l10n.Get("cmd.cspweather.list_item", new { type = weather }));
        }
    }

    [Command("setcspweather")]
    public void SetCspWeather(string upcomingStr, int duration)
    {
        if (Enum.TryParse(upcomingStr, true, out WeatherFxType upcoming))
        {
            _weatherManager.SetCspWeather(upcoming, duration);
            Reply(_l10n.Get("cmd.setcspweather.success"));
        }
        else
        {
            Reply(_l10n.Get("cmd.setcspweather.not_found", new { name = upcomingStr }));
        }
    }

    [Command("setrain")]
    public void SetRain(float intensity, float wetness, float water)
    {
        _weatherManager.CurrentWeather.RainIntensity = intensity;
        _weatherManager.CurrentWeather.RainWetness = wetness;
        _weatherManager.CurrentWeather.RainWater = water;
        _weatherManager.SendWeather();
    }

    [Command("setgrip")]
    public void SetGrip(float grip)
    {
        _weatherManager.CurrentWeather.TrackGrip = grip;
        _weatherManager.SendWeather();
    }

    [Command("distance"), RequireConnectedPlayer]
    public void GetDistance([Remainder] ACTcpClient player)
    {
        var distance = Vector3.Distance(Client!.EntryCar.Status.Position, player.EntryCar.Status.Position).ToString(CultureInfo.InvariantCulture);
        Reply(_l10n.Get("cmd.distance.result", new { distance }));
    }

    [Command("forcelights")]
    public void ForceLights(string toggle, [Remainder] ACTcpClient player)
    {
        bool forceLights = toggle == "on";
        player.EntryCar.ForceLights = forceLights;

        Reply(_l10n.Get(forceLights ? "cmd.forcelights.enabled" : "cmd.forcelights.disabled", new { name = player.Name }));
    }

    [Command("whois")]
    public void WhoIs(ACTcpClient player)
    {
        var ip = (player.TcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address;
        Reply(_l10n.Get("cmd.whois.info", new { ip, guid = player.Guid, ping = player.EntryCar.Ping }));
        var velocity = (int)(player.EntryCar.Status.Velocity.Length() * 3.6);
        Reply(_l10n.Get("cmd.whois.position", new { position = player.EntryCar.Status.Position, velocity }));
        if (player.OwnerGuid.HasValue && player.Guid != player.OwnerGuid)
        {
            Reply(_l10n.Get("cmd.whois.family_sharing_owner", new { guid = player.OwnerGuid }));
        }
    }

    [Command("restrict")]
    public void Restrict(ACTcpClient player, float restrictor, float ballastKg)
    {
        player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = ballastKg, Restrictor = restrictor });
        Reply(_l10n.Get("cmd.restrict.success"));
    }

    // Do not change the reply, it is used by CSP admin detection
    [Command("ballast")]
    public void Ballast()
    {
        Reply(_l10n.Get("cmd.ballast.syntax_error"));
    }

    [Command("set")]
    public void Set(string key, [Remainder] string value)
    {
        try
        {
            Reply(_configuration.SetProperty(key, value)
                ? _l10n.Get("cmd.set.success", new { key, value })
                : _l10n.Get("cmd.set.failed", new { key }));
        }
        catch (Exception ex)
        {
            Reply(_l10n.Get("cmd.set.error", new { error = ex.Message }));
        }
    }

    [Command("whitelist")]
    public async Task Whitelist(ulong guid)
    {
        await _whitelist.AddAsync(guid);
        Reply(_l10n.Get("cmd.whitelist.added", new { guid }));
    }

    [Command("say")]
    public void Say([Remainder] string message)
    {
        Broadcast(_l10n.Get("cmd.say.broadcast", new { message }));
    }
}
