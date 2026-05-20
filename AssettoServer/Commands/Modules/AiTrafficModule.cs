using System.Linq;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AiTrafficModule : ACModuleBase
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly ILocalizationService _l10n;

    public AiTrafficModule(ACServerConfiguration configuration, EntryCarManager entryCarManager, ILocalizationService l10n)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _l10n = l10n;
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        if (!_configuration.Extra.EnableAi)
        {
            Reply(_l10n.Get("cmd.setaioverbooking.ai_disabled"));
            return;
        }

        foreach (var aiCar in _entryCarManager.EntryCars.Where(car => car.AiControlled && car.Client == null))
        {
            aiCar.SetAiOverbooking(count);
        }
        Reply(_l10n.Get("cmd.setaioverbooking.success", new { count }));
    }
}
