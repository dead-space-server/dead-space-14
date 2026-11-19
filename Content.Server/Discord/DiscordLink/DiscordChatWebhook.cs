using System.Threading.Tasks;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Configuration;

namespace Content.Server.Discord.DiscordLink;

/// <summary>
///     Relays in-game chat to Discord through regular webhooks, without using the Discord bot gateway.
/// </summary>
public sealed partial class DiscordChatWebhook : IPostInjectInit
{
    private const int DiscordMessageMaxLength = 2000;
    private const string TruncatedSuffix = "...";

    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private DiscordWebhook _discordWebhook = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private string _adminChatWebhookUrl = string.Empty;
    private WebhookIdentifier? _adminChatWebhookIdentifier;

    public void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.DiscordAdminChatWebhook, OnAdminChatWebhookChanged, true);
    }

    public void Shutdown()
    {
        _configurationManager.UnsubValueChanged(CCVars.DiscordAdminChatWebhook, OnAdminChatWebhookChanged);
    }

    public void SendAdminChatMessage(string message, string author)
    {
        if (_adminChatWebhookIdentifier == null)
            return;

        _ = SendAdminChatMessageAsync(_adminChatWebhookIdentifier.Value, message, author);
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord.chat.webhook");
    }

    private async void OnAdminChatWebhookChanged(string webhookUrl)
    {
        _adminChatWebhookUrl = webhookUrl;
        _adminChatWebhookIdentifier = null;

        if (string.IsNullOrWhiteSpace(webhookUrl))
            return;

        var webhookData = await _discordWebhook.GetWebhook(webhookUrl);
        if (_adminChatWebhookUrl != webhookUrl)
            return;

        if (webhookData == null)
        {
            _sawmill.Error("Admin chat Discord webhook URL does not appear to be valid.");
            return;
        }

        _adminChatWebhookIdentifier = webhookData.Value.ToIdentifier();
    }

    private async Task SendAdminChatMessageAsync(WebhookIdentifier webhookIdentifier, string message, string author)
    {
        var content = $"**{ChatChannel.AdminChat.GetString()}**: `{SanitizeDiscordText(author)}`: {SanitizeDiscordText(message)}";
        content = TruncateDiscordContent(content);

        try
        {
            var payload = new WebhookPayload { Content = content };
            await _discordWebhook.CreateMessage(webhookIdentifier, payload);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending admin chat Discord webhook message:\n{e}");
        }
    }

    private static string SanitizeDiscordText(string text)
    {
        return text
            .Replace("@", "\\@")
            .Replace("<", "\\<")
            .Replace("/", "\\/")
            .Replace("`", "\\`");
    }

    private static string TruncateDiscordContent(string content)
    {
        if (content.Length <= DiscordMessageMaxLength)
            return content;

        return content[..(DiscordMessageMaxLength - TruncatedSuffix.Length)] + TruncatedSuffix;
    }
}
