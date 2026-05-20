using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;

namespace RaceChallengePlugin;

public class RaceChallengePlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarRace> _entryCarRaceFactory;
    private readonly ILocalizationService _l10n;
    private readonly Dictionary<int, EntryCarRace> _instances = new();

    public RaceChallengePlugin(EntryCarManager entryCarManager, Func<EntryCar, EntryCarRace> entryCarRaceFactory, IHostApplicationLifetime applicationLifetime, ILocalizationService l10n) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _entryCarRaceFactory = entryCarRaceFactory;
        _l10n = l10n;

        var pluginDir = Path.GetDirectoryName(typeof(RaceChallengePlugin).Assembly.Location)!;
        _l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "race");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(entryCar.SessionId, _entryCarRaceFactory(entryCar));
        }

        return Task.CompletedTask;
    }

    internal EntryCarRace GetRace(EntryCar entryCar) => _instances[entryCar.SessionId];
}
