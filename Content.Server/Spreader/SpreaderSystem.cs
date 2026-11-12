using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Spreader;
using Content.Shared.Tag;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Spreader;

/// <summary>
/// Handles generic spreading logic, where one anchored entity spreads to neighboring tiles.
/// </summary>
public sealed partial class SpreaderSystem : EntitySystem
{
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    [Dependency] private EntityQuery<EdgeSpreaderComponent> _edgeSpreaderQuery = default!;
    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<DockingComponent> _dockingQuery = default!;

    /// <summary>
    /// Cached maximum number of updates per spreader prototype. This is applied per-grid.
    /// </summary>
    private Dictionary<string, int> _prototypeUpdates = default!;

    /// <summary>
    /// Remaining number of updates per prototype on the grid being processed.
    /// </summary>
    private readonly Dictionary<string, int> _updates = [];
    private readonly List<EntityUid> _spreaders = [];

    public const float SpreadCooldownSeconds = 1;

    private static readonly ProtoId<TagPrototype> IgnoredTag = "SpreaderIgnore";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<SpreaderGridComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);

        SubscribeLocalEvent<EdgeSpreaderComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, ComponentStartup>(OnActiveStartup);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, ComponentShutdown>(OnActiveShutdown);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, ComponentRemove>(OnActiveRemove);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, GridUidChangedEvent>(OnGridChanged);
        SubscribeLocalEvent<ActiveEdgeSpreaderComponent, MetaFlagRemoveAttemptEvent>(OnMetaFlagRemoveAttempt);
        SetupPrototypes();
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<EdgeSpreaderPrototype>())
            SetupPrototypes();
    }

    private void SetupPrototypes()
    {
        _prototypeUpdates = [];
        foreach (var proto in ProtoMan.EnumeratePrototypes<EdgeSpreaderPrototype>())
        {
            _prototypeUpdates.Add(proto.ID, proto.UpdatesPerSecond);
        }
    }

    private void OnAirtightChanged(ref AirtightChanged ev)
    {
        ActivateSpreadableNeighbors(ev.Entity, ev.Position);
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        EnsureComp<SpreaderGridComponent>(ev.EntityUid);
    }

    private void OnTileChanged(Entity<SpreaderGridComponent> entity, ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            if (!_turf.IsSpace(change.OldTile) || _turf.IsSpace(change.NewTile))
                continue;

            ActivateSpreadableNeighbors(entity, (entity, change.GridIndices));
        }
    }

    private void OnTerminating(Entity<EdgeSpreaderComponent> entity, ref EntityTerminatingEvent args)
    {
        ActivateSpreadableNeighbors(entity);
    }

    private void OnActiveStartup(Entity<ActiveEdgeSpreaderComponent> entity, ref ComponentStartup args)
    {
        _metadata.AddFlag(entity, MetaDataFlags.ExtraTransformEvents);
        SetSpreaderGrid(entity, Transform(entity).GridUid);
    }

    private void OnActiveShutdown(Entity<ActiveEdgeSpreaderComponent> entity, ref ComponentShutdown args)
    {
        SetSpreaderGrid(entity, null);
    }

    private void OnGridChanged(Entity<ActiveEdgeSpreaderComponent> entity, ref GridUidChangedEvent args)
    {
        if (entity.Comp.LifeStage <= ComponentLifeStage.Running)
            SetSpreaderGrid(entity, args.NewGrid);
    }

    private void OnActiveRemove(Entity<ActiveEdgeSpreaderComponent> entity, ref ComponentRemove args)
    {
        _metadata.RemoveFlag(entity, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnMetaFlagRemoveAttempt(Entity<ActiveEdgeSpreaderComponent> entity, ref MetaFlagRemoveAttemptEvent args)
    {
        if (entity.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
    }

    private void SetSpreaderGrid(Entity<ActiveEdgeSpreaderComponent> entity, EntityUid? gridUid)
    {
        if (entity.Comp.Grid == gridUid)
            return;

        if (TryComp<SpreaderGridComponent>(entity.Comp.Grid, out var oldGrid))
            oldGrid.ActiveSpreaders.Remove(entity);

        entity.Comp.Grid = gridUid;
        if (gridUid != null && !TerminatingOrDeleted(gridUid.Value))
            EnsureComp<SpreaderGridComponent>(gridUid.Value).ActiveSpreaders.Add(entity);
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        var spreadGrids = EntityQueryEnumerator<SpreaderGridComponent>();
        var xforms = GetEntityQuery<TransformComponent>();
        var metadata = GetEntityQuery<MetaDataComponent>();
        var activeQuery = GetEntityQuery<ActiveEdgeSpreaderComponent>();

        while (spreadGrids.MoveNext(out var uid, out var grid))
        {
            grid.UpdateAccumulator -= frameTime;
            if (grid.UpdateAccumulator > 0)
                continue;

            grid.UpdateAccumulator += SpreadCooldownSeconds;
            if (grid.ActiveSpreaders.Count == 0)
                continue;

            _updates.Clear();
            _spreaders.Clear();
            _spreaders.AddRange(grid.ActiveSpreaders);
            _robustRandom.Shuffle(_spreaders);

            foreach (var spreaderUid in _spreaders)
            {
                if (!activeQuery.TryGetComponent(spreaderUid, out var active) || active.Grid != uid ||
                    !metadata.TryGetComponent(spreaderUid, out var meta) || meta.EntityPaused ||
                    TerminatingOrDeleted(spreaderUid, meta) ||
                    !xforms.TryGetComponent(spreaderUid, out var xform) || xform.GridUid != uid)
                {
                    continue;
                }

                if (!_edgeSpreaderQuery.TryGetComponent(spreaderUid, out var spreader))
                {
                    RemComp<ActiveEdgeSpreaderComponent>(spreaderUid);
                    continue;
                }

                if ((!_updates.TryGetValue(spreader.Id, out var updates) &&
                     !_prototypeUpdates.TryGetValue(spreader.Id, out updates)) || updates < 1)
                {
                    continue;
                }

                var previousUpdates = updates;
                Spread(spreaderUid, xform, spreader.Id, ref updates);
                _updates[spreader.Id] = Math.Min(updates, previousUpdates - 1);
            }
        }

        _spreaders.Clear();
    }

    private void Spread(EntityUid uid, TransformComponent xform, ProtoId<EdgeSpreaderPrototype> prototype, ref int updates)
    {
        GetNeighbors(uid, xform, prototype, out var freeTiles, out var neighbors);

        var ev = new SpreadNeighborsEvent()
        {
            NeighborFreeTiles = freeTiles,
            Neighbors = neighbors,
            Updates = updates,
        };

        RaiseLocalEvent(uid, ref ev);
        updates = ev.Updates;
    }

    /// <summary>
    /// Gets the neighboring node data for the specified entity and the specified node group.
    /// </summary>
    public void GetNeighbors(EntityUid uid, TransformComponent comp, ProtoId<EdgeSpreaderPrototype> prototype,
        out ValueList<(MapGridComponent, TileRef)> freeTiles, out ValueList<EntityUid> neighbors)
    {
        freeTiles = [];
        neighbors = [];
        if (!ProtoMan.Resolve(prototype, out var spreaderPrototype))
            return;

        if (!TryComp<MapGridComponent>(comp.GridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(comp.GridUid.Value, grid, comp.Coordinates);
        var blockedAtmosDirs = AtmosDirection.Invalid;

        // Due to docking ports they may not necessarily be opposite directions.
        var neighborTiles = new ValueList<(EntityUid entity, MapGridComponent grid, Vector2i Indices, AtmosDirection OtherDir, AtmosDirection OurDir)>();

        // Check if anything on our own tile blocking that direction.
        var ourEnts = _map.GetAnchoredEntities(comp.GridUid.Value, grid, tile);

        while (ourEnts.MoveNext(out var ent))
        {
            // Spread via docks in a special-case.
            if (_dockingQuery.TryGetComponent(ent, out var dock) &&
                dock.Docked &&
                TryComp(ent, out TransformComponent? xform) &&
                TryComp(dock.DockedWith, out TransformComponent? dockedXform) &&
                TryComp<MapGridComponent>(dockedXform.GridUid, out var dockedGrid))
            {
                neighborTiles.Add((
                    dockedXform.GridUid.Value, dockedGrid,
                    _map.CoordinatesToTile(dockedXform.GridUid.Value,
                        dockedGrid,
                        dockedXform.Coordinates),
                    xform.LocalRotation.ToAtmosDirection(),
                    dockedXform.LocalRotation.ToAtmosDirection()));
            }

            // If we're on a blocked tile work out which directions we can go.
            if (!_airtightQuery.TryGetComponent(ent, out var airtight) || !airtight.AirBlocked ||
                _tag.HasTag(ent.Value, IgnoredTag))
            {
                continue;
            }

            foreach (var value in new[] { AtmosDirection.North, AtmosDirection.East, AtmosDirection.South, AtmosDirection.West })
            {
                if ((value & airtight.AirBlockedDirection) == 0x0)
                    continue;

                blockedAtmosDirs |= value;
                break;
            }
            break;
        }

        // Add the normal neighbors.
        for (var i = 0; i < 4; i++)
        {
            var atmosDir = (AtmosDirection)(1 << i);
            var neighborPos = tile.Offset(atmosDir);
            neighborTiles.Add((comp.GridUid.Value, grid, neighborPos, atmosDir, i.ToOppositeDir()));
        }

        foreach (var (neighborEnt, neighborGrid, neighborPos, ourAtmosDir, otherAtmosDir) in neighborTiles)
        {
            // This tile is blocked to that direction.
            if ((blockedAtmosDirs & ourAtmosDir) != 0x0)
                continue;

            if (!_map.TryGetTileRef(neighborEnt, neighborGrid, neighborPos, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (spreaderPrototype.PreventSpreadOnSpaced && _turf.IsSpace(tileRef))
                continue;

            var directionEnumerator = _map.GetAnchoredEntities(neighborEnt, neighborGrid, neighborPos);
            var occupied = false;

            while (directionEnumerator.MoveNext(out var ent))
            {
                if (!_airtightQuery.TryGetComponent(ent, out var airtight) || !airtight.AirBlocked || _tag.HasTag(ent.Value, IgnoredTag))
                {
                    continue;
                }

                if ((airtight.AirBlockedDirection & otherAtmosDir) == 0x0)
                    continue;

                occupied = true;
                break;
            }

            if (occupied)
                continue;

            var hasNeighbor = false;
            directionEnumerator = _map.GetAnchoredEntities(neighborEnt, neighborGrid, neighborPos);

            while (directionEnumerator.MoveNext(out var ent))
            {
                if (!_edgeSpreaderQuery.TryGetComponent(ent, out var spreader))
                    continue;

                if (spreader.Id != prototype)
                    continue;

                neighbors.Add(ent.Value);
                hasNeighbor = true;
                break;
            }

            if (!hasNeighbor)
                freeTiles.Add((neighborGrid, tileRef));
        }
    }

    /// <summary>
    /// This function activates all spreaders that are adjacent to a given entity. This also activates other spreaders
    /// on the same tile as the current entity (for thin airtight entities like windoors).
    /// </summary>
    public void ActivateSpreadableNeighbors(EntityUid origin, (EntityUid Grid, Vector2i Tile)? position = null)
    {
        Vector2i tile;
        EntityUid gridUid;
        MapGridComponent? gridComp;

        if (position == null)
        {
            var transform = Transform(origin);
            if (!TryComp(transform.GridUid, out gridComp) || TerminatingOrDeleted(transform.GridUid.Value))
                return;

            tile = _map.TileIndicesFor(transform.GridUid.Value, gridComp, transform.Coordinates);
            gridUid = transform.GridUid.Value;
        }
        else
        {
            if (!TryComp(position.Value.Grid, out gridComp))
                return;
            (gridUid, tile) = position.Value;
        }

        var anchored = _map.GetAnchoredEntities(gridUid, gridComp, tile);
        while (anchored.MoveNext(out var entity))
        {
            // Don't re-activate the terminating entity
            if (entity == origin)
                continue;
            DebugTools.Assert(Transform(entity.Value).Anchored);

            // Activate any edge spreaders that are non-terminating
            if (_edgeSpreaderQuery.HasComponent(entity) && !TerminatingOrDeleted(entity))
                EnsureComp<ActiveEdgeSpreaderComponent>(entity.Value);
        }

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection)(1 << i);
            var adjacentTile = SharedMapSystem.GetDirection(tile, direction.ToDirection());
            anchored = _map.GetAnchoredEntities(gridUid, gridComp, adjacentTile);

            while (anchored.MoveNext(out var entity))
            {
                DebugTools.Assert(Transform(entity.Value).Anchored);

                // Activate any edge spreaders that are non-terminating
                if (_edgeSpreaderQuery.HasComponent(entity) && !TerminatingOrDeleted(entity))
                    EnsureComp<ActiveEdgeSpreaderComponent>(entity.Value);
            }
        }
    }

    public bool RequiresFloorToSpread(EntProtoId<EdgeSpreaderComponent> spreader)
    {
        if (!ProtoMan.Index(spreader).TryComp<EdgeSpreaderComponent>(out var spreaderComp, EntityManager.ComponentFactory))
            return false;

        return ProtoMan.Index(spreaderComp.Id).PreventSpreadOnSpaced;
    }
}
