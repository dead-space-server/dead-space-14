// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Marker;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The kinetic machete's marker shot (<see cref="LavalandChargedMarkerComponent"/>): the client says when the right
/// button goes down and up, the server times the charge, fires the mark or blows the machete up.
/// </summary>
public sealed class LavalandChargedMarkerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedDamageMarkerSystem _marker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandChargedMarkerStartEvent>(OnStart);
        SubscribeNetworkEvent<LavalandChargedMarkerReleaseEvent>(OnRelease);
    }

    private bool TryGetHeld(EntitySessionEventArgs args, NetEntity netWeapon, out EntityUid user,
        out Entity<LavalandChargedMarkerComponent> weapon)
    {
        user = default;
        weapon = default;
        if (args.SenderSession.AttachedEntity is not { } attached ||
            !TryGetEntity(netWeapon, out var uid) ||
            !TryComp<LavalandChargedMarkerComponent>(uid, out var comp) ||
            _hands.GetActiveItem(attached) != uid ||
            !_combat.IsInCombatMode(attached))
        {
            return false;
        }

        user = attached;
        weapon = (uid.Value, comp);
        return true;
    }

    private void OnStart(LavalandChargedMarkerStartEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetHeld(args, ev.Weapon, out var user, out var weapon) || weapon.Comp.Charging)
            return;

        var now = _timing.CurTime;
        if (now < weapon.Comp.NextShot)
        {
            var left = (int) Math.Ceiling((weapon.Comp.NextShot - now).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("lavaland-machete-recharging", ("seconds", left)), user, user);
            return;
        }

        weapon.Comp.Charging = true;
        weapon.Comp.ChargeStart = now;
        Dirty(weapon);
    }

    private void OnRelease(LavalandChargedMarkerReleaseEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetHeld(args, ev.Weapon, out var user, out var weapon) || !weapon.Comp.Charging)
            return;

        var held = _timing.CurTime - weapon.Comp.ChargeStart;
        weapon.Comp.Charging = false;
        Dirty(weapon);

        if (held >= weapon.Comp.OverchargeTime)
        {
            Overcharge(user, weapon);
            return;
        }

        Fire(user, weapon, GetCoordinates(ev.Target), weapon.Comp.MultiplierFor(held));
    }

    private void Fire(EntityUid user, Entity<LavalandChargedMarkerComponent> weapon, EntityCoordinates target, float multiplier)
    {
        weapon.Comp.NextShot = _timing.CurTime + weapon.Comp.Cooldown;
        Dirty(weapon);

        var from = _transform.GetMapCoordinates(user);
        var to = _transform.ToMapCoordinates(target);
        var direction = to.MapId == from.MapId ? to.Position - from.Position : Vector2.Zero;
        if (direction.LengthSquared() < 0.0001f)
            direction = _transform.GetWorldRotation(user).ToWorldVec();

        var projectile = Spawn(weapon.Comp.Projectile, from);
        // The mark is the machete's: a hit with the machete detonates it, for the charged damage.
        _marker.SetMarkDamage(projectile, weapon.Comp.MarkDamage * multiplier);
        _gun.ShootProjectile(projectile, Vector2.Normalize(direction), Vector2.Zero, weapon, user, weapon.Comp.ProjectileSpeed);
        if (weapon.Comp.ShotSound != null)
            _audio.PlayPvs(weapon.Comp.ShotSound, user);
    }

    /// <summary>Held too long: the machete blows up in the hand.</summary>
    private void Overcharge(EntityUid user, Entity<LavalandChargedMarkerComponent> weapon)
    {
        weapon.Comp.Charging = false;
        weapon.Comp.NextShot = _timing.CurTime + weapon.Comp.OverchargeCooldown;
        Dirty(weapon);

        _damage.TryChangeDamage(user, weapon.Comp.OverchargeDamage, origin: weapon);
        if (weapon.Comp.OverchargeSound != null)
            _audio.PlayPvs(weapon.Comp.OverchargeSound, user);
        _popup.PopupEntity(Loc.GetString("lavaland-machete-overcharge", ("weapon", weapon.Owner)), user, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Still holding past the limit: it blows up without waiting for the button.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandChargedMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Charging || now - comp.ChargeStart < comp.OverchargeTime)
                continue;

            if (xform.ParentUid is { Valid: true } holder && _hands.IsHolding(holder, uid))
            {
                Overcharge(holder, (uid, comp));
                continue;
            }

            comp.Charging = false;
            Dirty(uid, comp);
        }
    }
}
