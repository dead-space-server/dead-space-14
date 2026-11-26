using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [ViewVariables]
        public bool LobbyEnabled { get; private set; }

        [ViewVariables]
        public bool DummyTicker { get; private set; } = false;

        [ViewVariables]
        public TimeSpan LobbyDuration { get; private set; } = TimeSpan.Zero;

        [ViewVariables]
        public bool DisallowLateJoin { get; private set; } = false;

        [ViewVariables]
        public string? ServerName { get; private set; }

        [ViewVariables]
        private string? DiscordRoundEndRole { get; set; }

        private Task<WebhookData?>? _roundWebhook; // DS14

        [ViewVariables]
        private string? RoundEndSoundCollection { get; set; }

#if EXCEPTION_TOLERANCE
        [ViewVariables]
        public int RoundStartFailShutdownCount { get; private set; } = 0;
#endif

        private void InitializeCVars()
        {
            Subs.CVar(_cfg, CCVars.GameLobbyEnabled, value =>
            {
                LobbyEnabled = value;
                foreach (var (userId, status) in _playerGameStatuses)
                {
                    if (status == PlayerGameStatus.JoinedGame)
                        continue;
                    _playerGameStatuses[userId] =
                        LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;
                }
            }, true);
            Subs.CVar(_cfg, CCVars.GameDummyTicker, value => DummyTicker = value, true);
            Subs.CVar(_cfg, CCVars.GameLobbyDuration, value => LobbyDuration = TimeSpan.FromSeconds(value), true);
            Subs.CVar(_cfg, CCVars.GameDisallowLateJoins,
                value => { DisallowLateJoin = value; UpdateLateJoinStatus(); }, true);
            Subs.CVar(_cfg, CCVars.AdminLogsServerName, value =>
            {
                // TODO why tf is the server name on admin logs
                ServerName = value;
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundUpdateWebhook, value =>
            {
                _roundWebhook = string.IsNullOrWhiteSpace(value) ? null : _discord.GetWebhook(value); // DS14
            }, true);
            Subs.CVar(_cfg, CCVars.DiscordRoundEndRoleWebhook, value =>
            {
                DiscordRoundEndRole = value;

                if (value == string.Empty)
                {
                    DiscordRoundEndRole = null;
                }
            }, true);
            Subs.CVar(_cfg, CCVars.RoundEndSoundCollection, value => RoundEndSoundCollection = value, true);
#if EXCEPTION_TOLERANCE
            Subs.CVar(_cfg, CCVars.RoundStartFailShutdownCount, value => RoundStartFailShutdownCount = value, true);
#endif
        }
    }
}
