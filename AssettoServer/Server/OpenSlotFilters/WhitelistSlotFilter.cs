using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Whitelist;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;
using AssettoServer.Shared.Services;

namespace AssettoServer.Server.OpenSlotFilters;

public class WhitelistSlotFilter : OpenSlotFilterBase
{
    private readonly IWhitelistService _whitelist;
    private readonly ILocalizationService _l10n;

    public WhitelistSlotFilter(IWhitelistService whitelist, ILocalizationService l10n)
    {
        _whitelist = whitelist;
        _l10n = l10n;
    }

    public override async Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        if (!await _whitelist.IsWhitelistedAsync(request.Guid))
        {
            return new AuthFailedResponse(_l10n.Get("auth.not_whitelisted"));
        }

        return await base.ShouldAcceptConnectionAsync(client, request);
    }
}
