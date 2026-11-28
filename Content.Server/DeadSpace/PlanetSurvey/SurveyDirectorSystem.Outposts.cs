// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Shared.DeadSpace.PlanetSurvey;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly TimeSpan AlarmDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan AlarmHold = TimeSpan.FromMinutes(4);
    private static readonly SoundSpecifier Siren = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");
    private const string PowerNode = "SurveyPowerNode";
    private const string CommsMast = "SurveyCommsMast";
    private const float LampLight = 6f;
    private const float CloseSight = 4f;
    private static readonly Vector2 OutpostHalf = new(18.5f, 8.5f);
    private const float NoiseRange = 22f;
    private static readonly TimeSpan NoiseMemory = TimeSpan.FromSeconds(1.5);

    private readonly HashSet<EntityUid> _noisy = new();

    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    private readonly Dictionary<EntityUid, float> _sight = new();

    private void InitializeOutposts()
    {
        SubscribeLocalEvent<SurveyDeviceComponent, GetVerbsEvent<InteractionVerb>>(OnDeviceVerbs);
        SubscribeLocalEvent<SurveyDeviceComponent, SurveySabotageDoAfterEvent>(OnSabotaged);
        SubscribeLocalEvent<SurveyDeviceComponent, ComponentShutdown>(OnDeviceGone);
        SubscribeLocalEvent<SurveyFactoryCoreComponent, ComponentShutdown>(OnCoreDown);
    }

    private async Task SpawnOutposts(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots, Vector2 dir, Vector2 far, int seed)
    {
        if (world.Outpost is not { } config || world.OutpostCount <= 0)
            return;

        var map = ent.Owner;
        var grid = Comp<MapGridComponent>(map);
        var director = ent.Comp;
        var side = random.Next(2) == 0 ? 1f : -1f;

        for (var i = 0; i < world.OutpostCount; i++)
        {
            var turn = (MathF.PI * 0.55f + i * 0.5f) * side;
            side = -side;
            var outward = new Vector2(dir.X * MathF.Cos(turn) - dir.Y * MathF.Sin(turn), dir.X * MathF.Sin(turn) + dir.Y * MathF.Cos(turn));
            var pos = director.BaseCentre + outward * (director.BaseRadius + 24f);
            if (NearPad(director, pos, PadClearance + 16f))
                pos = director.BaseCentre - outward * (director.BaseRadius + 24f);

            var dungeons = await _dungeon.GenerateDungeonAsync(_proto.Index(config), map, grid, (Vector2i) (pos - OutpostHalf), seed + 100 + i);
            var outpost = new SurveyOutpost { Pos = pos };
            if (world.Devices)
                outpost.Role = i == 0 ? SurveyDeviceRole.Power : SurveyDeviceRole.Comms;

            var index = director.Outposts.Count;
            director.Outposts.Add(outpost);
            AddPoi(ent, pos, world.Kind == SurveyKind.DeadElement ? SurveyPoiKind.Shelter : SurveyPoiKind.Settlement);

            SurveySpot? deviceRoom = null;
            foreach (var dungeon in dungeons)
            {
                foreach (var tile in dungeon.AllTiles)
                {
                    director.Extent = Math.Max(director.Extent, Vector2.Distance(tile + new Vector2(0.5f, 0.5f), director.BaseCentre));
                }

                foreach (var room in dungeon.Rooms)
                {
                    var spot = new SurveySpot(room.Center, room.Tiles.ToList()) { Outpost = index };
                    spots.Add(spot);
                    deviceRoom ??= spot;
                }
            }

            if (outpost.Role is { } role && deviceRoom != null)
            {
                var device = SpawnAtPosition(role == SurveyDeviceRole.Power ? PowerNode : CommsMast,
                    new EntityCoordinates(map, FreeTile(map, deviceRoom, random)));
                AddPoi(ent, Pos(device), SurveyPoiKind.Device);
                if (role == SurveyDeviceRole.Comms)
                    SpawnAtPosition(RadioKey, new EntityCoordinates(map, FreeTile(map, deviceRoom, random)));
            }
        }
    }

    private void SpawnLamps(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        if (world.Lamp is not { } lamp)
            return;

        var placed = 0;
        foreach (var spot in spots)
        {
            if (spot.Tiles.Count < 9 || placed >= 24)
                continue;

            var uid = SpawnAtPosition(lamp, new EntityCoordinates(ent.Owner, FreeTile(ent.Owner, spot, random)));
            ent.Comp.Lamps.Add(uid);
            placed++;
        }
    }

    private bool InBase(SurveyDirectorComponent director, Vector2 pos)
    {
        return Vector2.Distance(pos, director.BaseCentre) <= director.BaseRadius + 4f;
    }

    private void OnDeviceVerbs(Entity<SurveyDeviceComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || ent.Comp.Broken)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(ent.Comp.Role == SurveyDeviceRole.Power ? "survey-verb-sabotage-power" : "survey-verb-sabotage-comms"),
            Act = () =>
            {
                var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(ent.Comp.Time), new SurveySabotageDoAfterEvent(), ent, target: ent)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                };
                _doAfter.TryStartDoAfter(doAfter);
            },
        });
    }

    private void OnSabotaged(Entity<SurveyDeviceComponent> ent, ref SurveySabotageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Broken)
            return;

        args.Handled = true;
        Disable(ent);
        _popup.PopupEntity(Loc.GetString("survey-sabotage-done"), ent, args.User, PopupType.Medium);
    }

    private void OnDeviceGone(Entity<SurveyDeviceComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.Broken)
            Disable(ent);
    }

    private void Disable(Entity<SurveyDeviceComponent> ent)
    {
        ent.Comp.Broken = true;
        if (Transform(ent).MapUid is not { } map || !TryComp<SurveyDirectorComponent>(map, out var director) || TerminatingOrDeleted(map))
            return;

        if (ent.Comp.Role == SurveyDeviceRole.Power)
            Blackout((map, director));
        else
            CutComms((map, director));
    }

    private void Blackout(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (director.Blackout)
            return;

        director.Blackout = true;
        foreach (var lamp in director.Lamps)
        {
            if (Exists(lamp) && InBase(director, Pos(lamp)))
                _lights.SetEnabled(lamp, false);
        }

        var doors = EntityQueryEnumerator<SurveyNativeDoorComponent, TransformComponent>();
        while (doors.MoveNext(out var door, out _, out var xform))
        {
            if (xform.MapUid == ent.Owner && InBase(director, _xform.GetWorldPosition(xform)))
                _power.SetNeedsPower(door, true);
        }

        foreach (var squad in director.Squads)
        {
            if (squad.Alive > 0 && InBase(director, squad.Home.Position))
                BarkOne(squad, "blackout");
        }

        Radio(ent, null, "blackout");

        Progress(ent.Owner, "power");
    }

    private void CutComms(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (director.CommsCut)
            return;

        director.CommsCut = true;
        director.AlarmAt = null;
        foreach (var squad in director.Squads)
        {
            if (squad.Alive > 0 && squad.Engaged)
                BarkOne(squad, "nocomms");
        }

        Progress(ent.Owner, "comms");
    }

    private void RaiseAlarm(Entity<SurveyDirectorComponent> ent, SurveySquad squad)
    {
        var director = ent.Comp;
        if (director.Alarm || director.AlarmAt != null || director.CommsCut)
            return;

        director.AlarmAt = _timing.CurTime + AlarmDelay;
        director.AlarmSquad = squad;
        BarkOne(squad, "alarm");
    }

    private void TickAlarm(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;

        if (director.AlarmAt is { } at && now >= at)
        {
            director.AlarmAt = null;
            if (director.CommsCut || director.AlarmSquad is not { Alive: > 0 })
                return;

            director.Alarm = true;
            director.AlarmEver = true;
            Radio(ent, Speaker(director.AlarmSquad), "alarm", ("where", Where(director, director.AlarmSquad.LastPos)));
            director.AlarmEnds = now + AlarmHold;
            director.Threat = Math.Min(ThreatMax, director.Threat + 0.25f);
            var soon = now + TimeSpan.FromSeconds(45);
            if (director.NextWave > soon)
                director.NextWave = soon;

            foreach (var squad in director.Squads)
            {
                if (squad.Alive == 0)
                    continue;

                squad.Armed = false;
                ArmUp(director, squad);
            }

            if (!director.Blackout)
                _audio.PlayPvs(Siren, new EntityCoordinates(ent.Owner, director.BaseCentre), AudioParams.Default.WithVolume(4f).WithMaxDistance(60f));
            return;
        }

        if (director.Alarm && now > director.AlarmEnds && now - director.LastContact > TimeSpan.FromSeconds(90))
            director.Alarm = false;
    }

    private void TickOutposts(Entity<SurveyDirectorComponent> ent)
    {
        foreach (var outpost in ent.Comp.Outposts)
        {
            if (outpost.Taken || outpost.Squad is not { } squad || squad.Alive > 0)
                continue;

            outpost.Taken = true;
            if (!ent.Comp.AlarmEver)
                Progress(ent.Owner, "stealth");
        }
    }

    private void UpdateSight(Entity<SurveyDirectorComponent> ent)
    {
        _sight.Clear();
        var director = ent.Comp;
        TryComp<MapGridComponent>(ent.Owner, out var grid);
        var night = IsNight(ent.Owner);

        foreach (var (uid, pos) in _players)
        {
            var lit = CarriesLight(uid);
            var dark = night;

            if (!dark && grid != null &&
                _map.TryGetTileRef(ent.Owner, grid, Transform(uid).Coordinates, out var tile) &&
                !tile.Tile.IsEmpty &&
                !_weather.CanWeatherAffect(ent.Owner, grid, tile))
            {
                dark = director.Blackout && InBase(director, pos) || !NearLamp(director, pos);
            }

            _sight[uid] = dark ? (lit ? 1.25f : 0.6f) : 1f;
        }

        _noisy.Clear();
        var now = _timing.CurTime;
        foreach (var (uid, _) in _players)
        {
            foreach (var held in _hands.EnumerateHeld(uid))
            {
                if (TryComp<Content.Shared.Weapons.Ranged.Components.GunComponent>(held, out var gun) &&
                    gun.NextFire > now - NoiseMemory)
                {
                    _noisy.Add(uid);
                    break;
                }
            }
        }
    }

    private EntityUid? SquadSees(SurveyDirectorComponent director, SurveySquad squad, float range)
    {
        foreach (var member in squad.Members)
        {
            if (!Alive(member))
                continue;

            var pos = Pos(member);
            if (NearestSeen(director, pos, range, false) is { } seen)
                return seen;

            foreach (var (uid, playerPos) in _players)
            {
                if (_noisy.Contains(uid) && Vector2.Distance(pos, playerPos) <= NoiseRange)
                    return uid;
            }
        }

        return null;
    }

    private float SightOf(EntityUid player, bool beast, SurveyDirectorComponent director)
    {
        if (beast)
            return director.Rain || director.Storm ? 0.6f : 1f;

        return _sight.GetValueOrDefault(player, 1f);
    }

    private EntityUid? NearestSeen(SurveyDirectorComponent director, Vector2 from, float range, bool beast)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var (uid, pos) in _players)
        {
            var d = Vector2.Distance(pos, from);
            if (d > Math.Max(CloseSight, range * SightOf(uid, beast, director)) || d >= bestDist)
                continue;

            bestDist = d;
            best = uid;
        }

        return best;
    }

    private bool NearLamp(SurveyDirectorComponent director, Vector2 pos)
    {
        foreach (var lamp in director.Lamps)
        {
            if (!Exists(lamp) || !_lights.TryGetLight(lamp, out var light) || !light.Enabled)
                continue;

            if (Vector2.DistanceSquared(Pos(lamp), pos) <= LampLight * LampLight)
                return true;
        }

        return false;
    }

    private bool CarriesLight(EntityUid uid)
    {
        if (_lights.TryGetLight(uid, out var own) && own.Enabled)
            return true;

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (_lights.TryGetLight(held, out var light) && light.Enabled)
                return true;
        }

        var slots = _inventory.GetSlotEnumerator(uid, Content.Shared.Inventory.SlotFlags.HEAD | Content.Shared.Inventory.SlotFlags.OUTERCLOTHING | Content.Shared.Inventory.SlotFlags.BELT);
        while (slots.NextItem(out var item))
        {
            if (_lights.TryGetLight(item, out var light) && light.Enabled)
                return true;
        }

        return false;
    }

    private bool IsNight(EntityUid map)
    {
        if (!TryComp<Content.Shared.Light.Components.LightCycleComponent>(map, out var cycle) || !cycle.Enabled)
            return false;

        var time = (float) (_timing.CurTime + cycle.Offset).TotalSeconds;
        var level = Content.Shared.Light.EntitySystems.SharedLightCycleSystem.CalculateLightLevel(cycle, time);
        return level < Math.Max(0.1f, cycle.MaxLightLevel) * 0.45f;
    }
}
