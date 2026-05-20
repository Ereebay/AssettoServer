using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;
using AssettoServer.Shared.Services;

namespace AssettoServer.Server.OpenSlotFilters;

public class SteamSlotFilter : OpenSlotFilterBase
{
    private readonly Steam _steam;
    private readonly ILocalizationService _l10n;

    public SteamSlotFilter(Steam steam, ILocalizationService l10n)
    {
        _steam = steam;
        _l10n = l10n;
    }

    public override async Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        if (!await _steam.ValidateSessionTicketAsync(request.SessionTicket, request.Guid, client))
        {
            return new AuthFailedResponse(_l10n.Get("auth.steam_failed"));
        }

        return await base.ShouldAcceptConnectionAsync(client, request);
    }
}
