using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;

namespace Content.Server.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IResourceManager _resourceManager = default!; // DS14-announce
        private readonly ChatSystem _chatSystem;

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            _chatSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState();
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    // DS14-announce-start
                    Color color;
                    try
                    {
                        color = Color.FromHex(doAnnounce.ColorHex);
                    }
                    catch (FormatException)
                    {
                        color = Color.FromHex("#1d8bad");
                    }

                    SoundSpecifier? sound = null;
                    if (!string.IsNullOrWhiteSpace(doAnnounce.SoundPath))
                    {
                        var path = doAnnounce.SoundPath.Trim();
                        if (path.StartsWith("/Audio/", StringComparison.OrdinalIgnoreCase) &&
                            _resourceManager.TryContentFileRead(path, out _))
                        {
                            var audioParams = AudioParams.Default.WithVolume(doAnnounce.SoundVolume).AddVolume(-8);
                            sound = new SoundPathSpecifier(path)
                            {
                                Params = audioParams
                            };
                        }
                    }
                    // DS14-announce-end

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement, color); // DS14
                            break;

                        // TODO: Per-station announcement support
                        case AdminAnnounceType.Station:
                            // DS14-announce-start
                            var sender = string.IsNullOrEmpty(doAnnounce.Announcer)
                                ? Loc.GetString("chat-manager-sender-announcement")
                                : doAnnounce.Announcer;

                            var announcementWithSender = doAnnounce.Announcement;
                            if (!string.IsNullOrEmpty(doAnnounce.Sender))
                            {
                                announcementWithSender +=
                                    $"\n{Loc.GetString("comms-console-announcement-sent-by")} {doAnnounce.Sender}";
                            }

                            _chatSystem.DispatchGlobalAnnouncement(
                                message: announcementWithSender,
                                sender: sender,
                                playSound: true,
                                announcementSound: sound,
                                colorOverride: color,
                                originalMessage: doAnnounce.Announcement,
                                voice: doAnnounce.EnableTTS && doAnnounce.CustomTTS ? doAnnounce.Voice : null,
                                usePresetTTS: doAnnounce.EnableTTS && !doAnnounce.CustomTTS,
                                languageId: doAnnounce.LanguageId // DS14-Languages
                            // DS14-announce-end
                            );
                            break;
                    }

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
