using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.ChromiteCaves;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Server.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Server.Tiles;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandJaunterSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedLavalandJaunterSystem _jaunter = default!;
    [Dependency] private readonly LavalandMadMinerSystem _madMiner = default!;
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!;
    [Dependency] private readonly LavalandBossArenaSystem _arena = default!;
    [Dependency] private readonly LavalandChromiteCavesSystem _caves = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandJaunterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
    }

    private void OnUseInHand(Entity<LavalandJaunterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_madMiner.TryInterceptJaunter(args.User, ent.Owner))
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;

        if (TryComp<UseDelayComponent>(ent.Owner, out var delay) &&
            !_useDelay.TryResetDelay((ent.Owner, delay), true))
        {
            return;
        }

        // In the Celestial maze the jaunter works by hand once you have landed (not while jumping, falling
        // or asleep) and takes you back to lavaland; the fight blocks it completely.
        var userMap = Transform(args.User).MapUid;
        var inMaze = HasComp<LavalandCelestialMazeComponent>(userMap);
        if (HasComp<LavalandNoTeleportZoneComponent>(userMap) && !inMaze ||
            inMaze && (HasComp<LavalandPitDescentComponent>(args.User) || HasComp<LavalandPitFrozenComponent>(args.User)) ||
            _arena.IsInSealedArena(args.User))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-jaunter-blocked"), args.User, args.User, PopupType.MediumCaution);
            return;
        }

        // In the chromite caves the jaunter only finds its anchor while fauna raid the outpost.
        if (HasComp<LavalandChromiteCavesComponent>(userMap) &&
            (_caves.IsTravelling(args.User) || !_caves.IsJaunterAllowed(userMap)))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-jaunter-caves"), args.User, args.User, PopupType.MediumCaution);
            return;
        }

        if (_dayCycle.IsJaunterBlocked(Transform(args.User).MapUid))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-jaunter-night"), args.User, args.User, PopupType.MediumCaution);
            return;
        }

        if (!TryActivate(ent, args.User, false))
            _popup.PopupEntity(Loc.GetString("lavaland-jaunter-no-anchor"), args.User, args.User, PopupType.MediumCaution);
    }

    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        // The Celestial pit is a way down, not a death: no jaunter rescue, no mad miner.
        // In the Celestial zone the jaunter never fires on its own.
        if (args.Cancelled || HasComp<LavalandCelestialPitComponent>(args.Chasm) ||
            HasComp<LavalandChromitePitComponent>(args.Chasm) ||
            HasComp<LavalandNoTeleportZoneComponent>(Transform(args.Tripper).MapUid))
            return;

        // The chasms of the chromite caves are deadly unless a raid lets the jaunter through.
        var tripperMap = Transform(args.Tripper).MapUid;
        if (HasComp<LavalandChromiteCavesComponent>(tripperMap))
        {
            if (!_caves.IsJaunterAllowed(tripperMap) ||
                !_jaunter.TryFindJaunterOn(args.Tripper, out var cavesJaunter) ||
                _timing.CurTime < cavesJaunter.Comp.NextAutomaticUse)
            {
                return;
            }

            cavesJaunter.Comp.NextAutomaticUse = _timing.CurTime + cavesJaunter.Comp.AutomaticUseCooldown;
            if (TryActivate(cavesJaunter, args.Tripper, true))
                args.Cancel();
            return;
        }

        var hasJaunter = _jaunter.TryFindJaunterOn(args.Tripper, out var jaunter);
        if (_madMiner.TryRescueFromChasm(
                args.Tripper, args, hasJaunter ? jaunter.Owner : null) ||
            !hasJaunter ||
            _timing.CurTime < jaunter.Comp.NextAutomaticUse ||
            _arena.IsInSealedArena(args.Tripper) ||
            _dayCycle.IsJaunterBlocked(Transform(args.Tripper).MapUid))
        {
            return;
        }

        jaunter.Comp.NextAutomaticUse = _timing.CurTime + jaunter.Comp.AutomaticUseCooldown;

        if (!TryActivate(jaunter, args.Tripper, true))
            return;

        args.Cancel();
    }

    private bool TryActivate(Entity<LavalandJaunterComponent> ent, EntityUid user, bool automatic)
    {
        if (!TryGetLavalandDestination(user, ent.Comp, out var destination))
            return false;

        StopPulling(user);

        _audio.PlayPvs(ent.Comp.DepartureSound, user);
        Spawn("PuddleSparkle", Transform(user).Coordinates);

        _transform.SetCoordinates(user, destination);

        _audio.PlayPvs(ent.Comp.ArrivalSound, user);
        Spawn("PuddleSparkle", Transform(user).Coordinates);

        _popup.PopupEntity(
            Loc.GetString(automatic ? "lavaland-jaunter-chasm-activate" : "lavaland-jaunter-activate"),
            user,
            user,
            PopupType.Medium);

        QueueDel(ent.Owner);
        return true;
    }

    private bool TryGetLavalandDestination(
        EntityUid user,
        LavalandJaunterComponent component,
        out EntityCoordinates destination)
    {
        destination = default;

        if (!TryGetLavalandMap(user, out var mapUid, out var lavalandMap) ||
            !_prototype.TryIndex<LavalandPlanetPrototype>(lavalandMap.Planet, out var planet))
        {
            return false;
        }

        if (TryFindSafeDestination(mapUid, planet.JaunterDestinationOffset, component, user, out destination) ||
            TryFindSafeDestination(mapUid, planet.FtlBeaconOffset, component, user, out destination))
        {
            return true;
        }

        return false;
    }

    private bool TryGetLavalandMap(
        EntityUid user,
        out EntityUid mapUid,
        [NotNullWhen(true)] out LavalandMapComponent? lavalandMap)
    {
        var xform = Transform(user);

        if (xform.MapUid is { } xformMapUid &&
            TryComp(xformMapUid, out lavalandMap))
        {
            mapUid = xformMapUid;
            return true;
        }

        // From the chromite caves too.
        if (_caves.TryGetLavaland(xform.MapUid, out var cavesLavaland) &&
            TryComp(cavesLavaland, out lavalandMap))
        {
            mapUid = cavesLavaland;
            return true;
        }

        // From the Celestial maze the jaunter leads back to the lavaland it belongs to.
        if (TryComp<LavalandCelestialMazeComponent>(xform.MapUid, out var maze) &&
            TryComp(maze.LavalandMap, out lavalandMap))
        {
            mapUid = maze.LavalandMap;
            return true;
        }

        var mapFromId = _map.GetMapOrInvalid(xform.MapID);
        if (mapFromId.IsValid() &&
            TryComp(mapFromId, out lavalandMap))
        {
            mapUid = mapFromId;
            return true;
        }

        if (xform.GridUid is { } gridUid &&
            TryComp<LavalandOutpostComponent>(gridUid, out var outpost) &&
            TryComp(outpost.Map, out lavalandMap))
        {
            mapUid = outpost.Map;
            return true;
        }

        mapUid = default;
        lavalandMap = default!;
        return false;
    }

    private bool TryFindSafeDestination(
        EntityUid mapUid,
        Vector2 center,
        LavalandJaunterComponent component,
        EntityUid user,
        out EntityCoordinates destination)
    {
        destination = default;

        var anchor = new EntityCoordinates(mapUid, center);
        if (TryGetSafeTileCoordinates(anchor, user, out destination))
            return true;

        for (var i = 0; i < component.SearchAttempts; i++)
        {
            var distance = MathF.Sqrt(_random.NextFloat()) * component.SearchRadius;
            var offset = _random.NextAngle().ToVec() * distance;
            var coords = new EntityCoordinates(mapUid, center + offset);

            if (TryGetSafeTileCoordinates(coords, user, out destination))
                return true;
        }

        return false;
    }

    private bool TryGetSafeTileCoordinates(EntityCoordinates coordinates, EntityUid user, out EntityCoordinates destination)
    {
        destination = default;

        if (!_turf.TryGetTileRef(coordinates, out var tile) ||
            _turf.IsSpace(tile.Value))
        {
            return false;
        }

        if (TryComp<PhysicsComponent>(user, out var physics) &&
            _turf.IsTileBlocked(tile.Value, (CollisionGroup) physics.CollisionMask))
        {
            return false;
        }

        if (HasUnsafeAnchoredEntity(tile.Value))
            return false;

        destination = _map.ToCenterCoordinates(tile.Value);
        return true;
    }

    private bool HasUnsafeAnchoredEntity(TileRef tile)
    {
        if (!TryComp<MapGridComponent>(tile.GridUid, out var grid))
            return true;

        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (anchoredUid == null)
                continue;

            if (HasComp<ChasmComponent>(anchoredUid.Value) ||
                HasComp<TileEntityEffectComponent>(anchoredUid.Value))
            {
                return true;
            }
        }

        return false;
    }

    private void StopPulling(EntityUid user)
    {
        if (TryComp<PullableComponent>(user, out var pullable) &&
            _pulling.IsPulled(user, pullable))
        {
            _pulling.TryStopPull(user, pullable);
        }

        if (TryComp<PullerComponent>(user, out var puller) &&
            puller.Pulling is { } pulled &&
            TryComp<PullableComponent>(pulled, out var pulledComp))
        {
            _pulling.TryStopPull(pulled, pulledComp);
        }
    }
}
