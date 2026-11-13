using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.Prototypes;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCCombatSystem
{
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;

    private EntityQuery<CombatModeComponent> _combatQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;
    private EntityQuery<RechargeBasicEntityAmmoComponent> _rechargeQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private const float FallbackProjectileLeadSpeed = 20f;

    /// <summary>
    /// Cooldown on raycasting to check LOS.
    /// </summary>
    public const float UnoccludedCooldown = 0.2f;

    private void InitializeRanged()
    {
        _combatQuery = GetEntityQuery<CombatModeComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _rechargeQuery = GetEntityQuery<RechargeBasicEntityAmmoComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentStartup>(OnRangedStartup);
        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentShutdown>(OnRangedShutdown);
    }

    private void OnRangedStartup(EntityUid uid, NPCRangedCombatComponent component, ComponentStartup args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            _combat.SetInCombatMode(uid, true, combat);
        }
        else
        {
            component.Status = CombatStatus.Unspecified;
        }
    }

    private void OnRangedShutdown(EntityUid uid, NPCRangedCombatComponent component, ComponentShutdown args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            _combat.SetInCombatMode(uid, false, combat);
        }
    }

    private void UpdateRanged(float frameTime)
    {
        var query = EntityQueryEnumerator<NPCRangedCombatComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Status == CombatStatus.Unspecified)
                continue;

            if (_steeringQuery.TryGetComponent(uid, out var steering) && steering.Status == SteeringStatus.NoPath)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (!_xformQuery.TryGetComponent(comp.Target, out var targetXform) ||
                !_physicsQuery.TryGetComponent(comp.Target, out var targetBody))
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (targetXform.MapID != xform.MapID)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (_combatQuery.TryGetComponent(uid, out var combatMode))
            {
                _combat.SetInCombatMode(uid, true, combatMode);
            }

            if (!_gun.TryGetGun(uid, out var gun))
            {
                comp.Status = CombatStatus.NoWeapon;
                comp.ShootAccumulator = 0f;
                continue;
            }

            var ammoEv = new GetAmmoCountEvent();
            RaiseLocalEvent(gun, ref ammoEv);

            if (ammoEv.Count == 0)
            {
                // Recharging then?
                if (_rechargeQuery.HasComponent(gun))
                {
                    continue;
                }

                comp.Status = CombatStatus.Unspecified;
                comp.ShootAccumulator = 0f;
                continue;
            }

            comp.LOSAccumulator -= frameTime;

            var worldPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(targetXform);

            // We'll work out the projected spot of the target and shoot there instead of where they are.
            var distance = (targetPos - worldPos).Length();
            var oldInLos = comp.TargetInLOS;

            // TODO: Should be doing these raycasts in parallel
            // Ideally we'd have 2 steps, 1. to go over the normal details for shooting and then 2. to handle beep / rotate / shoot
            if (comp.LOSAccumulator < 0f)
            {
                comp.LOSAccumulator += UnoccludedCooldown;

                // For consistency with NPC steering.
                var collisionGroup = comp.UseOpaqueForLOSChecks ? CollisionGroup.Opaque : (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);
                comp.TargetInLOS = _interaction.InRangeUnobstructed(uid, comp.Target, distance + 0.1f, collisionGroup);
            }

            if (!comp.TargetInLOS)
            {
                comp.ShootAccumulator = 0f;
                comp.Status = CombatStatus.NotInSight;

                if (TryComp(uid, out steering))
                {
                    steering.ForceMove = true;
                }

                continue;
            }

            if (!oldInLos && comp.SoundTargetInLOS != null)
            {
                _audio.PlayPvs(comp.SoundTargetInLOS, uid);
            }

            comp.ShootAccumulator += frameTime;

            if (comp.ShootAccumulator < comp.ShootDelay)
            {
                continue;
            }

            var targetSpot = GetRangedTargetSpot(gun, targetPos, targetBody.LinearVelocity, distance);

            // If we have a max rotation speed then do that.
            var goalRotation = (targetSpot - worldPos).ToWorldAngle();
            var rotationSpeed = comp.RotationSpeed;

            if (!_rotate.TryRotateTo(uid, goalRotation, frameTime, comp.AccuracyThreshold, rotationSpeed?.Theta ?? double.MaxValue, xform))
            {
                continue;
            }

            // TODO: LOS
            // TODO: Ammo checks
            // TODO: Burst fire
            // TODO: Cycling
            // Max rotation speed

            // TODO: Check if we can face

            if (!Enabled || !_gun.CanShoot(gun))
                continue;

            EntityCoordinates targetCordinates;

            if (_map.TryFindGridAt(xform.MapID, targetSpot, out var gridUid, out var mapGrid))
            {
                targetCordinates = new EntityCoordinates(gridUid, _map.WorldToLocal(gridUid, mapGrid, targetSpot));
            }
            else
            {
                targetCordinates = new EntityCoordinates(xform.MapUid!.Value, targetSpot);
            }

            comp.Status = CombatStatus.Normal;

            if (gun.Comp.NextFire > _timing.CurTime)
            {
                return;
            }

            _gun.AttemptShoot(uid, gun, targetCordinates, comp.Target);
        }
    }

    private Vector2 GetRangedTargetSpot(Entity<GunComponent> gun, Vector2 targetPos, Vector2 targetVelocity, float distance)
    {
        if (UsesHitscanAmmo(gun))
            return targetPos;

        var projectileSpeed = gun.Comp.ProjectileSpeedModified > 0f
            ? gun.Comp.ProjectileSpeedModified
            : FallbackProjectileLeadSpeed;

        return targetPos + targetVelocity * distance / projectileSpeed;
    }

    private bool UsesHitscanAmmo(Entity<GunComponent> gun)
    {
        if (TryComp<BatteryAmmoProviderComponent>(gun.Owner, out var battery))
            return IsHitscanAmmoPrototype(battery.Prototype);

        if (TryComp<BasicEntityAmmoProviderComponent>(gun.Owner, out var basic))
            return IsHitscanAmmoPrototype(basic.Proto);

        if (TryComp<BallisticAmmoProviderComponent>(gun.Owner, out var ballistic))
            return BallisticProviderUsesHitscanAmmo(ballistic);

        return false;
    }

    private bool BallisticProviderUsesHitscanAmmo(BallisticAmmoProviderComponent ballistic)
    {
        if (ballistic.Entities.Count > 0)
            return IsHitscanAmmoEntity(ballistic.Entities[^1]);

        return ballistic.UnspawnedCount > 0 && IsHitscanAmmoPrototype(ballistic.Proto);
    }

    private bool IsHitscanAmmoEntity(EntityUid ammo)
    {
        if (HasComp<HitscanAmmoComponent>(ammo))
            return true;

        return TryComp<CartridgeAmmoComponent>(ammo, out var cartridge) &&
               IsHitscanAmmoPrototype(cartridge.Prototype);
    }

    private bool IsHitscanAmmoPrototype(string? protoId, int depth = 0)
    {
        if (depth > 8 ||
            string.IsNullOrEmpty(protoId) ||
            !_prototype.TryIndex<EntityPrototype>(protoId, out var prototype))
        {
            return false;
        }

        if (prototype.HasComponent<HitscanAmmoComponent>(_componentFactory))
            return true;

        return prototype.TryGetComponent<CartridgeAmmoComponent>(out var cartridge, _componentFactory) &&
               IsHitscanAmmoPrototype(cartridge.Prototype, depth + 1);
    }
}
