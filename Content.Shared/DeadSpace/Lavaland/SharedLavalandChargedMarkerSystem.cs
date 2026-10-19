// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Marker;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The kinetic machete's marker shot (<see cref="LavalandChargedMarkerComponent"/>): the client says when the right
/// button goes down and up as predicted events, so the charge ring and the shot sound follow the button at once; the
/// server alone spawns the mark bolt and deals the overcharge damage, like guns do.
/// </summary>
public abstract class SharedLavalandChargedMarkerSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedDamageMarkerSystem _marker = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<LavalandChargedMarkerStartEvent>(OnStart);
        SubscribeAllEvent<LavalandChargedMarkerReleaseEvent>(OnRelease);
    }

    private bool TryGetHeld(EntitySessionEventArgs args, NetEntity netWeapon, out EntityUid user,
        out Entity<LavalandChargedMarkerComponent> weapon)
    {
        user = default;
        weapon = default;
        if (args.SenderSession.AttachedEntity is not { } attached ||
            !TryGetEntity(netWeapon, out var uid) ||
            !TryComp<LavalandChargedMarkerComponent>(uid, out var comp) ||
            Hands.GetActiveItem(attached) != uid ||
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

        var now = Timing.CurTime;
        if (now < weapon.Comp.NextShot)
        {
            var left = (int) Math.Ceiling((weapon.Comp.NextShot - now).TotalSeconds);
            _popup.PopupClient(Loc.GetString("lavaland-machete-recharging", ("seconds", left)), user, user);
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

        var held = Timing.CurTime - weapon.Comp.ChargeStart;
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
        weapon.Comp.NextShot = Timing.CurTime + weapon.Comp.Cooldown;
        Dirty(weapon);
        _audio.PlayPredicted(weapon.Comp.ShotSound, user, user);

        if (_net.IsClient)
            return;

        var from = _transform.GetMapCoordinates(user);
        var to = _transform.ToMapCoordinates(target);
        var direction = to.MapId == from.MapId ? to.Position - from.Position : Vector2.Zero;
        if (direction.LengthSquared() < 0.0001f)
            direction = _transform.GetWorldRotation(user).ToWorldVec();

        var projectile = Spawn(weapon.Comp.Projectile, from);
        // The mark is the machete's: a hit with the machete detonates it, for the charged damage.
        _marker.SetMarkDamage(projectile, weapon.Comp.MarkDamage * multiplier);
        _gun.ShootProjectile(projectile, Vector2.Normalize(direction), Vector2.Zero, weapon, user, weapon.Comp.ProjectileSpeed);
    }

    /// <summary>Held too long: the machete blows up in the hand.</summary>
    protected void Overcharge(EntityUid user, Entity<LavalandChargedMarkerComponent> weapon)
    {
        weapon.Comp.Charging = false;
        weapon.Comp.NextShot = Timing.CurTime + weapon.Comp.OverchargeCooldown;
        Dirty(weapon);

        _audio.PlayPredicted(weapon.Comp.OverchargeSound, user, user);
        _popup.PopupPredicted(Loc.GetString("lavaland-machete-overcharge", ("weapon", weapon.Owner)), user, user, PopupType.MediumCaution);

        if (_net.IsServer)
            _damage.TryChangeDamage(user, weapon.Comp.OverchargeDamage, origin: weapon);
    }
}
