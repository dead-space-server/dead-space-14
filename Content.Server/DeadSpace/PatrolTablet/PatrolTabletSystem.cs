using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Content.Shared.DeadSpace.PatrolTablet;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.PatrolTablet;

public sealed class PatrolTabletSystem : EntitySystem
{
    private const int MaxSquads = 16;
    private const int MaxSquadNameLength = 30;
    private const int MaxAnnouncementTitleLength = 120;
    private const int MaxAnnouncementTextLength = 2000;
    private const int AnnouncementTitleWords = 6;
    private const string SquadIconPrototypePrefix = "DeadSpaceSquadIcon";
    private const string HeadOfSecurityAccess = "HeadOfSecurity";
    private const string CaptainAccess = "Captain";

    /// <summary>
    /// Active announcement lock per recipient equipment prototype.
    /// Tablets with completely disjoint AnnouncementRequiredEquipment lists do not block each other.
    /// </summary>
    private readonly Dictionary<string, TimeSpan> _announcementBusyUntilByEquipment = new();

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PatrolTabletComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
        SubscribeLocalEvent<PatrolTabletComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletRenameSquadMessage>(OnRenameSquad);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletBulkAssignSquadMessage>(OnBulkAssignSquad);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletClearAllMessage>(OnClearAll);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletClearSquadMessage>(OnClearSquad);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletCreateSquadMessage>(OnCreateSquad);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletDeleteSquadMessage>(OnDeleteSquad);
        SubscribeLocalEvent<PatrolTabletComponent, PatrolTabletSendAnnouncementMessage>(OnSendAnnouncement);
    }

    private bool AddTrackedPersonnel(EntityUid uid, PatrolTabletComponent comp, EntityUid target, EntityUid user)
    {
        if (!comp.SquadManagementEnabled)
            return false;

        if (!_idCard.TryFindIdCard(target, out _))
        {
            _popup.PopupEntity(Loc.GetString("patrol-tablet-no-id-card"), uid, user);
            return false;
        }

        var netTarget = GetNetEntity(target);

        if (comp.TrackedPersonnel.Contains(netTarget))
            return false;

        if (!HasComp<PatrolMemberComponent>(target))
            AddComp<PatrolMemberComponent>(target);

        comp.TrackedPersonnel.Add(netTarget);
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("patrol-tablet-added-personnel", ("name", MetaData(target).EntityName)), uid, user);
        return true;
    }

    private void OnAfterInteract(EntityUid uid, PatrolTabletComponent comp, AfterInteractEvent args)
    {
        if (!comp.SquadManagementEnabled)
            return;

        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!HasComp<MobStateComponent>(args.Target.Value))
            return;

        if (comp.TrackedPersonnel.Contains(GetNetEntity(args.Target.Value)))
        {
            _popup.PopupEntity(Loc.GetString("patrol-tablet-already-tracked"), uid, args.User);
            return;
        }

        if (AddTrackedPersonnel(uid, comp, args.Target.Value, args.User))
            UpdateUiState(uid, comp);
        args.Handled = true;
    }

    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PatrolTabletComponent>(args.Used, out var comp))
            return;

        if (!comp.SquadManagementEnabled)
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (comp.TrackedPersonnel.Contains(GetNetEntity(args.Target)))
        {
            _popup.PopupEntity(Loc.GetString("patrol-tablet-already-tracked"), args.Used, args.User);
            args.Handled = true;
            return;
        }

        if (AddTrackedPersonnel(args.Used, comp, args.Target, args.User))
            UpdateUiState(args.Used, comp);
        args.Handled = true;
    }

    private void OnUiOpen(EntityUid uid, PatrolTabletComponent comp, AfterActivatableUIOpenEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private static string? SanitizeSquadName(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return name.Length > MaxSquadNameLength
            ? name[..MaxSquadNameLength]
            : name;
    }

    private bool IsValidSquadIcon(string iconId)
    {
        return iconId.StartsWith(SquadIconPrototypePrefix, StringComparison.Ordinal)
               && _prototype.TryIndex<SecurityIconPrototype>(iconId, out var icon)
               && !icon.Abstract;
    }

    private void OnRenameSquad(EntityUid uid, PatrolTabletComponent comp, PatrolTabletRenameSquadMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        var name = SanitizeSquadName(msg.NewName);
        if (name == null)
            return;

        var squad = comp.Squads.Find(s => s.Id == msg.SquadId);
        if (squad == null)
            return;

        squad.Name = name;
        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void OnBulkAssignSquad(EntityUid uid, PatrolTabletComponent comp, PatrolTabletBulkAssignSquadMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        var squad = comp.Squads.Find(s => s.Id == msg.SquadId);
        if (squad == null)
            return;

        foreach (var netEntity in comp.TrackedPersonnel)
        {
            var target = GetEntity(netEntity);
            if (!Exists(target))
                continue;

            SetSquadOnIdCard(target, squad.Id, squad.IconId);
        }

        comp.TrackedPersonnel.Clear();
        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void OnClearAll(EntityUid uid, PatrolTabletComponent comp, PatrolTabletClearAllMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        comp.TrackedPersonnel.Clear();
        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void OnClearSquad(EntityUid uid, PatrolTabletComponent comp, PatrolTabletClearSquadMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        var query = EntityQueryEnumerator<PatrolSquadCardComponent>();
        while (query.MoveNext(out var cardUid, out var squadCard))
        {
            if (squadCard.SquadId != msg.SquadId)
                continue;

            squadCard.SquadId = string.Empty;
            squadCard.SquadIcon = string.Empty;
            squadCard.MemberName = string.Empty;
            Dirty(cardUid, squadCard);
        }

        UpdateUiState(uid, comp);
    }

    private void OnCreateSquad(EntityUid uid, PatrolTabletComponent comp, PatrolTabletCreateSquadMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        var name = SanitizeSquadName(msg.Name);
        if (name == null || !IsValidSquadIcon(msg.IconId) || comp.Squads.Count >= MaxSquads)
            return;

        var id = $"squad_{Guid.NewGuid():N}"[..16];
        comp.Squads.Add(new SquadData(id, name, msg.IconId));
        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void OnDeleteSquad(EntityUid uid, PatrolTabletComponent comp, PatrolTabletDeleteSquadMessage msg)
    {
        if (!comp.SquadManagementEnabled)
            return;

        var squad = comp.Squads.Find(s => s.Id == msg.SquadId);
        if (squad == null)
            return;

        comp.Squads.Remove(squad);

        var query = EntityQueryEnumerator<PatrolSquadCardComponent>();
        while (query.MoveNext(out var cardUid, out var squadCard))
        {
            if (squadCard.SquadId != msg.SquadId)
                continue;

            squadCard.SquadId = string.Empty;
            squadCard.SquadIcon = string.Empty;
            squadCard.MemberName = string.Empty;
            Dirty(cardUid, squadCard);
        }

        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void OnSendAnnouncement(EntityUid uid, PatrolTabletComponent comp, PatrolTabletSendAnnouncementMessage msg)
    {
        if (!CanSendAnnouncement(msg.Actor))
        {
            _popup.PopupEntity(Loc.GetString("patrol-tablet-announcement-access-denied"), uid, msg.Actor);
            return;
        }

        if (GetAnnouncementBusyRemaining(comp) > 0f)
        {
            UpdateAllTabletUiStates();
            return;
        }

        if (_timing.CurTime < comp.NextAnnouncementTime)
        {
            UpdateUiState(uid, comp);
            return;
        }

        var title = NormalizeAnnouncementPart(msg.Title, MaxAnnouncementTitleLength);
        var text = NormalizeAnnouncementPart(msg.Text, MaxAnnouncementTextLength);

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(text))
            return;

        if (string.IsNullOrEmpty(title))
            SplitAnnouncementText(ref title, ref text);

        title = title.ToUpperInvariant();

        var senderName = MetaData(msg.Actor).EntityName;
        var senderJobTitle = Loc.GetString("patrol-tablet-announcement-unknown-job");

        if (IsAdminGhost(msg.Actor))
        {
            senderJobTitle = "Administrator";
        }
        else if (_idCard.TryFindIdCard(msg.Actor, out var senderIdCard))
        {
            senderName = senderIdCard.Comp.FullName ?? senderName;
            senderJobTitle = senderIdCard.Comp.LocalizedJobTitle ?? senderJobTitle;
        }

        var recipients = new List<ICommonSession>();
        var senderMap = Transform(msg.Actor).MapUid;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            if (IsAdminGhost(playerEntity))
            {
                recipients.Add(session);
                continue;
            }

            if (!TryComp<MobStateComponent>(playerEntity, out var mobState) ||
                mobState.CurrentState != MobState.Alive)
            {
                continue;
            }

            if (Transform(playerEntity).MapUid != senderMap)
                continue;

            if (!WearsAnnouncementEquipment(playerEntity, comp))
                continue;

            recipients.Add(session);
        }

        if (recipients.Count == 0)
            return;

        var duration = Math.Max(comp.AnnouncementDuration, 0f);
        var cooldown = Math.Max(comp.AnnouncementCooldown, 0f);

        var busyUntil = _timing.CurTime + TimeSpan.FromSeconds(duration);
        foreach (var equipmentId in comp.AnnouncementRequiredEquipment)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
                continue;

            _announcementBusyUntilByEquipment[equipmentId] = busyUntil;
        }

        comp.NextAnnouncementTime = _timing.CurTime + TimeSpan.FromSeconds(cooldown);

        foreach (var session in recipients)
        {
            RaiseNetworkEvent(new GhostRoleIntroductionEvent(
                title,
                text,
                comp.AnnouncementTextColor,
                comp.AnnouncementFont,
                comp.AnnouncementFontSize,
                comp.AnnouncementTitleFontSize,
                comp.AnnouncementDuration,
                comp.AnnouncementFadeFromBlackDuration,
                comp.AnnouncementFadeOutDuration,
                comp.AnnouncementTextDelay,
                comp.AnnouncementCharactersPerSecond,
                comp.AnnouncementShowBlackBackground,
                comp.AnnouncementTypeTitle,
                senderName,
                senderJobTitle,
                comp.AnnouncementSenderFontSize,
                targetedAnnouncement: true,
                announcementSound: comp.AnnouncementSound,
                interferenceSound: comp.AnnouncementInterferenceSound,
                interferenceDuration: comp.AnnouncementInterferenceDuration),
                session);
        }

        UpdateAllTabletUiStates();
    }

    private float GetAnnouncementBusyRemaining(PatrolTabletComponent comp)
    {
        var maxRemaining = 0f;

        foreach (var equipmentId in comp.AnnouncementRequiredEquipment)
        {
            if (string.IsNullOrWhiteSpace(equipmentId) ||
                !_announcementBusyUntilByEquipment.TryGetValue(equipmentId, out var busyUntil))
            {
                continue;
            }

            var remaining = (float)(busyUntil - _timing.CurTime).TotalSeconds;
            if (remaining > maxRemaining)
                maxRemaining = remaining;
        }

        return Math.Max(0f, maxRemaining);
    }

    private bool CanSendAnnouncement(EntityUid actor)
    {
        // Admin ghosts can send announcements without an ID card or living-mob state.
        if (IsAdminGhost(actor))
            return true;

        if (!TryComp<MobStateComponent>(actor, out var mobState) ||
            mobState.CurrentState != MobState.Alive)
        {
            return false;
        }

        if (!_idCard.TryFindIdCard(actor, out var idCard))
            return false;

        if (!TryComp<AccessComponent>(idCard.Owner, out var access) || !access.Enabled)
            return false;

        foreach (var tag in access.Tags)
        {
            if (tag.Id == HeadOfSecurityAccess || tag.Id == CaptainAccess)
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

    private void UpdateAllTabletUiStates()
    {
        var query = EntityQueryEnumerator<PatrolTabletComponent>();
        while (query.MoveNext(out var tablet, out var tabletComp))
            UpdateUiState(tablet, tabletComp);
    }

    private bool WearsAnnouncementEquipment(EntityUid wearer, PatrolTabletComponent comp)
    {
        if (comp.AnnouncementRequiredEquipment.Count == 0 ||
            !TryComp<InventoryComponent>(wearer, out var inventory))
        {
            return false;
        }

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item))
        {
            var prototypeId = MetaData(item).EntityPrototype?.ID;
            if (prototypeId != null && comp.AnnouncementRequiredEquipment.Contains(prototypeId))
                return true;
        }

        return false;
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
        var titleWordCount = Math.Min(AnnouncementTitleWords, words.Length);

        title = string.Join(" ", words, 0, titleWordCount);
        text = titleWordCount < words.Length
            ? string.Join(" ", words, titleWordCount, words.Length - titleWordCount)
            : string.Empty;
    }

    private void SetSquadOnIdCard(EntityUid target, string squadId, string squadIcon)
    {
        if (!_idCard.TryFindIdCard(target, out var idCard))
            return;

        var name = idCard.Comp.FullName ?? MetaData(target).EntityName;

        var squadCard = EnsureComp<PatrolSquadCardComponent>(idCard.Owner);
        squadCard.SquadId = squadId;
        squadCard.SquadIcon = squadIcon;
        squadCard.MemberName = name;
        Dirty(idCard.Owner, squadCard);
    }

    private void UpdateUiState(EntityUid uid, PatrolTabletComponent comp)
    {
        if (!_ui.HasUi(uid, PatrolTabletUiKey.Key))
            return;

        var officers = new List<PatrolOfficerInfo>();
        var squads = GetSquads(comp);

        foreach (var netEntity in comp.TrackedPersonnel)
        {
            var target = GetEntity(netEntity);
            if (!Exists(target))
                continue;

            var name = "Unknown";
            var jobTitle = "Security";

            if (TryComp(target, out MetaDataComponent? meta))
                name = meta.EntityName;

            var squadId = string.Empty;
            var squadIcon = string.Empty;

            if (_idCard.TryFindIdCard(target, out var idCard))
            {
                if (idCard.Comp.FullName != null)
                    name = idCard.Comp.FullName;

                jobTitle = idCard.Comp.LocalizedJobTitle ?? "Security";

                if (TryComp<PatrolSquadCardComponent>(idCard.Owner, out var squadCard))
                {
                    squadId = squadCard.SquadId;
                    squadIcon = squadCard.SquadIcon;
                }
            }

            var info = new PatrolOfficerInfo(
                GetNetEntity(target).ToString(),
                name,
                jobTitle)
            {
                SquadId = squadId,
                SquadIcon = squadIcon,
            };

            officers.Add(info);
        }

        var squadMemberNames = new Dictionary<string, HashSet<string>>();

        var cardQuery = EntityQueryEnumerator<PatrolSquadCardComponent>();
        while (cardQuery.MoveNext(out _, out var squadCard))
        {
            if (string.IsNullOrEmpty(squadCard.SquadId) || string.IsNullOrEmpty(squadCard.MemberName))
                continue;

            if (!squadMemberNames.TryGetValue(squadCard.SquadId, out var memberSet))
            {
                memberSet = new HashSet<string>();
                squadMemberNames[squadCard.SquadId] = memberSet;
            }

            memberSet.Add(squadCard.MemberName);
        }

        foreach (var squad in squads)
        {
            if (squadMemberNames.TryGetValue(squad.SquadId, out var members))
            {
                squad.AssignedCount = members.Count;
                squad.Members.AddRange(members);
            }
        }

        var busyRemaining = GetAnnouncementBusyRemaining(comp);
        var cooldownRemaining = Math.Max(
            0f,
            (float)(comp.NextAnnouncementTime - _timing.CurTime).TotalSeconds);

        _ui.SetUiState(uid, PatrolTabletUiKey.Key,
            new PatrolTabletUpdateState(
                officers,
                squads,
                comp.SquadManagementEnabled,
                busyRemaining,
                cooldownRemaining));
    }

    private List<PatrolSquadDef> GetSquads(PatrolTabletComponent comp)
    {
        var result = new List<PatrolSquadDef>();

        foreach (var squad in comp.Squads)
            result.Add(new PatrolSquadDef(squad.Id, squad.Name, squad.IconId));

        return result;
    }
}
