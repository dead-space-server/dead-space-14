using Content.Server.DeadSpace.Physics.Components;
using Content.Shared.Chasm;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.DeadSpace.Physics.Systems;

public sealed partial class HazardTileOverlapCleanupSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private EntityQuery<ChasmComponent> _chasmQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;

    public override void Initialize()
    {
        base.Initialize();

        _chasmQuery = GetEntityQuery<ChasmComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();

        SubscribeLocalEvent<HazardTileOverlapCleanupComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HazardTileOverlapCleanupComponent> ent, ref MapInitEvent args)
    {
        Timer.Spawn(0, () => CleanupOverlap(ent.Owner));
    }

    private void CleanupOverlap(EntityUid uid)
    {
        if (!TryComp(uid, out HazardTileOverlapCleanupComponent? cleanup) ||
            !TryComp(uid, out TransformComponent? xform) ||
            _container.IsEntityInContainer(uid) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return;
        }

        var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var anchoredUid))
        {
            if (anchoredUid == uid || !IsHazardTileEntity(anchoredUid.Value, cleanup))
                continue;

            QueueDel(uid);
            return;
        }
    }

    private bool IsHazardTileEntity(EntityUid uid, HazardTileOverlapCleanupComponent cleanup)
    {
        if (_chasmQuery.HasComp(uid))
            return true;

        if (!_metaQuery.TryComp(uid, out var meta) || meta.EntityPrototype == null)
            return false;

        var prototypeId = meta.EntityPrototype.ID;
        foreach (var hazardPrototype in cleanup.HazardPrototypes)
        {
            if (prototypeId == hazardPrototype.Id)
                return true;
        }

        return false;
    }
}
