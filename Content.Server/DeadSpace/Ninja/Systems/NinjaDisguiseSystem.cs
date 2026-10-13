// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Coordinates;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaDisguiseSystem : SharedNinjaDisguiseSystem
{
    private static readonly ProtoId<JobIconPrototype> JobIconNoId = "JobIconNoId";

    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private const float ScanRange = 1.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseScanEvent>(OnScan);
        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseScanDoAfterEvent>(OnScanDoAfter);
        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseApplyDoAfterEvent>(OnApplyDoAfter);
        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseMenuEvent>(OnOpenMenu);
        SubscribeLocalEvent<NinjaDisguiseComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<NinjaDisguiseComponent, GotUnequippedEvent>(OnSuitUnequipped);
        SubscribeLocalEvent<NinjaDisguiseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseRevealedEvent>(OnRevealed);
        SubscribeLocalEvent<NinjaDisguiseComponent, NinjaDisguiseStripAttemptEvent>(OnStripAttempt);

        Subs.BuiEvents<NinjaDisguiseComponent>(NinjaDisguiseUiKey.Key, subs =>
        {
            subs.Event<NinjaDisguiseApplyMessage>(OnBuiApply);
            subs.Event<NinjaDisguiseResetMessage>(OnBuiReset);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaDisguiseComponent>();
        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.Disguised)
                continue;

            if (!TryGetWearer(suitUid, out var wearer))
                continue;

            var cost = comp.DrainRate * frameTime;
            if (cost > 0f && !_ninja.TryUseCharge(wearer, cost))
                ResetDisguise((suitUid, comp));
        }
    }

    private void OnScan(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseScanEvent args)
    {
        var (suitUid, comp) = ent;

        if (!IsAuthorized(suitUid, args.Performer))
            return;

        if (!_ninja.IsNinja(args.Performer))
        {
            _popup.PopupEntity(Loc.GetString("ninja-disguise-scan-fail-not-ninja"), args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out _))
        {
            _popup.PopupEntity(Loc.GetString("ninja-disguise-scan-fail-not-humanoid"), args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        if (!_transform.InRange(Transform(args.Performer).Coordinates, Transform(args.Target).Coordinates, ScanRange))
        {
            _popup.PopupEntity(Loc.GetString("ninja-disguise-scan-fail-out-of-range"), args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        if (!_ninja.TryUseCharge(args.Performer, comp.EnergyCost))
        {
            _popup.PopupEntity(Loc.GetString("ninja-disguise-scan-fail-energy"), args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.Performer, TimeSpan.FromSeconds(comp.ScanDelay),
            new NinjaDisguiseScanDoAfterEvent(), suitUid, target: args.Target, used: suitUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = ScanRange,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
            return;

        comp.ActiveDoAfter = doAfterId;
        args.Handled = true;
    }

    private void OnScanDoAfter(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseScanDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Args.Target is not { } target)
            return;

        SaveScan(ent, target, args.Args.User);
        args.Handled = true;
    }

    private void SaveScan(Entity<NinjaDisguiseComponent> ent, EntityUid target, EntityUid performer)
    {
        var (suitUid, comp) = ent;

        if (!_transform.InRange(Transform(performer).Coordinates, Transform(target).Coordinates, ScanRange) ||
            !TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
        {
            return;
        }

        var entry = new NinjaDisguiseEntry
        {
            Name = MetaData(target).EntityName,
            Description = MetaData(target).EntityDescription,
            Appearance = CaptureAppearance(humanoid),
        };

        CaptureClothing(entry, target);
        if (TryComp<InventoryComponent>(target, out var targetInventory))
            entry.InventorySpeciesId = targetInventory.SpeciesId;

        if (_idCard.TryFindIdCard(target, out var targetId))
        {
            entry.HasIdCard = true;
            entry.JobTitle = targetId.Comp.LocalizedJobTitle;
            entry.JobIcon = targetId.Comp.JobIcon;
            entry.IdCardName = targetId.Comp.FullName;
            entry.IdCardJobTitle = targetId.Comp.LocalizedJobTitle;
            entry.IdCardJobIcon = targetId.Comp.JobIcon;
            entry.IdCardJobPrototype = targetId.Comp.JobPrototype;
            entry.IdCardJobDepartments = new List<ProtoId<DepartmentPrototype>>(targetId.Comp.JobDepartments);
        }

        comp.Entries.Add(entry);
        if (comp.Entries.Count > comp.MaxEntries)
            comp.Entries.RemoveAt(0);

        Dirty(suitUid, comp);
        UpdateUiIfOpen(suitUid, comp);

        _popup.PopupEntity(Loc.GetString("ninja-disguise-scan-success", ("name", entry.Name)), performer, performer, PopupType.Medium);
    }

    private void CaptureClothing(NinjaDisguiseEntry entry, EntityUid target)
    {
        if (TryComp<IdentityBlockerComponent>(target, out var targetBlocker) && targetBlocker.Enabled)
            entry.IdentityBlockedCoverage |= targetBlocker.Coverage;

        var enumerator = _inventory.GetSlotEnumerator(target);
        while (enumerator.NextItem(out var item, out var slot))
        {
            if (TryComp<IdentityBlockerComponent>(item, out var itemBlocker) && itemBlocker.Enabled)
                entry.IdentityBlockedCoverage |= itemBlocker.Coverage;

            if (MetaData(item).EntityPrototype is not { } proto)
                continue;

            entry.Inventory.Add(new NinjaDisguiseInventoryEntry
            {
                Slot = slot.Name,
                ItemId = proto.ID,
                SlotOffset = slot.Offset
            });

            if (!HasComp<ClothingComponent>(item))
                continue;

            if (TryComp(item, out HideLayerClothingComponent? hide) &&
                IsHideLayerEnabled(item, hide))
            {
                foreach (var hiddenSlot in hide.ClothingSlots)
                    entry.HiddenClothingSlots.Add(hiddenSlot);
            }
        }
    }

    private bool IsHideLayerEnabled(EntityUid item, HideLayerClothingComponent hide)
    {
        if (!hide.HideOnToggle)
            return true;

        if (!TryComp<MaskComponent>(item, out var mask))
            return true;

        return !mask.IsToggled;
    }

    private void OnOpenMenu(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseMenuEvent args)
    {
        if (!IsAuthorized(ent.Owner, args.Performer))
            return;

        _ui.TryToggleUi(ent.Owner, NinjaDisguiseUiKey.Key, args.Performer);
    }

    private void OnBuiOpened(Entity<NinjaDisguiseComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not NinjaDisguiseUiKey.Key)
            return;

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnBuiApply(Entity<NinjaDisguiseComponent> suit, ref NinjaDisguiseApplyMessage args)
    {
        if (!IsAuthorized(suit.Owner, args.Actor) ||
            args.Index < 0 ||
            args.Index >= suit.Comp.Entries.Count)
        {
            return;
        }

        if (!TryGetWearer(suit.Owner, out var wearer))
            return;

        if (suit.Comp.Effect is { } effectId && suit.Comp.EffectEntity == null)
        {
            suit.Comp.EffectEntity = SpawnAttachedTo(effectId, wearer.ToCoordinates());
        }

        var doAfter = new DoAfterArgs(EntityManager, wearer, TimeSpan.FromSeconds(suit.Comp.ApplyDelay),
            new NinjaDisguiseApplyDoAfterEvent(), suit.Owner, used: suit.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
        {
            DeleteDisguiseEffect(suit.Comp);
            return;
        }

        suit.Comp.ActiveDoAfter = doAfterId;
        suit.Comp.PendingIndex = args.Index;
    }

    private void OnApplyDoAfter(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseApplyDoAfterEvent args)
    {
        var (suitUid, comp) = ent;

        comp.ActiveDoAfter = null;

        if (args.Cancelled || comp.PendingIndex is not { } index)
        {
            DeleteDisguiseEffect(comp);
            comp.PendingIndex = null;
            args.Handled = true;
            return;
        }

        comp.PendingIndex = null;

        if (!IsAuthorized(suitUid, args.Args.User) || !TryGetWearer(suitUid, out var wearer))
        {
            DeleteDisguiseEffect(comp);
            args.Handled = true;
            return;
        }

        DeleteDisguiseEffect(comp);
        ApplyDisguise(ent, index);
        args.Handled = true;
    }

    private void DeleteDisguiseEffect(NinjaDisguiseComponent comp)
    {
        if (comp.EffectEntity is not { } effect)
            return;

        if (Exists(effect))
            Del(effect);

        comp.EffectEntity = null;
    }

    private void OnBuiReset(Entity<NinjaDisguiseComponent> suit, ref NinjaDisguiseResetMessage args)
    {
        if (!IsAuthorized(suit.Owner, args.Actor))
            return;

        ResetDisguise(suit);
    }

    private void OnSuitUnequipped(Entity<NinjaDisguiseComponent> ent, ref GotUnequippedEvent args)
    {
        ResetDisguise(ent);
    }

    private void OnShutdown(Entity<NinjaDisguiseComponent> ent, ref ComponentShutdown args)
    {
        ResetDisguise(ent);
    }

    private void OnRevealed(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseRevealedEvent args)
    {
        ResetDisguise(ent);
    }

    private void OnStripAttempt(Entity<NinjaDisguiseComponent> ent, ref NinjaDisguiseStripAttemptEvent args)
    {
        _popup.PopupEntity(Loc.GetString("ninja-disguise-strip-attempt"), args.Wearer, args.Wearer, PopupType.MediumCaution);
    }

    private void ApplyDisguise(Entity<NinjaDisguiseComponent> ent, int index)
    {
        var (suitUid, comp) = ent;

        if (index < 0 || index >= comp.Entries.Count)
            return;

        var entry = comp.Entries[index];
        if (entry.Appearance == null)
            return;

        if (!TryGetWearer(suitUid, out var wearer))
            return;

        if (!comp.Disguised)
            comp.OriginalAppearance = CaptureSelf(wearer);

        ApplyEntry(wearer, entry);

        MimicIdCard(wearer, entry);
        SetIdentityBlocker(wearer, entry.IdentityBlockedCoverage);
        NeutralizeWearerIdentityBlockers(wearer, comp);

        comp.Disguised = true;
        comp.ActiveIndex = index;
        Dirty(suitUid, comp);
        UpdateUiIfOpen(suitUid, comp);

        _popup.PopupEntity(Loc.GetString("ninja-disguise-applied", ("name", entry.Name)), wearer, wearer, PopupType.Medium);
    }

    private void ResetDisguise(Entity<NinjaDisguiseComponent> ent)
    {
        var (suitUid, comp) = ent;

        if (comp.ActiveDoAfter is { } doAfterId)
        {
            _doAfter.Cancel(doAfterId);
            comp.ActiveDoAfter = null;
            comp.PendingIndex = null;
        }

        DeleteDisguiseEffect(comp);

        if (!comp.Disguised)
            return;

        RestoreWearerIdentityBlockers(comp);

        if (TryGetWearer(suitUid, out var wearer) && comp.OriginalAppearance is { } original)
        {
            ApplyEntry(wearer, original);
            MimicIdCard(wearer, original);
            SetIdentityBlocker(wearer, original.IdentityBlockedCoverage);
        }

        comp.OriginalAppearance = null;
        comp.Disguised = false;
        comp.ActiveIndex = null;
        Dirty(suitUid, comp);
        UpdateUiIfOpen(suitUid, comp);

        if (TryGetWearer(suitUid, out var current))
            _popup.PopupEntity(Loc.GetString("ninja-disguise-reset"), current, current, PopupType.Medium);
    }

    private NinjaDisguiseEntry CaptureSelf(EntityUid wearer)
    {
        var entry = new NinjaDisguiseEntry
        {
            Name = MetaData(wearer).EntityName,
            Description = MetaData(wearer).EntityDescription,
        };

        if (TryComp<HumanoidAppearanceComponent>(wearer, out var humanoid))
            entry.Appearance = CaptureAppearance(humanoid);

        CaptureClothing(entry, wearer);

        if (TryComp<InventoryComponent>(wearer, out var inventory))
            entry.InventorySpeciesId = inventory.SpeciesId;

        if (TryComp<IdentityBlockerComponent>(wearer, out var ownBlocker) && ownBlocker.Enabled)
            entry.IdentityBlockedCoverage = ownBlocker.Coverage;

        if (_idCard.TryFindIdCard(wearer, out var wearerId))
        {
            entry.HasIdCard = true;
            entry.IdCardName = wearerId.Comp.FullName;
            entry.IdCardJobTitle = wearerId.Comp.LocalizedJobTitle;
            entry.IdCardJobIcon = wearerId.Comp.JobIcon;
            entry.IdCardJobPrototype = wearerId.Comp.JobPrototype;
            entry.IdCardJobDepartments = new List<ProtoId<DepartmentPrototype>>(wearerId.Comp.JobDepartments);
        }

        return entry;
    }

    private void ApplyEntry(EntityUid target, NinjaDisguiseEntry entry)
    {
        _meta.SetEntityName(target, entry.Name);
        _meta.SetEntityDescription(target, entry.Description);

        if (entry.Appearance is { } appearance)
        {
            var humanoid = EnsureComp<HumanoidAppearanceComponent>(target);
            ApplyAppearance((target, humanoid), appearance);
            Dirty(target, humanoid);

            if (TryComp<InventoryComponent>(target, out var inventory))
                _inventory.SetInventorySpecies(target, entry.InventorySpeciesId ?? appearance.Species.Id, inventory);
        }
    }

    private bool TryGetWearer(EntityUid suitUid, [NotNullWhen(true)] out EntityUid wearer)
    {
        wearer = Transform(suitUid).ParentUid;
        return wearer.IsValid() && Exists(wearer);
    }

    private void MimicIdCard(EntityUid target, NinjaDisguiseEntry entry)
    {
        if (!_idCard.TryFindIdCard(target, out var card))
            return;

        if (entry.HasIdCard)
        {
            _idCard.TryChangeFullName(card.Owner, entry.IdCardName, card.Comp);
            _idCard.TryChangeJobTitle(card.Owner, entry.IdCardJobTitle, card.Comp);

            if (entry.IdCardJobIcon is { } icon)
                _idCard.TryChangeJobIcon(card.Owner, _proto.Index<JobIconPrototype>(icon.Id), card.Comp);

            _idCard.TryChangeJobDepartment(card.Owner, entry.IdCardJobDepartments, card.Comp);
        }
        else
        {
            _idCard.TryChangeFullName(card.Owner, null, card.Comp);
            _idCard.TryChangeJobTitle(card.Owner, null, card.Comp);
            _idCard.TryChangeJobIcon(card.Owner, _proto.Index(JobIconNoId), card.Comp);
            _idCard.TryChangeJobDepartment(card.Owner, new List<ProtoId<DepartmentPrototype>>(), card.Comp);
        }

        card.Comp.JobPrototype = entry.HasIdCard ? entry.IdCardJobPrototype : null;
        Dirty(card.Owner, card.Comp);
    }

    private void SetIdentityBlocker(EntityUid target, IdentityBlockerCoverage coverage)
    {
        if (coverage == IdentityBlockerCoverage.NONE)
        {
            RemComp<IdentityBlockerComponent>(target);
        }
        else
        {
            var blocker = EnsureComp<IdentityBlockerComponent>(target);
            blocker.Enabled = true;
            blocker.Coverage = coverage;
            Dirty(target, blocker);
        }

        _identity.QueueIdentityUpdate(target);
    }

    private void NeutralizeWearerIdentityBlockers(EntityUid wearer, NinjaDisguiseComponent comp)
    {
        RestoreWearerIdentityBlockers(comp);

        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (!TryComp<IdentityBlockerComponent>(item, out var blocker) || !blocker.Enabled)
                continue;

            blocker.Enabled = false;
            Dirty(item, blocker);
            comp.DisabledIdentityBlockers.Add(item);
        }
    }

    private void RestoreWearerIdentityBlockers(NinjaDisguiseComponent comp)
    {
        foreach (var item in comp.DisabledIdentityBlockers)
        {
            if (Exists(item) && TryComp<IdentityBlockerComponent>(item, out var blocker))
            {
                blocker.Enabled = true;
                Dirty(item, blocker);
            }
        }

        comp.DisabledIdentityBlockers.Clear();
    }

    private bool IsAuthorized(EntityUid suitUid, EntityUid actor)
    {
        return TryGetWearer(suitUid, out var wearer) && wearer == actor;
    }

    private void UpdateUiIfOpen(EntityUid suitUid, NinjaDisguiseComponent comp)
    {
        if (_ui.IsUiOpen(suitUid, NinjaDisguiseUiKey.Key))
            UpdateUi(suitUid, comp);
    }

    private void UpdateUi(EntityUid suitUid, NinjaDisguiseComponent comp)
    {
        _ui.SetUiState(suitUid, NinjaDisguiseUiKey.Key,
            new NinjaDisguiseState(new List<NinjaDisguiseEntry>(comp.Entries), comp.Disguised, comp.ActiveIndex));
    }
}
