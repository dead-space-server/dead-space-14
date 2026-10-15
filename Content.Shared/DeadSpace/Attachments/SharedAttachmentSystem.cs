// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Attachments;

/// <summary>
/// Stat modifiers, unique groups and examine text of <see cref="AttachmentComponent"/>s.
/// Component swaps and effects happen on the server, overlays on the client.
/// </summary>
public abstract class SharedAttachmentSystem : EntitySystem
{
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttachmentHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AttachmentHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<AttachmentHolderComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<AttachmentHolderComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<AttachmentHolderComponent, GetMeleeAttackRateEvent>(OnGetAttackRate);
        SubscribeLocalEvent<AttachmentHolderComponent, GunRefreshModifiersEvent>(OnGunRefresh);
        SubscribeLocalEvent<AttachmentHolderComponent, ExaminedEvent>(OnHolderExamined);
        SubscribeLocalEvent<AttachmentComponent, ExaminedEvent>(OnAttachmentExamined);
        SubscribeLocalEvent<AttachmentDamageShieldComponent, DamageModifyEvent>(OnShieldDamageModify);
        SubscribeLocalEvent<AttachmentHolderComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<AttachmentHolderComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    /// <summary>
    /// tg bayonet: left click on a creature within reach stabs it with the fixed knife instead of shooting.
    /// </summary>
    private void OnShotAttempted(Entity<AttachmentHolderComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled ||
            GetSlotItem(ent, ent.Comp.BayonetSlot) == null ||
            args.Used.Comp.Target is not { } target ||
            target == args.User ||
            !HasComp<Content.Shared.Mobs.Components.MobStateComponent>(target) ||
            !TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            return;
        }

        var userXform = Transform(args.User);
        var targetXform = Transform(target);
        if (userXform.MapID != targetXform.MapID ||
            (_transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(targetXform)).Length() > melee.Range + 0.3f)
        {
            return;
        }

        args.Cancel();
        _melee.AttemptLightAttack(args.User, ent, melee, target);
    }

    private void OnGetItemActions(Entity<AttachmentHolderComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.LightSlot != null && args.InHands)
            args.AddAction(ref ent.Comp.LightActionEntity, ent.Comp.LightAction);
    }

    /// <summary>Attachments currently in the holder's attachment slots.</summary>
    public IEnumerable<Entity<AttachmentComponent>> GetAttachments(Entity<AttachmentHolderComponent> holder)
    {
        foreach (var slotId in holder.Comp.Slots)
        {
            if (!_itemSlots.TryGetSlot(holder, slotId, out var slot) ||
                slot.Item is not { } item ||
                !TryComp<AttachmentComponent>(item, out var attachment))
            {
                continue;
            }

            yield return (item, attachment);
        }
    }

    protected bool IsAttachmentSlot(AttachmentHolderComponent holder, BaseContainer container)
    {
        return holder.Slots.Contains(container.ID);
    }

    private void OnInserted(Entity<AttachmentHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!IsAttachmentSlot(ent.Comp, args.Container))
            return;

        // Container messages are replayed on the client while applying states; only the server owns the result.
        if (_net.IsServer)
        {
            OnSlotFilled(ent, args.Container.ID, args.Entity);
            if (TryComp<AttachmentComponent>(args.Entity, out var attachment))
                OnAttached(ent, (args.Entity, attachment));
        }

        AttachmentsChanged(ent);
    }

    private void OnRemoved(Entity<AttachmentHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!IsAttachmentSlot(ent.Comp, args.Container))
            return;

        if (_net.IsServer)
        {
            if (TryComp<AttachmentComponent>(args.Entity, out var attachment))
                OnDetached(ent, (args.Entity, attachment));
            OnSlotEmptied(ent, args.Container.ID, args.Entity);
        }

        AttachmentsChanged(ent);
    }

    /// <summary>Server: any item went into an attachment slot (slot components, bayonet, flashlight).</summary>
    protected virtual void OnSlotFilled(Entity<AttachmentHolderComponent> holder, string slot, EntityUid item)
    {
    }

    protected virtual void OnSlotEmptied(Entity<AttachmentHolderComponent> holder, string slot, EntityUid item)
    {
    }

    /// <summary>The item sitting in a slot of the holder, if any.</summary>
    public EntityUid? GetSlotItem(EntityUid holder, string? slot)
    {
        if (slot == null || !_itemSlots.TryGetSlot(holder, slot, out var itemSlot))
            return null;
        return itemSlot.Item;
    }

    /// <summary>Server: swap components and refresh stats.</summary>
    protected virtual void OnAttached(Entity<AttachmentHolderComponent> holder, Entity<AttachmentComponent> attachment)
    {
    }

    /// <summary>Server: restore components and refresh stats.</summary>
    protected virtual void OnDetached(Entity<AttachmentHolderComponent> holder, Entity<AttachmentComponent> attachment)
    {
    }

    /// <summary>Both sides, after any attachment change. The client redraws overlays here.</summary>
    protected virtual void AttachmentsChanged(Entity<AttachmentHolderComponent> holder)
    {
        if (HasComp<GunComponent>(holder))
            _gun.RefreshModifiers(holder.Owner);
    }

    private void OnInsertAttempt(Entity<AttachmentHolderComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled ||
            !IsAttachmentSlot(ent.Comp, args.Container) ||
            !TryComp<AttachmentComponent>(args.EntityUid, out var inserting) ||
            inserting.UniqueGroup is not { } group)
        {
            return;
        }

        foreach (var attached in GetAttachments(ent))
        {
            if (attached.Owner == args.EntityUid || attached.Comp.UniqueGroup != group)
                continue;

            args.Cancel();
            return;
        }
    }

    private void OnGetMeleeDamage(Entity<AttachmentHolderComponent> ent, ref GetMeleeDamageEvent args)
    {
        var wielded = TryComp<WieldableComponent>(ent, out var wieldable) && wieldable.Wielded;
        var multiplier = 1f;
        foreach (var attachment in GetAttachments(ent))
        {
            if (attachment.Comp.BonusDamage != null)
                args.Damage += attachment.Comp.BonusDamage;
            if (wielded && attachment.Comp.WieldedBonusDamage != null)
                args.Damage += attachment.Comp.WieldedBonusDamage;
            multiplier *= attachment.Comp.DamageMultiplier;
        }

        if (!MathHelper.CloseTo(multiplier, 1f))
            args.Damage *= multiplier;
    }

    private void OnGetAttackRate(Entity<AttachmentHolderComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        foreach (var attachment in GetAttachments(ent))
        {
            args.Rate += attachment.Comp.BonusAttackRate;
            args.Multipliers *= attachment.Comp.AttackRateMultiplier;
        }
    }

    private void OnGunRefresh(Entity<AttachmentHolderComponent> ent, ref GunRefreshModifiersEvent args)
    {
        foreach (var attachment in GetAttachments(ent))
        {
            args.FireRate *= attachment.Comp.FireRateMultiplier;
            args.ProjectileSpeed *= attachment.Comp.ProjectileSpeedMultiplier;
        }
    }

    private void OnHolderExamined(Entity<AttachmentHolderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(AttachmentHolderComponent)))
        {
            foreach (var attachment in GetAttachments(ent))
            {
                args.PushMarkup(Loc.GetString("attachment-holder-examine-entry", ("name", Name(attachment))));
                if (attachment.Comp.Description is { } description)
                    args.PushMarkup(Loc.GetString(description));
            }
        }
    }

    private void OnAttachmentExamined(Entity<AttachmentComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Description is { } description)
            args.PushMarkup(Loc.GetString("attachment-examine-effect", ("effect", Loc.GetString(description))));
    }

    private void OnShieldDamageModify(Entity<AttachmentDamageShieldComponent> ent, ref DamageModifyEvent args)
    {
        if (Timing.CurTime >= ent.Comp.EndTime)
            return;

        args.Damage *= ent.Comp.Coefficient;
    }
}
