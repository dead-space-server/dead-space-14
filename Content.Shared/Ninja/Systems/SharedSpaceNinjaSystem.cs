using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Provides shared ninja API, handles being attacked revealing ninja and stops guns from shooting.
/// </summary>
public abstract class SharedSpaceNinjaSystem : EntitySystem
{
    [Dependency] protected readonly SharedNinjaSuitSystem Suit = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    //DS14-start
    [Dependency] protected readonly SharedBatterySystem Battery = default!;
    [Dependency] protected readonly PowerCellSystem PowerCell = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    //DS14-end

    public EntityQuery<SpaceNinjaComponent> NinjaQuery;

    public override void Initialize()
    {
        base.Initialize();

        NinjaQuery = GetEntityQuery<SpaceNinjaComponent>();

        SubscribeLocalEvent<SpaceNinjaComponent, AttackedEvent>(OnNinjaAttacked);
        SubscribeLocalEvent<SpaceNinjaComponent, MeleeAttackEvent>(OnNinjaAttack);
        SubscribeLocalEvent<SpaceNinjaComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    //DS14-start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!SimulateHeat)
            return;

        var query = EntityQueryEnumerator<SpaceNinjaComponent>();

        while (query.MoveNext(out var uid, out var ninja))
        {
            if (ninja.Suit is not { } suitUid)
                continue;

            if (!TryComp<NinjaSuitHeatComponent>(suitUid, out var heat))
                continue;

            var previousHeat = heat.Heat;

            if (TryComp<NinjaCloakComponent>(suitUid, out var cloak) && cloak.Enabled)
            {
                heat.Heat += heat.HeatRate * frameTime;

                if (heat.Heat >= heat.EffectsThreshold && previousHeat < heat.EffectsThreshold)
                {
                    Popup.PopupPredicted(Loc.GetString("ninja-suit-heat-warning"), uid, uid, PopupType.MediumCaution);
                }

                var dangerThreshold = heat.DangerThreshold;

                if (heat.Heat >= dangerThreshold &&
                    previousHeat < dangerThreshold)
                {
                    Popup.PopupPredicted(Loc.GetString("ninja-suit-heat-danger"), uid, uid, PopupType.MediumCaution);
                }

                if (heat.Heat >= heat.EffectsThreshold &&
                    heat.MaxHeat > heat.EffectsThreshold)
                {
                    var chance = (heat.Heat - heat.EffectsThreshold) / (heat.MaxHeat - heat.EffectsThreshold);

                    if (_random.Prob(chance * frameTime * 0.1f)) Spawn(heat.EffectPrototype, Transform(uid).Coordinates);
                }

                if (heat.Heat >= heat.MaxHeat)
                {
                    heat.Heat = heat.MaxHeat;

                    cloak.Enabled = false;
                    Dirty(suitUid, cloak);

                    Popup.PopupPredicted(Loc.GetString("ninja-suit-overheated"), uid, uid, PopupType.MediumCaution);
                }
            }
            else
            {
                heat.Heat = MathF.Max(0f, heat.Heat - heat.CoolRate * frameTime);
            }

            if (MathF.Abs(heat.Heat - heat.LastSentHeat) >= 1f)
            {
                heat.LastSentHeat = heat.Heat;
                Dirty(suitUid, heat);
            }
        }
    }

    protected virtual bool SimulateHeat => false;
    //DS14-end

    public bool IsNinja([NotNullWhen(true)] EntityUid? uid)
    {
        return NinjaQuery.HasComp(uid);
    }

    /// <summary>
    /// Set the ninja's worn suit entity
    /// </summary>
    public void AssignSuit(Entity<SpaceNinjaComponent> ent, EntityUid? suit)
    {
        if (ent.Comp.Suit == suit)
            return;

        ent.Comp.Suit = suit;
        Dirty(ent, ent.Comp);
    }

    /// <summary>
    /// Set the ninja's worn gloves entity
    /// </summary>
    public void AssignGloves(Entity<SpaceNinjaComponent> ent, EntityUid? gloves)
    {
        if (ent.Comp.Gloves == gloves)
            return;

        ent.Comp.Gloves = gloves;
        Dirty(ent, ent.Comp);
    }

    /// <summary>
    /// Bind a katana entity to a ninja, letting it be recalled and dash.
    /// Does nothing if the player is not a ninja or already has a katana bound.
    /// </summary>
    public void BindKatana(Entity<SpaceNinjaComponent?> ent, EntityUid katana)
    {
        if (!NinjaQuery.Resolve(ent, ref ent.Comp, false) || ent.Comp.Katana != null)
            return;

        ent.Comp.Katana = katana;
        Dirty(ent, ent.Comp);
    }

    //DS14-start
    public bool HasCharge(EntityUid user, float charge)
    {
        if (!TryComp<SpaceNinjaComponent>(user, out var ninja) || ninja.Suit == null)
            return false;

        if (!PowerCell.TryGetBatteryFromSlot(ninja.Suit.Value, out var battery))
            return false;

        return Battery.GetCharge(battery.Value.Owner) >= charge;
    }
    //DS14-end

    /// <summary>
    /// Gets the user's battery and tries to use some charge from it, returning true if successful.
    /// Serverside only.
    /// </summary>
    public virtual bool TryUseCharge(EntityUid user, float charge)
    {
        return false;
    }

    /// <summary>
    /// Handle revealing ninja if cloaked when attacked.
    /// </summary>
    //DS14-start
    private void OnNinjaAttacked(Entity<SpaceNinjaComponent> ent, ref AttackedEvent args)
    {
        RevealNinja(ent);
    }

    /// <summary>
    /// Handle revealing ninja if cloaked when attacking.
    /// Only reveals, there is no cooldown.
    /// </summary>
    private void OnNinjaAttack(Entity<SpaceNinjaComponent> ent, ref MeleeAttackEvent args)
    {
        RevealNinja(ent);
    }

    public bool RevealNinja(EntityUid user)
    {
        if (!NinjaQuery.TryComp(user, out var ninja) || ninja.Suit is not { } suitUid)
            return false;

        if (!TryComp<NinjaSuitComponent>(suitUid, out var suit))
            return false;

        Suit.RevealNinja((suitUid, suit), user);
        return true;
    }
    //DS14-end

    /// <summary>
    /// Require ninja to fight with HONOR, no guns!
    /// </summary>
    //DS14-start
    private void OnShotAttempted(Entity<SpaceNinjaComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!HasComp<NinjaAmmoProviderComponent>(args.Used))
        {
            Popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
            args.Cancel();
        }
    }
    //DS14-end
}