// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.DeadSpace.FireAxe;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.FireAxe;

public sealed partial class FireAxeDeconstructSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FireAxeDeconstructComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FireAxeDeconstructComponent, FireAxeDeconstructDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, FireAxeDeconstructComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target != null)
            return;

        var location = args.ClickLocation;
        var user = args.User;
        var gridUid = Transform(user).GridUid;

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, location);

        if (tile.Tile.IsEmpty)
            return;

        var tileDef = _turf.GetContentTileDefinition(tile);

        if (!tileDef.IsSubFloor || tileDef.Indestructible)
            return;

        if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
            return;

        var ev = new FireAxeDeconstructDoAfterEvent(GetNetCoordinates(location), GetNetEntity(gridUid.Value));
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(component.DeconstructDelay), ev, uid)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, FireAxeDeconstructComponent component, FireAxeDeconstructDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var gridUid = GetEntity(args.GridId);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var location = GetCoordinates(args.Location);
        var tile = _mapSystem.GetTileRef(gridUid, mapGrid, location);

        if (tile.Tile.IsEmpty)
            return;

        var tileDef = _turf.GetContentTileDefinition(tile);

        if (!tileDef.IsSubFloor || tileDef.Indestructible)
            return;

        if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
            return;

        _tile.DeconstructTile(tile, spawnItem: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/crowbar.ogg"), uid);
    }
}