// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Light.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.DeadSpace.Attachments;

public sealed class AttachmentSystem : SharedAttachmentSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly Content.Shared.PowerCell.PowerCellSystem _powerCell = default!;
    [Dependency] private readonly Content.Shared.Power.EntitySystems.SharedBatterySystem _battery = default!;
    [Dependency] private readonly Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;

    private readonly List<EntityUid> _expiredShields = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttachmentHolderComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AttachmentHolderComponent, DamageMarkerDetonatedEvent>(OnMarkDetonated);
        SubscribeLocalEvent<AttachmentHolderComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<AttachmentHolderComponent, AttachmentToggleLightActionEvent>(OnToggleLightAction);
        Subs.BuiEvents<AttachmentHolderComponent>(AttachmentUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<AttachmentInsertMessage>(OnInsertMessage);
            subs.Event<AttachmentEjectMessage>(OnEjectMessage);
        });
    }

    #region Menu

    private void OnGetVerbs(Entity<AttachmentHolderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("attachment-menu-verb"),
            Priority = 5,
            Act = () => OpenMenu(ent, user),
        });

        if (GetSlotItem(ent, ent.Comp.LightSlot) is { } light && HasComp<HandheldLightComponent>(light))
        {
            var on = ent.Comp.LightOn;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString(on ? "attachment-light-off" : "attachment-light-on"),
                Priority = 4,
                Act = () => SetLight(ent, !on, user),
            });
        }
    }

    private void OpenMenu(Entity<AttachmentHolderComponent> ent, EntityUid user)
    {
        var ui = EnsureComp<UserInterfaceComponent>(ent);
        if (!_ui.HasUi(ent, AttachmentUiKey.Key, ui))
            _ui.SetUi((ent.Owner, ui), AttachmentUiKey.Key, new InterfaceData("AttachmentHolderBoundUserInterface"));

        _ui.OpenUi((ent.Owner, ui), AttachmentUiKey.Key, user);
        UpdateMenu(ent, user);
    }

    private void OnUiOpened(Entity<AttachmentHolderComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateMenu(ent, args.Actor);
    }

    private void OnInsertMessage(Entity<AttachmentHolderComponent> ent, ref AttachmentInsertMessage args)
    {
        if (ent.Comp.Slots.Contains(args.SlotId) && _itemSlots.TryGetSlot(ent, args.SlotId, out var slot))
            _itemSlots.TryInsertFromHand(ent, slot, args.Actor);
        UpdateMenu(ent, args.Actor);
    }

    private void OnEjectMessage(Entity<AttachmentHolderComponent> ent, ref AttachmentEjectMessage args)
    {
        if (ent.Comp.Slots.Contains(args.SlotId) && _itemSlots.TryGetSlot(ent, args.SlotId, out var slot))
            _itemSlots.TryEjectToHands(ent, slot, args.Actor);
        UpdateMenu(ent, args.Actor);
    }

    /// <summary>Slots, what sits in them and whether the item in the user's hand fits.</summary>
    private void UpdateMenu(Entity<AttachmentHolderComponent> ent, EntityUid user)
    {
        if (!_ui.HasUi(ent, AttachmentUiKey.Key))
            return;

        var held = _hands.GetActiveItem(user);
        var slots = new List<AttachmentSlotState>();
        foreach (var slotId in ent.Comp.Slots)
        {
            if (!_itemSlots.TryGetSlot(ent, slotId, out var slot))
                continue;

            var name = string.IsNullOrEmpty(slot.Name) ? slotId : Loc.GetString(slot.Name);
            var canInsert = slot.Item == null && held is { } item && _itemSlots.CanInsert(ent, item, user, slot);
            slots.Add(new AttachmentSlotState(slotId, name,
                slot.Item is { } inside ? Name(inside) : null,
                slot.Item is { } net ? GetNetEntity(net) : null,
                canInsert));
        }

        _ui.SetUiState(ent.Owner, AttachmentUiKey.Key, new AttachmentHolderBuiState(slots));
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateWeaponLights(frameTime);

        _expiredShields.Clear();
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<AttachmentDamageShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (now >= shield.EndTime)
                _expiredShields.Add(uid);
        }

        foreach (var uid in _expiredShields)
        {
            RemComp<AttachmentDamageShieldComponent>(uid);
        }
    }

    #region Components

    protected override void OnAttached(Entity<AttachmentHolderComponent> holder, Entity<AttachmentComponent> attachment)
    {
        if (holder.Comp.Applied.ContainsKey(attachment))
            return;

        ApplyComponents(holder, attachment, attachment.Comp.AddComponents, attachment.Comp.RemoveComponents);
    }

    protected override void OnSlotFilled(Entity<AttachmentHolderComponent> holder, string slot, EntityUid item)
    {
        if (holder.Comp.SlotComponents.TryGetValue(slot, out var components) && !holder.Comp.Applied.ContainsKey(item))
            ApplyComponents(holder, item, components, new ComponentRegistry());

        if (slot == holder.Comp.BayonetSlot)
            CopyBayonetStats(holder, item);
    }

    /// <summary>The weapon stabs exactly like the fixed knife: its damage, attack rate, reach and sounds.</summary>
    private void CopyBayonetStats(Entity<AttachmentHolderComponent> holder, EntityUid knifeUid)
    {
        if (!TryComp<MeleeWeaponComponent>(knifeUid, out var knife) || !TryComp<MeleeWeaponComponent>(holder, out var melee))
            return;

        ClearStats(holder);
        melee.Damage = new DamageSpecifier(knife.Damage);
        melee.AttackRate = knife.AttackRate;
        melee.Range = knife.Range;
        melee.Angle = knife.Angle;
        melee.ResistanceBypass = knife.ResistanceBypass;
        melee.HeavyAttackResistanceBypass = knife.HeavyAttackResistanceBypass;
        melee.ClickDamageModifier = knife.ClickDamageModifier;
        melee.BluntStaminaDamageFactor = knife.BluntStaminaDamageFactor;
        melee.SwingSound = knife.SwingSound;
        melee.HitSound = knife.HitSound;
        melee.NoDamageSound = knife.NoDamageSound;
        // Right click swings wide over an area like the knife does, with the demons' strike animation.
        melee.Animation = holder.Comp.BayonetAnimation ?? knife.Animation;
        melee.WideAnimation = holder.Comp.BayonetAnimation ?? knife.WideAnimation;
        melee.WideAnimationRotation = knife.WideAnimationRotation;
        Dirty(holder, melee);
        RefreshStats(holder);
    }

    protected override void OnSlotEmptied(Entity<AttachmentHolderComponent> holder, string slot, EntityUid item)
    {
        if (holder.Comp.SlotComponents.ContainsKey(slot))
            RevertComponents(holder, item);

        if (slot == holder.Comp.LightSlot)
            SetLight(holder, false);
    }

    /// <summary>Adds/replaces/removes components on the weapon, remembering what to put back. Keyed by the item.</summary>
    private void ApplyComponents(
        Entity<AttachmentHolderComponent> holder,
        EntityUid key,
        ComponentRegistry add,
        ComponentRegistry remove)
    {
        var applied = new AppliedAttachment();
        holder.Comp.Applied[key] = applied;
        ClearStats(holder);

        foreach (var name in remove.Keys)
        {
            var type = Factory.GetRegistration(name).Type;
            if (!EntityManager.TryGetComponent(holder, type, out var existing))
                continue;

            applied.Saved.Add(_serialization.CreateCopy(existing, notNullableOverride: true));
            RemComp(holder, existing);
        }

        foreach (var (name, entry) in add)
        {
            var type = Factory.GetRegistration(name).Type;
            if (EntityManager.TryGetComponent(holder, type, out var existing))
                applied.Saved.Add(_serialization.CreateCopy(existing, notNullableOverride: true));
            else
                applied.Added.Add(name);

            var component = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            AddComp(holder, component, true);
        }

        RefreshStats(holder);
    }

    protected override void OnDetached(Entity<AttachmentHolderComponent> holder, Entity<AttachmentComponent> attachment)
    {
        RevertComponents(holder, attachment);
    }

    private void RevertComponents(Entity<AttachmentHolderComponent> holder, EntityUid key)
    {
        if (!holder.Comp.Applied.Remove(key, out var applied))
            return;

        if (!TerminatingOrDeleted(holder))
        {
            ClearStats(holder);
            foreach (var name in applied.Added)
            {
                var type = Factory.GetRegistration(name).Type;
                if (EntityManager.TryGetComponent(holder, type, out var added))
                    RemComp(holder, added);
            }

            foreach (var saved in applied.Saved)
            {
                var component = (Component) _serialization.CreateCopy(saved, notNullableOverride: true);
                AddComp(holder, component, true);
            }
        }

        RefreshStats(holder);
    }

    /// <summary>Puts range and recharge back to what they were before attachments, so components can be swapped safely.</summary>
    private void ClearStats(Entity<AttachmentHolderComponent> holder)
    {
        if (holder.Comp.BaseRange is { } range && TryComp<MeleeWeaponComponent>(holder, out var melee))
        {
            melee.Range = range;
            Dirty(holder, melee);
        }

        if (holder.Comp.BaseRechargeCooldown is { } cooldown && TryComp<RechargeBasicEntityAmmoComponent>(holder, out var recharge))
        {
            recharge.RechargeCooldown = cooldown;
            Dirty(holder, recharge);
        }

        holder.Comp.BaseRange = null;
        holder.Comp.BaseRechargeCooldown = null;
    }

    /// <summary>Applies stats that live directly on components: melee range and ammo recharge.</summary>
    private void RefreshStats(Entity<AttachmentHolderComponent> holder)
    {
        if (TerminatingOrDeleted(holder))
            return;

        var rangeMultiplier = 1f;
        var rangeBonus = 0f;
        var rechargeBonus = 0f;
        foreach (var attachment in GetAttachments(holder))
        {
            rangeMultiplier *= attachment.Comp.RangeMultiplier;
            rangeBonus += attachment.Comp.BonusRange;
            rechargeBonus += attachment.Comp.RechargeCooldownBonus;
        }

        if (TryComp<MeleeWeaponComponent>(holder, out var melee))
        {
            holder.Comp.BaseRange = melee.Range;
            melee.Range = Math.Max(0.5f, melee.Range * rangeMultiplier + rangeBonus);
            Dirty(holder, melee);
        }

        if (TryComp<RechargeBasicEntityAmmoComponent>(holder, out var recharge))
        {
            holder.Comp.BaseRechargeCooldown = recharge.RechargeCooldown;
            recharge.RechargeCooldown = Math.Max(0.1f, recharge.RechargeCooldown + rechargeBonus);
            Dirty(holder, recharge);
        }
    }

    #endregion

    #region Flashlight

    private void OnToggleLightAction(Entity<AttachmentHolderComponent> ent, ref AttachmentToggleLightActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (GetSlotItem(ent, ent.Comp.LightSlot) is not { } light || !HasComp<HandheldLightComponent>(light))
        {
            _popup.PopupEntity(Loc.GetString("attachment-light-missing"), args.Performer, args.Performer);
            return;
        }

        SetLight(ent, !ent.Comp.LightOn, args.Performer);
    }

    /// <summary>
    /// The flashlight is inside the weapon (a container hides its light), so the weapon shines with the flashlight's
    /// light. It still works as that flashlight: its own battery runs it at its wattage, and it does not turn on
    /// or goes out when the battery is empty.
    /// </summary>
    public void SetLight(Entity<AttachmentHolderComponent> holder, bool on, EntityUid? user = null)
    {
        if (TerminatingOrDeleted(holder))
            return;

        var item = GetSlotItem(holder, holder.Comp.LightSlot);
        TryComp<HandheldLightComponent>(item, out var flashlight);

        if (on && item != null && flashlight != null && _light.TryGetLight(item.Value, out var source))
        {
            if (!HasFlashlightCharge(item.Value, flashlight))
            {
                _audio.PlayPvs(flashlight.TurnOnFailSound, holder);
                if (user != null)
                    _popup.PopupEntity(Loc.GetString("attachment-light-no-power"), holder, user.Value);
                return;
            }

            var light = _light.EnsureLight(holder);
            _light.SetRadius(holder, source.Radius, light);
            _light.SetEnergy(holder, source.Energy, light);
            _light.SetColor(holder, source.Color, light);
            _light.SetSoftness(holder, source.Softness, light);
            _light.SetFalloff(holder, source.Falloff, light);
            _light.SetCurveFactor(holder, source.CurveFactor, light);
            _light.SetEnabled(holder, true, light);
            if (!holder.Comp.LightOn)
                _audio.PlayPvs(flashlight.TurnOnSound, holder);
            holder.Comp.LightOn = true;
            Dirty(holder);
            return;
        }

        if (holder.Comp.LightOn && flashlight != null)
            _audio.PlayPvs(flashlight.TurnOffSound, holder);

        holder.Comp.LightOn = false;
        Dirty(holder);
        if (_light.TryGetLight(holder, out var existing))
            _light.SetEnabled(holder, false, existing);
    }

    private bool HasFlashlightCharge(EntityUid flashlight, HandheldLightComponent handheld)
    {
        if (!_powerCell.TryGetBatteryFromSlotOrEntity(flashlight, out var battery))
            return false;

        return _battery.GetCharge((battery.Value.Owner, battery.Value.Comp)) > 0f;
    }

    /// <summary>Lit weapon flashlights drain their own battery.</summary>
    private void UpdateWeaponLights(float frameTime)
    {
        var query = EntityQueryEnumerator<AttachmentHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (!holder.LightOn)
                continue;

            if (GetSlotItem(uid, holder.LightSlot) is not { } item ||
                !TryComp<HandheldLightComponent>(item, out var flashlight) ||
                !_powerCell.TryGetBatteryFromSlotOrEntity(item, out var battery))
            {
                SetLight((uid, holder), false);
                continue;
            }

            var batteryEnt = (battery.Value.Owner, battery.Value.Comp);
            _battery.UseCharge(batteryEnt, flashlight.Wattage * frameTime);
            if (_battery.GetCharge(batteryEnt) <= 0f)
                SetLight((uid, holder), false);
        }
    }

    #endregion

    #region Effects

    private void OnMeleeHit(Entity<AttachmentHolderComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var attachment in GetAttachments(ent))
        {
            foreach (var target in args.HitEntities)
            {
                if (attachment.Comp.TargetHitEffects.Count > 0)
                    _effects.ApplyEffects(target, attachment.Comp.TargetHitEffects.ToArray(), user: args.User);

                RunEffects(attachment, attachment.Comp.HitEffects, args.User, target, ent, args.BonusDamage);
            }
        }
    }

    private void OnMarkDetonated(Entity<AttachmentHolderComponent> ent, ref DamageMarkerDetonatedEvent args)
    {
        foreach (var attachment in GetAttachments(ent))
        {
            RunEffects(attachment, attachment.Comp.MarkDetonationEffects, args.User, args.Target, ent, args.BonusDamage);
        }
    }

    private void RunEffects(
        Entity<AttachmentComponent> attachment,
        List<AttachmentEffect> effects,
        EntityUid user,
        EntityUid target,
        EntityUid weapon,
        DamageSpecifier bonusDamage)
    {
        if (effects.Count == 0)
            return;

        var effectArgs = new AttachmentEffectArgs(user, target, weapon, attachment, EntityManager);
        foreach (var effect in effects)
        {
            if (effect.Chance < 1f && !_random.Prob(effect.Chance))
                continue;

            effect.Apply(effectArgs, bonusDamage);
        }
    }

    #endregion
}
