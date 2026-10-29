// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.Physics.Components;
using Content.Shared.DeadSpace.Implants;
using Content.Shared.DeadSpace.Weapons.Smart;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Weapons.Smart;

public sealed class SmartWeaponServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SmartWeaponComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeNetworkEvent<SmartWeaponTargetChangedEvent>(OnTargetChanged);
    }

    private void OnTargetChanged(SmartWeaponTargetChangedEvent ev, EntitySessionEventArgs args)
    {
        // Only the one holding the gun aims it.
        if (args.SenderSession.AttachedEntity is not { Valid: true } user ||
            !TryGetEntity(ev.Gun, out var gunUid) ||
            !TryComp(gunUid, out SmartWeaponComponent? smartWeapon) ||
            !_hands.IsHolding(user, gunUid.Value))
        {
            return;
        }

        EntityUid? target = null;
        if (ev.Target is { } netTarget)
        {
            // As the client picks it: a living thing, in sight.
            if (!TryGetEntity(netTarget, out var targetUid) ||
                TerminatingOrDeleted(targetUid.Value) ||
                !HasComp<MobStateComponent>(targetUid.Value) ||
                !_examine.InRangeUnOccluded(user, targetUid.Value, ExamineSystemShared.ExamineRange))
            {
                return;
            }

            target = targetUid;
        }

        smartWeapon.Target = target;
        Dirty(gunUid.Value, smartWeapon);
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