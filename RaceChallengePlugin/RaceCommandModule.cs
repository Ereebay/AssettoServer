using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Services;
using Qmmands;

namespace RaceChallengePlugin;

[RequireConnectedPlayer]
public class RaceCommandModule : ACModuleBase
{
    private readonly RaceChallengePlugin _plugin;
    private readonly ILocalizationService _l10n;

    public RaceCommandModule(RaceChallengePlugin plugin, ILocalizationService l10n)
    {
        _plugin = plugin;
        _l10n = l10n;
    }

    [Command("race"), RequireConnectedPlayer]
    public void Race(ACTcpClient player)
        => _plugin.GetRace(Client!.EntryCar).ChallengeCar(player.EntryCar);

    [Command("accept"), RequireConnectedPlayer]
    public async ValueTask AcceptRaceAsync()
    {
        var currentRace = _plugin.GetRace(Client!.EntryCar).CurrentRace;
        if (currentRace == null)
            Reply(_l10n.Get("plugin.race.cmd.accept.no_request"));
        else if (currentRace.HasStarted)
            Reply(_l10n.Get("plugin.race.cmd.accept.already_started"));
        else if (currentRace.Challenger == Client!.EntryCar)
            Reply(_l10n.Get("plugin.race.cmd.accept.self_initiated"));
        else
            await currentRace.StartAsync();
    }
}
