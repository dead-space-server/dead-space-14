// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Notify;
using Content.Server.Speech.Components;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Surgery;

public sealed partial class SurgeryMmiSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SurgeryOrganFreshSystem _fresh = default!;
    [Dependency] private ReturnToBodyNotificationSystem _returnToBody = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private BlindableSystem _blind = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BrainComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BrainComponent, EntGotInsertedIntoContainerMessage>(OnBrainInserted);
        SubscribeLocalEvent<MMIComponent, PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<MMIComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<MMIComponent, EntGotInsertedIntoContainerMessage>(OnMmiIn);
        SubscribeLocalEvent<MMIComponent, EntGotRemovedFromContainerMessage>(OnMmiOut);
        SubscribeLocalEvent<SurgeryCavityComponent, DidEquipEvent>(OnClothes);
        SubscribeLocalEvent<SurgeryCavityComponent, DidUnequipEvent>(OnClothesOff);
    }

    private void OnInsertAttempt(Entity<BrainComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<MMIComponent>(args.SlotEntity) || _fresh.Spoil(ent) < 1f)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("surgery-mmi-rotten"), args.SlotEntity, user);
    }

    private void OnBrainInserted(Entity<BrainComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var mmi = args.Container.Owner;
        if (!HasComp<MMIComponent>(mmi))
            return;

        Refresh(mmi);
        _returnToBody.Notify(mmi);
    }

    private void OnAttached(Entity<MMIComponent> ent, ref PlayerAttachedEvent args)
    {
        Refresh(ent);
    }

    private void OnDetached(Entity<MMIComponent> ent, ref PlayerDetachedEvent args)
    {
        _appearance.SetData(ent, MMIVisuals.HasMind, false);
    }

    private void Refresh(EntityUid mmi)
    {
        var alive = _mind.TryGetMind(mmi, out _, out var mind) && mind.CurrentEntity == mmi && mind.UserId != null;
        _appearance.SetData(mmi, MMIVisuals.HasMind, alive);
    }

    private void OnMmiIn(Entity<MMIComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != SurgeryCavityComponent.Abdomen)
            return;

        EnsureComp<SurgeryMmiBellyComponent>(args.Container.Owner);
        Cover(args.Container.Owner, ent);
    }

    private void OnMmiOut(Entity<MMIComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != SurgeryCavityComponent.Abdomen)
            return;

        Uncover(ent);
        RemComp<SurgeryMmiBellyComponent>(args.Container.Owner);
    }

    private void OnClothes(Entity<SurgeryCavityComponent> ent, ref DidEquipEvent args)
    {
        if (Belly(ent, out var mmi))
            Cover(ent.Owner, mmi);
    }

    private void OnClothesOff(Entity<SurgeryCavityComponent> ent, ref DidUnequipEvent args)
    {
        if (Belly(ent, out var mmi))
            Cover(ent.Owner, mmi);
    }

    private bool Belly(EntityUid body, out EntityUid mmi)
    {
        mmi = default;
        if (!_containers.TryGetContainer(body, SurgeryCavityComponent.Abdomen, out var cavity))
            return false;

        foreach (var item in cavity.ContainedEntities)
        {
            if (!HasComp<MMIComponent>(item))
                continue;

            mmi = item;
            return true;
        }

        return false;
    }

    private void Cover(EntityUid body, EntityUid mmi)
    {
        if (!LimbCoverRules.Worn(body, LimbZone.Groin, EntityManager, _inventory))
        {
            RemComp<MumbleAccentComponent>(mmi);
            RemComp<TemporaryBlindnessComponent>(mmi);
            _blind.UpdateIsBlind(mmi);
            return;
        }

        EnsureComp<MumbleAccentComponent>(mmi);
        EnsureComp<BlindableComponent>(mmi);
        EnsureComp<TemporaryBlindnessComponent>(mmi);
        _blind.UpdateIsBlind(mmi);
    }

    private void Uncover(EntityUid mmi)
    {
        RemComp<MumbleAccentComponent>(mmi);
        RemComp<TemporaryBlindnessComponent>(mmi);
        _blind.UpdateIsBlind(mmi);
        RemComp<BlindableComponent>(mmi);
    }
}
