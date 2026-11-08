using Content.Shared.Actions;
using Content.Shared.DeadSpace.Banner;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Banner;

public sealed class BannerSystem : SharedBannerSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BannerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<BannerComponent, BannerOffenseActionEvent>(OnOffenseAction);
        SubscribeLocalEvent<BannerComponent, BannerDefenseActionEvent>(OnDefenseAction);
        SubscribeLocalEvent<BannerComponent, BannerConcentrateFireActionEvent>(OnConcentrateFireAction);
        SubscribeLocalEvent<BannerConcentratedFireComponent, DidEquipHandEvent>(OnConcentratedHandEquipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;

        var defenseQuery = EntityQueryEnumerator<BannerDefenseBuffComponent>();
        while (defenseQuery.MoveNext(out var uid, out var buff))
        {
            if (time >= buff.EndTime)
                RemComp<BannerDefenseBuffComponent>(uid);
        }

        var fireQuery = EntityQueryEnumerator<BannerConcentratedFireComponent>();
        while (fireQuery.MoveNext(out var uid, out var fire))
        {
            if (time < fire.EndTime)
                continue;

            foreach (var gun in fire.BoostedGuns)
            {
                UnboostGun(gun);
            }

            RemComp<BannerConcentratedFireComponent>(uid);
        }
    }

    private void OnGetActions(Entity<BannerComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;

        args.AddAction(ref ent.Comp.OffenseAction, ent.Comp.OffenseActionId);
        args.AddAction(ref ent.Comp.DefenseAction, ent.Comp.DefenseActionId);
        args.AddAction(ref ent.Comp.ConcentrateFireAction, ent.Comp.ConcentrateFireActionId);
        Dirty(ent);
    }

    private void OnOffenseAction(Entity<BannerComponent> ent, ref BannerOffenseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        foreach (var target in EnumerateAffected(args.Performer, ent.Comp.Range))
        {
            _movementMod.TryUpdateMovementSpeedModDuration(target, ent.Comp.OffenseStatusEffect,
                ent.Comp.OffenseDuration, ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
        }

        SetSiblingsCooldown(ent.Comp.OffenseAction, ent.Comp.DefenseAction, ent.Comp.ConcentrateFireAction,
            ent.Comp.OffenseDuration);
    }

    private void OnDefenseAction(Entity<BannerComponent> ent, ref BannerDefenseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var end = _timing.CurTime + ent.Comp.DefenseDuration;
        foreach (var target in EnumerateAffected(args.Performer, ent.Comp.Range))
        {
            var buff = EnsureComp<BannerDefenseBuffComponent>(target);
            buff.Coefficient = ent.Comp.DamageCoefficient;
            if (buff.EndTime < end)
                buff.EndTime = end;
            Dirty(target, buff);
        }

        SetSiblingsCooldown(ent.Comp.OffenseAction, ent.Comp.DefenseAction, ent.Comp.ConcentrateFireAction,
            ent.Comp.DefenseDuration);
    }

    private void OnConcentrateFireAction(Entity<BannerComponent> ent, ref BannerConcentrateFireActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var end = _timing.CurTime + ent.Comp.ConcentrateFireDuration;
        foreach (var target in EnumerateAffected(args.Performer, ent.Comp.Range))
        {
            var fire = EnsureComp<BannerConcentratedFireComponent>(target);
            if (fire.EndTime < end)
                fire.EndTime = end;
            fire.FireRateBonus = ent.Comp.FireRateBonus;

            foreach (var held in _hands.EnumerateHeld(target))
            {
                if (!HasComp<GunComponent>(held))
                    continue;

                BoostGun(held, ent.Comp.FireRateBonus, end);
                if (!fire.BoostedGuns.Contains(held))
                    fire.BoostedGuns.Add(held);
            }

            Dirty(target, fire);
        }

        SetSiblingsCooldown(ent.Comp.OffenseAction, ent.Comp.DefenseAction, ent.Comp.ConcentrateFireAction,
            ent.Comp.ConcentrateFireDuration);
    }

    private void OnConcentratedHandEquipped(
        Entity<BannerConcentratedFireComponent> ent,
        ref DidEquipHandEvent args
    )
    {
        if (_timing.CurTime >= ent.Comp.EndTime)
            return;

        if (!HasComp<GunComponent>(args.Equipped))
            return;

        BoostGun(args.Equipped, ent.Comp.FireRateBonus, ent.Comp.EndTime);
        if (!ent.Comp.BoostedGuns.Contains(args.Equipped))
            ent.Comp.BoostedGuns.Add(args.Equipped);
    }

    private void BoostGun(EntityUid gun, float fireRateBonus, TimeSpan end)
    {
        var boost = EnsureComp<BannerGunBoostComponent>(gun);
        boost.FireRateBonus = fireRateBonus;
        if (boost.EndTime < end)
            boost.EndTime = end;
        Dirty(gun, boost);

        _gun.RefreshModifiers(gun);
    }

    private void UnboostGun(EntityUid gun)
    {
        if (!RemComp<BannerGunBoostComponent>(gun))
            return;

        _gun.RefreshModifiers(gun);
    }

    private void SetSiblingsCooldown(EntityUid? used, EntityUid? first, EntityUid? second, TimeSpan cooldown)
    {
        if (first != null && first != used)
            _actions.SetCooldown(first.Value, cooldown);

        if (second != null && second != used)
            _actions.SetCooldown(second.Value, cooldown);
    }

    private IEnumerable<EntityUid> EnumerateAffected(EntityUid performer, float range)
    {
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(performer).Coordinates, range))
        {
            if (_mobState.IsDead(ent))
                continue;

            yield return ent.Owner;
        }
    }
}
