using Content.Server.Administration.Logs;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
// DS14-start
using Content.Shared.DeadSpace.Administration;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
// DS14-end
using Content.Shared.NukeOps;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.NukeOps;

/// <summary>
/// This handles nukeops special war mode declaration device and directly using nukeops game rule.
/// </summary>
public sealed class WarDeclaratorSystem : EntitySystem
{
    // DS14-start
    private const int MaxSecondaryTitleLength = 120;
    private const int MaxSecondaryTextLength = 2000;
    private const int SecondaryTitleWords = 6;
    // DS14-end

    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!; // DS14
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    // DS14-start
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    // DS14-end

    public override void Initialize()
    {
        SubscribeLocalEvent<WarDeclaratorComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<WarDeclaratorComponent, ActivatableUIOpenAttemptEvent>(OnAttemptOpenUI);
        SubscribeLocalEvent<WarDeclaratorComponent, WarDeclaratorActivateMessage>(OnActivated);
        // DS14-start
        SubscribeLocalEvent<WarDeclaratorComponent, WarDeclaratorSecondaryAnnouncementMessage>(OnSecondaryAnnouncement);
        // DS14-end
    }

    private void OnMapInit(Entity<WarDeclaratorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Message = Loc.GetString(ent.Comp.Message); // DS14
        ent.Comp.DisableAt = _gameTiming.CurTime + TimeSpan.FromMinutes(ent.Comp.WarDeclarationDelay);
    }

    private void OnAttemptOpenUI(Entity<WarDeclaratorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // DS14-start
        // Admin ghosts are allowed to use the declaration UI without an ID/access check.
        if (!IsAdminGhost(args.User) && !_accessReaderSystem.IsAllowed(args.User, ent))
        // DS14-end
        {
            if (!args.Silent)
            {
                var msg = Loc.GetString("war-declarator-not-working");
                _popupSystem.PopupEntity(msg, ent);
            }

            args.Cancel();
            return;
        }

        UpdateUI(ent, ent.Comp.CurrentStatus);
    }

    private void OnActivated(Entity<WarDeclaratorComponent> ent, ref WarDeclaratorActivateMessage args)
    {
        var ev = new WarDeclaredEvent(ent.Comp.CurrentStatus, ent);
        RaiseLocalEvent(ref ev);

        ent.Comp.CurrentStatus = ev.Status;

        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var message = SharedChatSystem.SanitizeAnnouncement(args.Message, maxLength);
        if (ent.Comp.AllowEditingMessage && message != string.Empty)
            ent.Comp.Message = message;

        if (ev.Status == WarConditionStatus.WarReady)
        {
            var title = Loc.GetString(ent.Comp.SenderTitle);

            // DS14-start
            _chat.DispatchGlobalAnnouncement(ent.Comp.Message, title, false, colorOverride: ent.Comp.Color);
            _sound.PlayAlertLevelGlobal(Filter.Broadcast(), ent.Comp.Sound, ent.Comp.Sound.Params);
            // DS14-end

            _adminLogger.Add(
                LogType.Chat,
                LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} has declared war with this text: {ent.Comp.Message}");
        }

        UpdateUI(ent, ev.Status);
    }

    // DS14-start
    private void OnSecondaryAnnouncement(
        Entity<WarDeclaratorComponent> ent,
        ref WarDeclaratorSecondaryAnnouncementMessage args)
    {
        if (!ent.Comp.SecondaryAnnouncementEnabled)
            return;

        // Admin ghosts do not have MobStateComponent and must bypass the living-mob check.
        if (!IsAdminGhost(args.Actor) &&
            (!TryComp<MobStateComponent>(args.Actor, out var actorMobState) ||
             actorMobState.CurrentState != MobState.Alive))
        {
            return;
        }

        var title = NormalizeAnnouncementPart(args.Title, MaxSecondaryTitleLength);
        var text = NormalizeAnnouncementPart(args.Text, MaxSecondaryTextLength);

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(text))
            return;

        if (string.IsNullOrEmpty(title))
            SplitAnnouncementText(ref title, ref text);

        title = title.ToUpperInvariant();
        SendSecondaryAnnouncement(ent.Comp, title, text);

        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} sent a targeted nukeops secondary announcement: {title} / {text}");
    }

    private void SendSecondaryAnnouncement(WarDeclaratorComponent comp, string title, string message)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            // Admin ghosts always receive targeted announcements for moderation.
            if (!IsAdminGhost(playerEntity))
            {
                if (!TryComp<MobStateComponent>(playerEntity, out var mobState) ||
                    mobState.CurrentState != MobState.Alive)
                {
                    continue;
                }

                if (!WearsSecondaryAnnouncementEquipment(playerEntity, comp))
                    continue;
            }

            RaiseNetworkEvent(new GhostRoleIntroductionEvent(
                title,
                message,
                comp.SecondaryAnnouncementTextColor,
                comp.SecondaryAnnouncementFont,
                comp.SecondaryAnnouncementFontSize,
                comp.SecondaryAnnouncementTitleFontSize,
                comp.SecondaryAnnouncementDuration,
                comp.SecondaryAnnouncementFadeFromBlackDuration,
                comp.SecondaryAnnouncementFadeOutDuration,
                comp.SecondaryAnnouncementTextDelay,
                comp.SecondaryAnnouncementCharactersPerSecond,
                comp.SecondaryAnnouncementShowBlackBackground,
                comp.SecondaryAnnouncementTypeTitle,
                targetedAnnouncement: true,
                announcementSound: comp.SecondaryAnnouncementSound,
                interferenceSound: comp.SecondaryAnnouncementInterferenceSound,
                interferenceDuration: comp.SecondaryAnnouncementInterferenceDuration),
                session);
        }
    }

    private bool WearsSecondaryAnnouncementEquipment(EntityUid wearer, WarDeclaratorComponent comp)
    {
        if (comp.SecondaryAnnouncementRequiredEquipment.Count == 0 ||
            !TryComp<InventoryComponent>(wearer, out var inventory))
        {
            return false;
        }

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item))
        {
            var prototypeId = MetaData(item).EntityPrototype?.ID;
            if (prototypeId != null && comp.SecondaryAnnouncementRequiredEquipment.Contains(prototypeId))
                return true;
        }

        return false;
    }

    private bool IsAdminGhost(EntityUid entity)
    {
        if (HasComp<AdminGhostVisibilityComponent>(entity))
            return true;

        // Fallback for older or custom admin-observer prototypes.
        return TryComp<GhostComponent>(entity, out var ghost) && ghost.CanGhostInteract;
    }

    private static string NormalizeAnnouncementPart(string value, int maxLength)
    {
        var normalized = string.Join(' ', value.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length > maxLength
            ? normalized[..maxLength].TrimEnd()
            : normalized;
    }

    private static void SplitAnnouncementText(ref string title, ref string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleWordCount = Math.Min(SecondaryTitleWords, words.Length);

        title = string.Join(" ", words, 0, titleWordCount);
        text = titleWordCount < words.Length
            ? string.Join(" ", words, titleWordCount, words.Length - titleWordCount)
            : string.Empty;
    }
    // DS14-end

    private void UpdateUI(Entity<WarDeclaratorComponent> ent, WarConditionStatus? status = null)
    {
        _userInterfaceSystem.SetUiState(
            ent.Owner,
            WarDeclaratorUiKey.Key,
            // DS14-start
            new WarDeclaratorBoundUserInterfaceState(
                status,
                ent.Comp.DisableAt,
                ent.Comp.ShuttleDisabledTime,
                ent.Comp.SecondaryAnnouncementEnabled));
            // DS14-end
    }
}
