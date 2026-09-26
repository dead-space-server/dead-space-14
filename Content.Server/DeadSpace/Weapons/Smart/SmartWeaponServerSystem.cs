// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.Physics.Components;
using Content.Shared.DeadSpace.Implants;
using Content.Shared.DeadSpace.Weapons.Smart;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Weapons.Smart;

public sealed class SmartWeaponServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SmartWeaponComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeNetworkEvent<SmartWeaponTargetChangedEvent>(OnTargetChanged);
    }

    private void OnTargetChanged(SmartWeaponTargetChangedEvent ev, EntitySessionEventArgs args)
    {
        var gun = GetEntity(ev.Gun);
        if (!TryComp(gun, out SmartWeaponComponent? smartWeapon))
            return;

        smartWeapon.Target = ev.Target != null ? GetEntity(ev.Target.Value) : null;
        Dirty(gun, smartWeapon);
    }

    private void OnAmmoShot(EntityUid uid, SmartWeaponComponent component, ref AmmoShotEvent args)
    {
        var owner = Transform(uid).ParentUid;
        if (owner == EntityUid.Invalid || !HasComp<SmartLinkImplantComponent>(owner))
            return;

        if (component.Target is not { } target || TerminatingOrDeleted(target))
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            var chasing = EnsureComp<ChasingWalkComponent>(projectile);
            chasing.ChasingEntity = target;
            chasing.ImpulseInterval = component.MagnetismUpdateInterval;
            chasing.MaxAngleVectorChangePerImpulse = component.MagnetismMaxAngle;
            chasing.NextChangeVectorTime = TimeSpan.MaxValue;
            chasing.NextImpulseTime = _gameTiming.CurTime + TimeSpan.FromSeconds(component.MagnetismDelay);
            chasing.StopAtTarget = false;
            chasing.RotateWithImpulse = component.MagnetismRotateWithImpulse;
        }
    }
}