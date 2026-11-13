using System.IO;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.UI
{
    public sealed partial class AdminAnnounceEui : BaseEui
    {
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IResourceManager _resourceManager = default!;
        // DS14-announce-start
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        // DS14-announce-end

        private readonly ChatSystem _chatSystem;
        // DS14-announce-start
        private readonly StationSystem _stationSystem;
        private readonly SharedAudioSystem _audio;
        // DS14-announce-end

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            // DS14-announce-start
            var entitySystems = IoCManager.Resolve<IEntitySystemManager>();
            _chatSystem = entitySystems.GetEntitySystem<ChatSystem>();
            _stationSystem = entitySystems.GetEntitySystem<StationSystem>();
            _audio = entitySystems.GetEntitySystem<SharedAudioSystem>();
            // DS14-announce-end
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState(GetAnnouncementTargets()); // DS14
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
                    var (hex, color) = GetAnnouncementColor(doAnnounce.ColorHex);
                    var sound = GetAnnouncementSound(doAnnounce.SoundPath, doAnnounce.SoundVolume);
                    var sender = string.IsNullOrWhiteSpace(doAnnounce.Announcer)
                        ? Loc.GetString("chat-manager-sender-announcement")
                        : doAnnounce.Announcer.Trim();
                    var announcementWithSignature = GetAnnouncementWithSignature(doAnnounce.Announcement, doAnnounce.Sender);
                    var targetLog = doAnnounce.AnnounceType.ToString();

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement($"{sender}: {announcementWithSignature}", color);
                            if (sound != null)
                                _audio.PlayGlobal(sound, Filter.Broadcast(), true);
                            break;

                        case AdminAnnounceType.All:
                        {
                            DispatchGlobalAnnouncement(doAnnounce, announcementWithSignature, sender, color, sound);
                            break;
                        }

                        case AdminAnnounceType.Map:
                        {
                            if (!TryGetMapTarget(doAnnounce.TargetGrid, out var filter, out targetLog))
                                break;

                            DispatchFilteredAnnouncement(doAnnounce, filter, announcementWithSignature, sender, color, sound);
                            break;
                        }
                    }

                    _adminLogger.Add(
                        LogType.Chat,
                        LogImpact.Low,
                        $"{Player.Name} has sent admin announcement " +
                        $"[type={doAnnounce.AnnounceType}] " +
                        $"[target={targetLog}] " +
                        $"[color={hex}] " +
                        $"[sound={(sound != null ? doAnnounce.SoundPath : "none")}] " +
                        $"[volume={doAnnounce.SoundVolume}] " +
                        $"[announcer=\"{doAnnounce.Announcer}\"] " +
                        $"[sender=\"{doAnnounce.Sender}\"] " +
                        $": {doAnnounce.Announcement}"
                    );
                    // DS14-announce-end

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }

        // DS14-announce-start
        private List<AdminAnnounceTargetEntry> GetAnnouncementTargets()
        {
            var targets = new List<AdminAnnounceTargetEntry>();
            var addedGrids = new HashSet<EntityUid>();

            foreach (var station in _stationSystem.GetStations())
            {
                if (_stationSystem.GetLargestGrid(station) is not { } grid)
                    continue;

                TryAddAnnouncementTarget(targets, addedGrids, grid);
            }

            // CentComm is loaded by the emergency shuttle system, not StationSystem.
            var centcommQuery = _entityManager.EntityQueryEnumerator<StationCentcommComponent>();
            while (centcommQuery.MoveNext(out _, out var centcomm))
            {
                if (centcomm.Entity is not { } grid)
                    continue;

                TryAddAnnouncementTarget(targets, addedGrids, grid);
            }

            targets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return targets;
        }

        private bool TryAddAnnouncementTarget(
            List<AdminAnnounceTargetEntry> targets,
            HashSet<EntityUid> addedGrids,
            EntityUid grid)
        {
            if (addedGrids.Contains(grid) ||
                !_entityManager.TryGetComponent<TransformComponent>(grid, out var xform) ||
                xform.MapID == MapId.Nullspace ||
                !_entityManager.TryGetComponent<MetaDataComponent>(grid, out var metadata) ||
                string.IsNullOrWhiteSpace(metadata.EntityName))
            {
                return false;
            }

            addedGrids.Add(grid);
            targets.Add(new AdminAnnounceTargetEntry(metadata.EntityName, _entityManager.GetNetEntity(grid)));
            return true;
        }

        private bool TryGetMapTarget(NetEntity? netGrid, out Filter filter, out string targetLog)
        {
            filter = Filter.Empty();
            targetLog = "invalid";

            if (netGrid == null ||
                !_entityManager.TryGetEntity(netGrid.Value, out var grid) ||
                grid == null ||
                !_entityManager.TryGetComponent<TransformComponent>(grid.Value, out var xform) ||
                xform.MapID == MapId.Nullspace)
            {
                return false;
            }

            filter = Filter.Empty().AddInMap(xform.MapID, _entityManager);
            targetLog = _entityManager.GetComponent<MetaDataComponent>(grid.Value).EntityName;
            return true;
        }

        private void DispatchGlobalAnnouncement(
            AdminAnnounceEuiMsg.DoAnnounce doAnnounce,
            string announcement,
            string sender,
            Color color,
            SoundSpecifier? sound)
        {
            _chatSystem.DispatchGlobalAnnouncement(
                message: announcement,
                sender: sender,
                colorOverride: color,
                playSound: true,
                announcementSound: sound,
                originalMessage: doAnnounce.Announcement,
                voice: doAnnounce.EnableTTS && doAnnounce.CustomTTS ? doAnnounce.Voice : null,
                usePresetTTS: doAnnounce.EnableTTS && !doAnnounce.CustomTTS,
                languageId: doAnnounce.LanguageId // DS14-Languages
            );
        }

        private void DispatchFilteredAnnouncement(
            AdminAnnounceEuiMsg.DoAnnounce doAnnounce,
            Filter filter,
            string announcement,
            string sender,
            Color color,
            SoundSpecifier? sound)
        {
            _chatSystem.DispatchAdminFilteredAnnouncement(
                filter: filter,
                message: announcement,
                sender: sender,
                colorOverride: color,
                playSound: true,
                announcementSound: sound,
                originalMessage: doAnnounce.Announcement,
                voice: doAnnounce.EnableTTS && doAnnounce.CustomTTS ? doAnnounce.Voice : null,
                usePresetTTS: doAnnounce.EnableTTS && !doAnnounce.CustomTTS,
                languageId: doAnnounce.LanguageId // DS14-Languages
            );
        }

        private (string Hex, Color Color) GetAnnouncementColor(string? colorHex)
        {
            var hex = colorHex?.Trim();

            if (string.IsNullOrWhiteSpace(hex))
                hex = "1d8bad";

            if (!hex.StartsWith('#'))
                hex = "#" + hex;

            try
            {
                return (hex, Color.FromHex(hex));
            }
            catch (FormatException)
            {
                return ("#1d8bad", Color.FromHex("#1d8bad"));
            }
        }

        private SoundSpecifier? GetAnnouncementSound(string? soundPath, float soundVolume)
        {
            if (string.IsNullOrWhiteSpace(soundPath))
                return null;

            var path = soundPath.Trim();
            if (!path.StartsWith("/Audio/", StringComparison.OrdinalIgnoreCase) ||
                !HasAnnouncementAudio(path))
            {
                return null;
            }

            var audioParams = AudioParams.Default.WithVolume(soundVolume).AddVolume(-8);
            return new SoundPathSpecifier(path)
            {
                Params = audioParams
            };
        }

        private bool HasAnnouncementAudio(string path)
        {
            if (_prototypeManager.HasIndex<AudioMetadataPrototype>(path))
                return true;

            try
            {
                if (!_resourceManager.TryContentFileRead(path, out var stream))
                    return false;

                stream.Dispose();
                return true;
            }
            catch (Exception e) when (e is ArgumentException or FileNotFoundException)
            {
                return false;
            }
        }

        private static string GetAnnouncementWithSignature(string announcement, string? signature)
        {
            if (string.IsNullOrWhiteSpace(signature))
                return announcement;

            return $"{announcement}\n{Loc.GetString("comms-console-announcement-sent-by")} {signature.Trim()}";
        }
        // DS14-announce-end
    }
}
