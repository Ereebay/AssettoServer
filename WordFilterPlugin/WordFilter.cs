using System.Text.RegularExpressions;
using AssettoServer.Commands;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;
using AssettoServer.Shared.Services;

namespace WordFilterPlugin;

public class WordFilter : OpenSlotFilterBase
{
    private readonly EntryCarManager _entryCarManager;
    private readonly WordFilterConfiguration _configuration;
    private readonly ILocalizationService _l10n;

    public WordFilter(WordFilterConfiguration configuration, EntryCarManager entryCarManager, ChatService chatService, ILocalizationService l10n)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _l10n = l10n;

        var pluginDir = Path.GetDirectoryName(typeof(WordFilter).Assembly.Location)!;
        _l10n.RegisterSource(Path.Combine(pluginDir, "lang"), "wordfilter");

        chatService.MessageReceived += OnChatMessageReceived;
    }

    private void OnChatMessageReceived(ACTcpClient sender, ChatEventArgs args)
    {
        if (_configuration.BannableChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered and banned: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
            Task.Run(() => _entryCarManager.BanAsync(sender, _l10n.Get("plugin.wordfilter.ban_reason")));
        }
        else if (_configuration.ProhibitedChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
        }
    }
    
    public override Task<AuthFailedResponse?> ShouldAcceptConnectionAsync(ACTcpClient client, HandshakeRequest request)
    {
        if (_configuration.ProhibitedUsernamePatterns.Any(regex => Regex.Match(request.Name, regex, RegexOptions.IgnoreCase).Success))
        {
            return Task.FromResult<AuthFailedResponse?>(new AuthFailedResponse(_l10n.Get("plugin.wordfilter.prohibited_username")));
        }
        
        return base.ShouldAcceptConnectionAsync(client, request);
    }
}
