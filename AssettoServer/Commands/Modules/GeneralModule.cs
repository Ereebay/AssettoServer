using AssettoServer.Server;
using Qmmands;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class GeneralModule : ACModuleBase
{
    private readonly WeatherManager _weatherManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;
    private readonly ILocalizationService _l10n;

    public GeneralModule(WeatherManager weatherManager,
        EntryCarManager entryCarManager,
        ACServerConfiguration configuration,
        ILocalizationService l10n)
    {
        _weatherManager = weatherManager;
        _entryCarManager = entryCarManager;
        _configuration = configuration;
        _l10n = l10n;
    }

    [Command("ping"), RequireConnectedPlayer]
    public void Ping()
        => Reply(_l10n.Get("cmd.ping.result", new { ping = Client!.EntryCar.Ping }));

    [Command("time")]
    public void Time()
        => Reply(_l10n.Get("cmd.time.result", new { time = _weatherManager.CurrentDateTime.ToString("H:mm", CultureInfo.InvariantCulture) }));

#if DEBUG
    [Command("test")]
    public ValueTask Test()
    {
        throw new Exception("Test exception");
    }
#endif

    // Do not change the reply, it is used by CSP admin detection
    [Command("admin"), RequireConnectedPlayer]
    public void AdminAsync(string password)
    {
        if (_configuration.Server.CheckAdminPassword(password))
        {
            Client!.LoginAsAdministrator();
            Reply(_l10n.Get("cmd.admin.success"));
        }
        else
            Reply(_l10n.Get("cmd.admin.refused"));
    }

    [Command("legal")]
    public async Task ShowLegalNotice()
    {
        using var sr = new StringReader(LegalNotice.LegalNoticeText);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            Reply(line);
        }
    }
    
    [Command("resetcar"), RequireConnectedPlayer]
    public void ResetCarAsync()
    {
        if (_configuration.Extra is { EnableClientMessages: true, EnableCarReset: true, MinimumCSPVersion: >= CSPVersion.V0_2_8, EnableAi: true })
        {
            Reply(Client!.EntryCar.TryResetPosition() 
                ? "Position successfully reset" 
                : "Couldn't reset position");
        }
        else
            Reply("Reset is not enabled on this server");
    }
}
