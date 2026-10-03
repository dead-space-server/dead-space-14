// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Actions;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class NinjaEmergencyTeleportSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turfSystem = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedNinjaSmokeAbilitySystem _smoke = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly Vector2 TeleportCheckExtents = new(0.475f, 0.475f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, NinjaEmergencyTeleportEvent>(OnTeleport);
        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }

    private void OnMapInit(Entity<NinjaEmergencyTeleportComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.TeleportActionEntity, comp.TeleportAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaEmergencyTeleportComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.TeleportActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaEmergencyTeleportComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.TeleportActionEntity);
        }
    }

    private void OnTeleport(Entity<NinjaEmergencyTeleportComponent> ent, ref NinjaEmergencyTeleportEvent args)
    {
        if (TryComp<PullableComponent>(args.Performer, out var pull) && _pulling.IsPulled(args.Performer, pull))
            _pulling.TryStopPull(args.Performer, pull);

        if (TryComp<PullerComponent>(args.Performer, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable);

        if (_net.IsClient)
            return;

        var targetCoords = SelectRandomTeleportPosition(args.Performer, ent.Comp.TeleportRadius);

        if (targetCoords == null)
            return;

        if (!_ninja.TryUseCharge(args.Performer, ent.Comp.EnergyCost))
        {
            _popup.PopupEntity(Loc.GetString("ninja-no-power"), args.Performer, args.Performer);
            return;
        }

        if (TryComp<NinjaSmokeAbilityComponent>(ent, out var smokeComp))
            _smoke.TrySpawnNinjaSmoke((ent.Owner, smokeComp), true);

        _transform.SetCoordinates(args.Performer, targetCoords.Value);
        _audio.PlayPvs(ent.Comp.TeleportSound, args.Performer);
        args.Handled = true;
    }

    private EntityCoordinates? SelectRandomTeleportPosition(EntityUid uid, Vector2 radius, int tries = 80, PhysicsComponent? physicsComponent = null)
    {
        var userXform = Transform(uid);
        var userCoords = userXform.Coordinates;

        if (!Resolve(uid, ref physicsComponent))
            return null;

        var forward = userXform.LocalRotation.ToWorldVec().Normalized();
        var side = new Vector2(-forward.Y, forward.X);
        var minDistance = MathF.Max(1f, radius.X);
        var maxDistance = MathF.Max(minDistance, radius.Y);
        var collisionMask = (CollisionGroup)physicsComponent.CollisionMask;

        return TryPickPosition(minDistance, maxDistance)
               ?? (minDistance > 1f ? TryPickPosition(1f, minDistance) : null);

        EntityCoordinates? TryPickPosition(float min, float max)
        {
            for (var i = 0; i < tries; i++)
            {
                var distance = (max - min) * MathF.Sqrt(_random.NextFloat()) + min;

                var lateralOffset = _random.NextFloat(-distance / 2f, distance / 2f);
                var candidateCoords = userCoords.Offset(forward * distance + side * lateralOffset);

                if (IsTeleportPositionBlocked(candidateCoords, collisionMask))
                    continue;

                if (_turfSystem.TryGetTileRef(candidateCoords, out var tileRef) && !tileRef.Value.Tile.IsEmpty)
                    return _turfSystem.GetTileCenter(tileRef.Value);

                return candidateCoords;
            }

            return null;
        }
    }

    private bool IsTeleportPositionBlocked(EntityCoordinates candidate, CollisionGroup collisionMask)
    {
        if (_turfSystem.TryGetTileRef(candidate, out var tileRef) && !tileRef.Value.Tile.IsEmpty)
            return _turfSystem.IsTileBlocked(tileRef.Value, collisionMask);

        var mapCoords = _transform.ToMapCoordinates(candidate);
        if (mapCoords.MapId == MapId.Nullspace)
            return true;

        var box = new Box2(mapCoords.Position - TeleportCheckExtents, mapCoords.Position + TeleportCheckExtents);

        foreach (var ent in _entityLookup.GetEntitiesIntersecting(mapCoords.MapId, box, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (HasComp<MapGridComponent>(ent))
                continue;

            if (!TryComp<FixturesComponent>(ent, out var fixtures))
                continue;

            var (pos, rot) = _transform.GetWorldPositionRotation(ent);
            var worldXform = new Transform(pos, (float)rot.Theta);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                if ((fixture.CollisionLayer & (int)collisionMask) == 0)
                    continue;

                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    if (fixture.Shape.ComputeAABB(worldXform, i).Intersects(box))
                        return true;
                }
            }
        }

        return false;
    }
}