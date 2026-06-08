using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Shared.Services;
using Qmmands;

namespace ReportPlugin;

public class ReportCommandModule : ACModuleBase
{
    private readonly ReportPlugin _plugin;
    private readonly ILocalizationService _l10n;

    public ReportCommandModule(ReportPlugin plugin, ILocalizationService l10n)
    {
        _plugin = plugin;
        _l10n = l10n;
    }

    [Command("report"), RequireConnectedPlayer]
    public async Task Report([Remainder] string reason)
    {
        var report = _plugin.GetLastReplay(Client!);

        if (report == null)
        {
            Reply(_l10n.Get("plugin.report.cmd.no_replay"));
        }
        else if (report.Submitted)
        {
            Reply(_l10n.Get("plugin.report.cmd.already_submitted"));
        }
        else
        {
            Client!.Logger.Information("Report received from {ClientName} ({SessionId}), ID: {Id}, Reason: {Reason}",
                Client.Name, Client.SessionId, report.Guid, reason);
            await _plugin.SubmitReport(Client, report, reason);
            Reply(_l10n.Get("plugin.report.cmd.submitted"));
            report.Submitted = true;
        }
    }
}
