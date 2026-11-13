using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Tiles;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland;

public sealed partial class LavalandShelterCapsuleSystem : EntitySystem
{
    private const CollisionGroup ShelterBlockerMask =
        CollisionGroup.Impassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.LowImpassable;

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    private List<Entity<MapGridComponent>> _nearbyGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandShelterCapsuleComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<LavalandShelterCapsuleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryResetUseDelay(ent.Owner))
            return;

        if (!TryGetDeploymentTile(args.User, out var mapUid, out var lavaland, out var tileRef))
        {
            Popup(args.User, "lavaland-shelter-capsule-not-lavaland");
            return;
        }

        if (!CanDeployAt(mapUid, lavaland, tileRef, ent.Comp, out var reason))
        {
            Popup(args.User, reason);
            return;
        }

        var deployCoordinates = _map.ToCenterCoordinates(tileRef);
        if (!_mapLoader.TryLoadGrid(Transform(mapUid).MapID, ent.Comp.ShelterPath, out var shelterGrid, offset: deployCoordinates.Position))
        {
            Log.Error($"Failed to load Lavaland shelter grid {ent.Comp.ShelterPath}.");
            Popup(args.User, "lavaland-shelter-capsule-failed");
            return;
        }

        var marker = EnsureComp<LavalandShelterComponent>(shelterGrid.Value.Owner);
        marker.Map = mapUid;
        _metadata.SetEntityName(shelterGrid.Value.Owner, Loc.GetString("lavaland-shelter-grid-name"));

        RebuildShelterAtmosphere(shelterGrid.Value);
        MoveUserIntoShelter(args.User, shelterGrid.Value, ent.Comp);
        Spawn(ent.Comp.DeployEffect, deployCoordinates);
        _audio.PlayPvs(ent.Comp.DeploySound, deployCoordinates);
        Popup(args.User, "lavaland-shelter-capsule-deployed", PopupType.Medium);
        QueueDel(ent.Owner);
    }

    private bool TryGetDeploymentTile(
        EntityUid user,
        out EntityUid mapUid,
        [NotNullWhen(true)] out LavalandMapComponent? lavaland,
        out TileRef tileRef)
    {
        mapUid = default;
        lavaland = null;
        tileRef = default;

        if (!TryGetLavalandMap(user, out mapUid, out lavaland) ||
            !TryComp<MapGridComponent>(mapUid, out _) ||
            !_turf.TryGetTileRef(Transform(user).Coordinates, out var userTile) ||
            userTile.Value.GridUid != mapUid ||
            userTile.Value.Tile.IsEmpty ||
            _turf.IsSpace(userTile.Value))
        {
            return false;
        }

        tileRef = userTile.Value;
        return true;
    }

    private bool TryGetLavalandMap(
        EntityUid user,
        out EntityUid mapUid,
        [NotNullWhen(true)] out LavalandMapComponent? lavaland)
    {
        var xform = Transform(user);

        if (xform.MapUid is { } xformMapUid &&
            TryComp(xformMapUid, out lavaland))
        {
            mapUid = xformMapUid;
            return true;
        }

        var mapFromId = _map.GetMapOrInvalid(xform.MapID);
        if (mapFromId.IsValid() &&
            TryComp(mapFromId, out lavaland))
        {
            mapUid = mapFromId;
            return true;
        }

        mapUid = default;
        lavaland = null;
        return false;
    }

    private bool CanDeployAt(
        EntityUid mapUid,
        LavalandMapComponent lavaland,
        TileRef center,
        LavalandShelterCapsuleComponent capsule,
        out string reason)
    {
        reason = string.Empty;

        if (!_prototype.TryIndex<LavalandPlanetPrototype>(lavaland.Planet, out var planet))
        {
            reason = "lavaland-shelter-capsule-not-lavaland";
            return false;
        }

        var radius = Math.Max(1, capsule.ClearanceRadius);
        var centerPosition = _map.ToCenterCoordinates(center).Position;

        if (IsOutsideMapBounds(centerPosition, radius, planet) ||
            IsInsideOutpostReservation(centerPosition, radius, planet, capsule))
        {
            reason = "lavaland-shelter-capsule-bad-area";
            return false;
        }

        if (HasNearbyNonTerrainGrid(mapUid, centerPosition, radius))
        {
            reason = "lavaland-shelter-capsule-bad-area";
            return false;
        }

        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid) ||
            !HasClearArea(mapUid, mapGrid, center, radius))
        {
            reason = "lavaland-shelter-capsule-blocked";
            return false;
        }

        return true;
    }

    private static bool IsOutsideMapBounds(Vector2 center, int radius, LavalandPlanetPrototype planet)
    {
        if (planet.MapHalfSize <= 0)
            return false;

        var limit = planet.MapHalfSize - radius - 1f;
        return Math.Abs(center.X) > limit || Math.Abs(center.Y) > limit;
    }

    private static bool IsInsideOutpostReservation(
        Vector2 center,
        int radius,
        LavalandPlanetPrototype planet,
        LavalandShelterCapsuleComponent capsule)
    {
        if (!planet.TerminalReservationEnabled || planet.TerminalReservationSize <= 0)
            return false;

        var halfSize = planet.TerminalReservationSize / 2f + radius + capsule.OutpostReservationPadding;
        var delta = center - planet.TerminalGridOffset;
        return Math.Abs(delta.X) <= halfSize && Math.Abs(delta.Y) <= halfSize;
    }

    private bool HasNearbyNonTerrainGrid(EntityUid mapUid, Vector2 center, int radius)
    {
        _nearbyGrids.Clear();

        var mapId = Transform(mapUid).MapID;
        var bounds = Box2.CenteredAround(center, Vector2.One * (radius * 2 + 1));
        _map.FindGridsIntersecting(mapId, bounds, ref _nearbyGrids);

        foreach (var grid in _nearbyGrids)
        {
            if (grid.Owner != mapUid)
                return true;
        }

        return false;
    }

    private bool HasClearArea(EntityUid mapUid, MapGridComponent mapGrid, TileRef center, int radius)
    {
        for (var x = center.GridIndices.X - radius; x <= center.GridIndices.X + radius; x++)
        {
            for (var y = center.GridIndices.Y - radius; y <= center.GridIndices.Y + radius; y++)
            {
                var indices = new Vector2i(x, y);
                if (!_map.TryGetTileRef(mapUid, mapGrid, indices, out var tile) ||
                    tile.Tile.IsEmpty ||
                    _turf.IsSpace(tile) ||
                    _turf.IsTileBlocked(tile.GridUid, tile.GridIndices, ShelterBlockerMask, grid: mapGrid) ||
                    HasUnsafeAnchoredEntity(tile, mapGrid))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasUnsafeAnchoredEntity(TileRef tile, MapGridComponent grid)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (anchoredUid == null ||
                TerminatingOrDeleted(anchoredUid.Value))
            {
                continue;
            }

            var uid = anchoredUid.Value;
            if (HasComp<ChasmComponent>(uid) ||
                HasComp<TileEntityEffectComponent>(uid) ||
                HasComp<LavalandOutpostComponent>(uid) ||
                HasComp<LavalandShelterComponent>(uid))
            {
                return true;
            }
        }

        return false;
    }

    private void MoveUserIntoShelter(
        EntityUid user,
        Entity<MapGridComponent> shelterGrid,
        LavalandShelterCapsuleComponent capsule)
    {
        if (TryFindSafeShelterTile(shelterGrid, capsule, out var coordinates))
            _transform.SetCoordinates(user, coordinates);
    }

    private bool TryFindSafeShelterTile(
        Entity<MapGridComponent> shelterGrid,
        LavalandShelterCapsuleComponent capsule,
        out EntityCoordinates coordinates)
    {
        coordinates = default;

        var centerTile = new Vector2i(
            (int) Math.Floor(capsule.ShelterSpawnOffset.X / shelterGrid.Comp.TileSize),
            (int) Math.Floor(capsule.ShelterSpawnOffset.Y / shelterGrid.Comp.TileSize));
        var maxRadius = Math.Max(2, capsule.ClearanceRadius);

        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Abs(x) != radius && Math.Abs(y) != radius)
                        continue;

                    var indices = centerTile + new Vector2i(x, y);
                    if (!_map.TryGetTileRef(shelterGrid.Owner, shelterGrid.Comp, indices, out var tile) ||
                        tile.Tile.IsEmpty ||
                        _turf.IsSpace(tile) ||
                        _turf.IsTileBlocked(tile.GridUid, tile.GridIndices, CollisionGroup.MobMask, grid: shelterGrid.Comp))
                    {
                        continue;
                    }

                    coordinates = _map.ToCenterCoordinates(tile, shelterGrid.Comp);
                    return true;
                }
            }
        }

        return false;
    }

    private void RebuildShelterAtmosphere(Entity<MapGridComponent> shelterGrid)
    {
        if (!TryComp<GridAtmosphereComponent>(shelterGrid.Owner, out var gridAtmosphere))
            return;

        _atmosphere.RebuildGridAtmosphere((shelterGrid.Owner, gridAtmosphere, shelterGrid.Comp));
    }

    private bool TryResetUseDelay(EntityUid uid)
    {
        return !TryComp<UseDelayComponent>(uid, out var useDelay) ||
               _useDelay.TryResetDelay((uid, useDelay), checkDelayed: true);
    }

    private void Popup(EntityUid user, string message, PopupType popup = PopupType.MediumCaution)
    {
        _popup.PopupEntity(Loc.GetString(message), user, user, popup);
    }
}
