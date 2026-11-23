// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Spider;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Spiders.SpiderTerror;

public sealed partial class SharedSpiderTerrorMagneticSystem : EntitySystem
{
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private IGameTiming _timing = default!;

    private ushort _vebTileId;
    private EntityQuery<SpiderWebObjectComponent> _webQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<SpiderComponent, MoveEvent>(OnMove);

        _webQuery = GetEntityQuery<SpiderWebObjectComponent>();
    }

    private ushort GetVebTileId()
    {
        if (_vebTileId == 0)
        {
            var tileDef = (ContentTileDefinition)_tileDef["VebTile"];
            _vebTileId = tileDef.TileId;
        }

        return _vebTileId;
    }

    private void OnMove(Entity<SpiderComponent> ent, ref MoveEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<GravityAffectedComponent>(ent.Owner, out var gravity))
            return;

        _gravity.RefreshWeightless((ent.Owner, gravity));
    }

    private void OnIsWeightless(Entity<SpiderComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled)
            return;

        if (IsOnWeb(ent.Owner))
        {
            args.IsWeightless = false;
            args.Handled = true;
        }
    }

    private bool IsOnWeb(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.GridUid == null)
            return false;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return false;

        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        if (tile.Tile.TypeId == GetVebTileId())
            return true;

        foreach (var ent in _map.GetAnchoredEntities(xform.GridUid.Value, grid, xform.Coordinates))
        {
            if (_webQuery.HasComp(ent))
                return true;
        }

        return false;
    }
}
