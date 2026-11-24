// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Content.Server.DeadSpace.RoundEnd;
using Content.Server.Discord;
using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private DiscordWebhook _discord = default!;

    private const int DiscordAttachmentsPerMessage = 10;
    private static readonly TimeZoneInfo? MoscowTimeZone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Moscow", out var zone) ? zone : null;

    private Task _roundDiscordQueue = Task.CompletedTask;

    private string DiscordServerName => string.IsNullOrWhiteSpace(ServerName) || ServerName == "unknown"
        ? _baseServer.ServerName
        : ServerName;

    private static string FormatDiscordTime()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var local = MoscowTimeZone == null
            ? utcNow.ToOffset(TimeSpan.FromHours(3))
            : TimeZoneInfo.ConvertTime(utcNow, MoscowTimeZone);

        return local.ToString("HH:mm | dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private void SendRoundEndDiscordMessage(RoundEndMessageEvent snapshot, IEnumerable<(string Text, bool IsOutcome)> report)
    {
        if (_roundWebhook is not { } webhook)
            return;

        var id = snapshot.RoundId.ToString(CultureInfo.InvariantCulture);
        var server = DiscordServerName;
        var duration = snapshot.RoundDuration;
        var gamemode = string.IsNullOrEmpty(snapshot.GamemodeTitle)
            ? Loc.GetString("discord-round-notifications-unknown-mode")
            : snapshot.GamemodeTitle;

        var content = Loc.GetString("discord-round-notifications-end",
            ("timestamp", FormatDiscordTime()),
            ("server", server),
            ("id", id),
            ("hours", (long) duration.TotalHours),
            ("minutes", duration.Minutes),
            ("seconds", duration.Seconds),
            ("gamemode", gamemode));

        var ping = DiscordRoundEndRole == null
            ? null
            : Loc.GetString("discord-round-notifications-end-ping", ("roleId", DiscordRoundEndRole));

        RoundEndDiscordManifest? manifest = null;
        try
        {
            manifest = RoundEndDiscordManifest.Capture(snapshot, report);
        }
        catch (Exception e)
        {
            Log.Error($"Error while preparing round {id} Discord manifest: {e}");
        }

        var continuation = Loc.GetString("discord-round-notifications-manifest-continuation",
            ("id", id), ("server", server));

        _roundDiscordQueue = SendRoundDiscordMessageAsync(_roundDiscordQueue, webhook, content, ping, manifest, continuation);
    }

    private void SendRoundStartingDiscordMessage()
    {
        if (_roundWebhook is not { } webhook)
            return;

        var content = Loc.GetString("discord-round-notifications-new",
            ("timestamp", FormatDiscordTime()), ("server", DiscordServerName));
        _roundDiscordQueue = SendRoundDiscordMessageAsync(_roundDiscordQueue, webhook, content);
    }

    private void SendRoundStartedDiscordMessage()
    {
        if (_roundWebhook is not { } webhook)
            return;

        var map = _gameMapManager.GetSelectedMap()?.MapName ?? Loc.GetString("discord-round-notifications-unknown-map");
        var gamemode = (Decoy ?? CurrentPreset) is { } preset
            ? Loc.GetString(preset.ModeTitle)
            : Loc.GetString("discord-round-notifications-unknown-mode");

        var content = Loc.GetString("discord-round-notifications-started",
            ("timestamp", FormatDiscordTime()),
            ("server", DiscordServerName),
            ("id", RoundId.ToString(CultureInfo.InvariantCulture)),
            ("map", map),
            ("gamemode", gamemode));

        _roundDiscordQueue = SendRoundDiscordMessageAsync(_roundDiscordQueue, webhook, content);
    }

    private async Task SendRoundDiscordMessageAsync(
        Task previous,
        Task<WebhookData?> webhook,
        string content,
        string? ping = null,
        RoundEndDiscordManifest? manifest = null,
        string? continuation = null)
    {
        try
        {
            // Keep status posts in event order even if the next round starts during an upload.
            await previous.ConfigureAwait(false);
            if (await webhook.ConfigureAwait(false) is not { } data)
                return;

            WebhookAttachment[][] pages = [];
            if (manifest != null)
            {
                try
                {
                    pages = await Task.Run(() => manifest.Render().Chunk(DiscordAttachmentsPerMessage).ToArray())
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.Error($"Error while rendering round {manifest.RoundId} Discord manifest: {e}");
                }
            }

            var identifier = data.ToIdentifier();
            var payload = new WebhookPayload { Content = content };
            var posted = await PostRoundDiscordMessageAsync(identifier, payload, pages.FirstOrDefault()).ConfigureAwait(false);
            if (!posted && pages.Length > 0)
            {
                posted = await PostRoundDiscordMessageAsync(identifier, payload).ConfigureAwait(false);
            }

            if (ping != null)
            {
                var pingPayload = new WebhookPayload { Content = ping };
                pingPayload.AllowedMentions.AllowRoleMentions();
                await PostRoundDiscordMessageAsync(identifier, pingPayload).ConfigureAwait(false);
            }

            if (!posted)
                return;

            for (var i = 1; i < pages.Length; i++)
            {
                await PostRoundDiscordMessageAsync(identifier, new WebhookPayload { Content = continuation }, pages[i])
                    .ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending Discord round status: {e}");
        }
    }

    private async Task<bool> PostRoundDiscordMessageAsync(
        WebhookIdentifier identifier,
        WebhookPayload payload,
        WebhookAttachment[]? attachments = null)
    {
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var response = attachments == null
                    ? await _discord.CreateMessage(identifier, payload).ConfigureAwait(false)
                    : await _discord.CreateMessage(identifier, payload, attachments).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == 1 ||
                    response.Headers.RetryAfter?.Delta is not { } retryAfter)
                {
                    return response.IsSuccessStatusCode;
                }

                await Task.Delay(retryAfter).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            Log.Error($"Error while posting Discord round status: {e}");
            return false;
        }
    }
}
