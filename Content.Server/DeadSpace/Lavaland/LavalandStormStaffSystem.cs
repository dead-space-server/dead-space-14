// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Staff of storms from Legion: thunderbolts on a chosen tile after a short telegraph,
/// stronger and wider during an ash storm, and the ability to dispel the storm.
/// </summary>
public sealed class LavalandStormStaffSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LavalandWeatherSystem _weather = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandStormStaffComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LavalandStormStaffComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LavalandStormStaffComponent, LavalandStormStaffDispelDoAfterEvent>(OnDispel);
        SubscribeLocalEvent<LavalandStormStaffComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<LavalandStormStaffComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-storm-staff-charges",
            ("charges", ent.Comp.Charges), ("max", ent.Comp.MaxCharges)));
        if (_timing.CurTime < ent.Comp.NextDispel)
        {
            args.PushMarkup(Loc.GetString("lavaland-storm-staff-dispel-cooldown",
                ("minutes", (int) Math.Ceiling((ent.Comp.NextDispel - _timing.CurTime).TotalMinutes))));
        }
    }

    private void OnAfterInteract(Entity<LavalandStormStaffComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (HasComp<PacifiedComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-pacified"), user, user);
            return;
        }

        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-empty"), user, user);
            return;
        }

        var target = _transform.ToMapCoordinates(args.ClickLocation);
        var userPosition = _transform.GetMapCoordinates(user);
        if (target.MapId != userPosition.MapId ||
            Vector2.Distance(target.Position, userPosition.Position) > ent.Comp.Range ||
            !TryGetTile(args.ClickLocation, out var gridUid, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-invalid"), user, user);
            return;
        }

        foreach (var pending in ent.Comp.PendingBolts)
        {
            if (pending.Grid == gridUid && pending.Tile == tile)
            {
                _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-already-targeted"), user, user);
                return;
            }
        }

        var now = _timing.CurTime;
        var boosted = Transform(gridUid).MapUid is { } map && _weather.IsStormActive(map);
        ent.Comp.Charges--;
        ent.Comp.RechargeAt.Add(now + ent.Comp.RechargeTime);
        ent.Comp.PendingBolts.Add((now + ent.Comp.BoltDelay, gridUid, tile, boosted, user));

        var grid = Comp<MapGridComponent>(gridUid);
        Spawn(ent.Comp.TelegraphPrototype, _map.GridTileToLocal(gridUid, grid, tile));
        if (ent.Comp.AimSound != null)
            _audio.PlayPvs(ent.Comp.AimSound, ent);
    }

    private void OnUseInHand(Entity<LavalandStormStaffComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (Transform(args.User).MapUid is not { } map ||
            !TryComp<LavalandMapComponent>(map, out var lavaland) ||
            lavaland.WeatherStage == LavalandWeatherStage.Calm)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-no-storm"), args.User, args.User);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextDispel)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-dispel-cooldown",
                ("minutes", (int) Math.Ceiling((ent.Comp.NextDispel - _timing.CurTime).TotalMinutes))), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-dispel-start"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DispelTime,
            new LavalandStormStaffDispelDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDispel(Entity<LavalandStormStaffComponent> ent, ref LavalandStormStaffDispelDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (_timing.CurTime < ent.Comp.NextDispel ||
            Transform(args.User).MapUid is not { } map || !_weather.TryDispelStorm(map))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-no-storm"), args.User, args.User);
            return;
        }

        ent.Comp.NextDispel = _timing.CurTime + ent.Comp.DispelCooldown;
        _popup.PopupEntity(Loc.GetString("lavaland-storm-staff-dispelled", ("user", args.User)), args.User,
            PopupType.Large);
        if (ent.Comp.DispelSound != null)
            _audio.PlayPvs(ent.Comp.DispelSound, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandStormStaffComponent>();
        while (query.MoveNext(out var uid, out var staff))
        {
            for (var i = staff.RechargeAt.Count - 1; i >= 0; i--)
            {
                if (now < staff.RechargeAt[i])
                    continue;

                staff.RechargeAt.RemoveAt(i);
                staff.Charges = Math.Min(staff.Charges + 1, staff.MaxCharges);
                if (staff.RechargeSound != null)
                    _audio.PlayPvs(staff.RechargeSound, uid);
            }

            for (var i = staff.PendingBolts.Count - 1; i >= 0; i--)
            {
                var bolt = staff.PendingBolts[i];
                if (now < bolt.At)
                    continue;

                staff.PendingBolts.RemoveAt(i);
                Strike(staff, bolt.Grid, bolt.Tile, bolt.Boosted, bolt.User);
            }
        }
    }

    private void Strike(LavalandStormStaffComponent staff, EntityUid gridUid, Vector2i center, bool boosted, EntityUid user)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var centerCoords = _map.GridTileToLocal(gridUid, grid, center);
        Spawn(staff.BoltPrototype, centerCoords);
        if (staff.BoltSound != null)
            _audio.PlayPvs(staff.BoltSound, centerCoords);

        // During an ash storm the bolt also covers the eight surrounding tiles.
        var radius = boosted ? 1 : 0;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var tile = center + new Vector2i(x, y);
                var isCenter = x == 0 && y == 0;
                var coords = _map.GridTileToLocal(gridUid, grid, tile);
                if (!isCenter)
                    Spawn(staff.SparkPrototype, coords);

                _mobs.Clear();
                _lookup.GetEntitiesInRange(coords, 0.5f, _mobs);
                foreach (var mob in _mobs)
                {
                    if (_mobState.IsDead(mob, mob.Comp))
                        continue;

                    var multiplier = (HasComp<HumanoidAppearanceComponent>(mob) ? 1f : staff.CreatureMultiplier) *
                                     (isCenter ? 2f : 1f) *
                                     (boosted ? 2f : 1f);
                    _damageable.TryChangeDamage(mob.Owner, staff.BoltDamage * multiplier, origin: user);
                }
            }
        }
    }

    private bool TryGetTile(EntityCoordinates coordinates, out EntityUid gridUid, out Vector2i tile)
    {
        tile = default;
        gridUid = default;
        if (_transform.GetGrid(coordinates) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        gridUid = grid;
        tile = _map.TileIndicesFor(grid, gridComp, coordinates);
        return _map.TryGetTileRef(grid, gridComp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
    }
}
