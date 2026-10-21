// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Staff of lava: pointing at floor turns it into lava after a short channel, pointing at lava turns it back.
/// </summary>
public sealed class LavalandLavaStaffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandLavaStaffComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LavalandLavaStaffComponent, LavalandLavaStaffDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<LavalandLavaStaffComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || _timing.CurTime < ent.Comp.NextUse)
            return;

        var user = args.User;
        if (!TryGetTile(args.ClickLocation, user, ent.Comp.Range, out var gridUid, out var grid, out var tile))
            return;

        args.Handled = true;
        var coordinates = _map.GridTileToLocal(gridUid, grid, tile);
        if (TryGetLava(gridUid, grid, tile, ent.Comp, out var lava))
        {
            QueueDel(lava);
            ent.Comp.NextUse = _timing.CurTime + ent.Comp.ResetCooldown;
            _popup.PopupEntity(Loc.GetString("lavaland-lava-staff-reset", ("user", user)), user, PopupType.Medium);
            if (ent.Comp.Sound != null)
                _audio.PlayPvs(ent.Comp.Sound, coordinates);
            return;
        }

        if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-lava-staff-blocked"), user, user);
            return;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.CreateDelay,
                new LavalandLavaStaffDoAfterEvent(GetNetCoordinates(coordinates)), ent.Owner, used: ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            }))
        {
            return;
        }

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.CreateDelay + TimeSpan.FromSeconds(1);
        ent.Comp.Warning = Spawn(ent.Comp.WarningPrototype, coordinates);
        _popup.PopupEntity(Loc.GetString("lavaland-lava-staff-point", ("user", user)), user, PopupType.MediumCaution);
    }

    private void OnDoAfter(Entity<LavalandLavaStaffComponent> ent, ref LavalandLavaStaffDoAfterEvent args)
    {
        if (ent.Comp.Warning is { } warning)
            QueueDel(warning);
        ent.Comp.Warning = null;

        if (args.Handled)
            return;
        args.Handled = true;

        if (args.Cancelled)
        {
            ent.Comp.NextUse = _timing.CurTime;
            return;
        }

        var coordinates = GetCoordinates(args.TargetCoordinates);
        if (!TryGetTile(coordinates, args.User, float.MaxValue, out var gridUid, out var grid, out var tile) ||
            TryGetLava(gridUid, grid, tile, ent.Comp, out _) ||
            _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
        {
            return;
        }

        var lava = Spawn(ent.Comp.LavaPrototype, _map.GridTileToLocal(gridUid, grid, tile));
        if (!Transform(lava).Anchored)
            _transform.AnchorEntity(lava, Transform(lava));

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.CreateCooldown;
        _popup.PopupEntity(Loc.GetString("lavaland-lava-staff-created", ("user", args.User)), args.User, PopupType.MediumCaution);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} created lava with {ToPrettyString(ent.Owner):staff} at {coordinates}");
    }

    private bool TryGetTile(EntityCoordinates coordinates,
        EntityUid user,
        float range,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;

        var target = _transform.ToMapCoordinates(coordinates);
        var origin = _transform.GetMapCoordinates(user);
        if (target.MapId != origin.MapId || Vector2.Distance(target.Position, origin.Position) > range ||
            _transform.GetGrid(coordinates) is not { } gridEnt || !TryComp(gridEnt, out MapGridComponent? gridComp))
        {
            return false;
        }

        gridUid = gridEnt;
        grid = gridComp;
        tile = _map.TileIndicesFor(gridEnt, gridComp, coordinates);
        return _map.TryGetTileRef(gridEnt, gridComp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
    }

    private bool TryGetLava(EntityUid gridUid, MapGridComponent grid, Vector2i tile, LavalandLavaStaffComponent staff, out EntityUid lava)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (uid is { } candidate && MetaData(candidate).EntityPrototype?.ID == staff.LavaPrototype.Id)
            {
                lava = candidate;
                return true;
            }
        }

        lava = default;
        return false;
    }
}
