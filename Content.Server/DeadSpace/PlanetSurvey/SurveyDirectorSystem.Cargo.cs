// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const float IntactDamage = 20f;

    private void InitializeCargo()
    {
        SubscribeLocalEvent<FTLStartedEvent>(OnFtlStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
        SubscribeLocalEvent<SurveyCargoComponent, ExaminedEvent>(OnCargoExamined);
    }

    private void OnCargoExamined(Entity<SurveyCargoComponent> ent, ref ExaminedEvent args)
    {
        var money = ent.Comp.Stolen ? ent.Comp.Money * 2 : ent.Comp.Money;
        var items = string.Join(", ", ent.Comp.Payload
            .GroupBy(p => p)
            .Select(g => g.Count() > 1
                ? $"{_proto.Index(g.Key).Name} ×{g.Count()}"
                : _proto.Index(g.Key).Name));

        using (args.PushGroup(nameof(SurveyCargoComponent)))
        {
            args.PushMarkup(Loc.GetString("survey-cargo-examine", ("need", Loc.GetString(ent.Comp.Need))));
            args.PushMarkup(items.Length == 0
                ? Loc.GetString("survey-cargo-examine-money", ("money", money))
                : Loc.GetString("survey-cargo-examine-reward", ("money", money), ("items", items)));

            if (ent.Comp.Stolen)
                args.PushMarkup(Loc.GetString("survey-cargo-examine-stolen"));
        }
    }

    private void SpawnCargo(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        if (world.Cargo.Count == 0)
            return;

        var places = _guarded.Count > 0 ? _guarded : spots;
        if (places.Count == 0)
            return;

        var kinds = world.Cargo.OrderBy(_ => random.Next()).ToList();
        for (var i = 0; i < world.CargoCount; i++)
        {
            var spot = places[i % places.Count];
            var at = FreeTile(ent.Owner, spot, random);
            var uid = SpawnAtPosition(kinds[i % kinds.Count], new EntityCoordinates(ent.Owner, at));
            var cargo = EnsureComp<SurveyCargoComponent>(uid);
            cargo.Planet = ent.Owner;
            cargo.Kind = world.Kind;
            cargo.Stolen = ent.Comp.Motive == SurveyMotive.Retrieve;

            if (cargo.Stolen)
                _meta.SetEntityName(uid, Loc.GetString("survey-cargo-stolen", ("name", Name(uid))));

            ent.Comp.Cargo.Add(uid);
        }
    }

    private void TickCargo(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        _scratch.Clear();
        _scratch.AddRange(director.Cargo);

        foreach (var cargo in _scratch)
        {
            if (TerminatingOrDeleted(cargo))
            {
                director.Cargo.Remove(cargo);
                director.CargoHolders.Remove(cargo);
                continue;
            }

            if (Carrier(cargo) is not { } carrier)
                continue;

            if (director.CargoHolders.TryGetValue(cargo, out var previous) && previous == carrier)
                continue;

            var first = !director.CargoHolders.ContainsKey(cargo);
            director.CargoHolders[cargo] = carrier;
            director.ReissueAll = true;

            if (!first)
                continue;

            director.Heat++;
            Hunt(ent, carrier);
        }

        _scratch.Clear();
    }

    private EntityUid? Carrier(EntityUid uid)
    {
        var xform = Transform(uid);
        while (xform.ParentUid.IsValid() && xform.ParentUid != xform.MapUid && xform.ParentUid != xform.GridUid)
        {
            var parent = xform.ParentUid;
            if (HasComp<ActorComponent>(parent))
                return parent;

            xform = Transform(parent);
        }

        return null;
    }

    private void Hunt(Entity<SurveyDirectorComponent> ent, EntityUid carrier)
    {
        switch (ent.Comp.Kind)
        {
            case SurveyKind.Raiders:
                var at = Pos(carrier);
                SurveySquad? best = null;
                var bestDist = float.MaxValue;
                foreach (var squad in ent.Comp.Squads)
                {
                    if (squad.Engaged || squad.Ambush || squad.Withdrawn || squad.Alive == 0)
                        continue;

                    var d = Vector2.DistanceSquared(squad.Home.Position, at);
                    if (d >= bestDist)
                        continue;

                    bestDist = d;
                    best = squad;
                }

                if (best == null)
                    break;

                best.Engaged = true;
                best.Target = carrier;
                best.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(40);
                BarkOne(best, "loot");
                break;

            case SurveyKind.Beasts:
                foreach (var squad in ent.Comp.Squads)
                {
                    foreach (var member in squad.Members)
                    {
                        if (!Alive(member) || Board(member) is not { } board || HasComp<SurveyMemberComponent>(member) && Comp<SurveyMemberComponent>(member).Prey)
                            continue;

                        board.SetValue(SurveyKeys.Call, At(ent.Owner, Pos(carrier)));
                        board.SetValue(SurveyKeys.Called, true);
                        Replan(member);
                    }
                }
                break;
        }
    }

    private void OnFtlStarted(ref FTLStartedEvent ev)
    {
        if (ev.FromMapUid is not { } from || !TryComp<SurveyDirectorComponent>(from, out var director))
            return;

        director.Shuttles.Remove(ev.Entity);
        var taken = 0;

        _scratch.Clear();
        _scratch.AddRange(director.Cargo);
        foreach (var uid in _scratch)
        {
            if (!TryComp<SurveyCargoComponent>(uid, out var cargo) || Transform(uid).GridUid != ev.Entity)
                continue;

            cargo.Departed = true;
            cargo.Outcome = director.Motive;
            cargo.Heat = director.Heat;
            director.Cargo.Remove(uid);
            director.CargoHolders.Remove(uid);
            taken++;
        }

        _scratch.Clear();

        var filter = Filter.Empty().AddInGrid(ev.Entity);
        var text = taken == 0
            ? Loc.GetString("survey-leave-empty")
            : Loc.GetString("survey-leave-cargo", ("count", taken), ("heat", director.Heat));
        _chat.DispatchFilteredAnnouncement(filter, text, sender: Loc.GetString("survey-sender-sky"), playSound: false, colorOverride: Color.LightSteelBlue);
    }

    private void OnFtlCompleted(ref FTLCompletedEvent ev)
    {
        if (TryComp<SurveyDirectorComponent>(ev.MapUid, out var director))
        {
            director.Shuttles.Add(ev.Entity);
            return;
        }

        var query = EntityQueryEnumerator<SurveyCargoComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cargo, out var xform))
        {
            if (!cargo.Departed || cargo.Resolved || xform.GridUid != ev.Entity)
                continue;

            Deliver((uid, cargo));
        }
    }

    private void Deliver(Entity<SurveyCargoComponent> cargo)
    {
        cargo.Comp.Resolved = true;
        var station = cargo.Comp.Planet is { } planet && TryComp<SurveyDirectorComponent>(planet, out var director)
            ? director.Station
            : null;

        var name = Name(cargo);
        var need = Loc.GetString(cargo.Comp.Need);
        string text;
        var paid = true;

        switch (cargo.Comp.Outcome)
        {
            case SurveyMotive.Retrieve:
                var damage = TryComp<DamageableComponent>(cargo, out var damageable) ? damageable.TotalDamage.Float() : 0f;
                if (damage >= IntactDamage)
                {
                    text = Loc.GetString("survey-deliver-broken", ("name", name));
                    paid = false;
                    break;
                }

                Pay(station, cargo.Comp.Money * 2);
                SpawnPayload(cargo);
                text = Loc.GetString("survey-deliver-retrieve", ("name", name), ("need", need), ("money", cargo.Comp.Money * 2));
                break;

            case SurveyMotive.Curse:
                Pay(station, cargo.Comp.Money);
                SpawnPayload(cargo);
                cargo.Comp.CurseAt = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(60, 150));
                text = Loc.GetString("survey-deliver-windfall", ("name", name), ("need", need), ("money", cargo.Comp.Money));
                break;

            default:
                Pay(station, cargo.Comp.Money);
                SpawnPayload(cargo);
                text = Loc.GetString("survey-deliver-windfall", ("name", name), ("need", need), ("money", cargo.Comp.Money));
                break;
        }

        if (station is { } home && Exists(home))
            _chat.DispatchStationAnnouncement(home, text, Loc.GetString("survey-sender"), playDefaultSound: false, colorOverride: Color.LightSkyBlue);

        if (!paid || Transform(cargo).GridUid is not { } shuttle)
            return;

        var crew = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == shuttle && _mobState.IsAlive(uid))
                crew.Add(uid);
        }

        var share = crew.Count == 0 ? 0 : Math.Max(150, cargo.Comp.Money / 2 / crew.Count);
        foreach (var member in crew)
        {
            GiveCash(member, share);
            _popup.PopupEntity(Loc.GetString("survey-personal-share", ("money", share)), member, member);
        }
    }

    private void Pay(EntityUid? station, int money)
    {
        if (station is not { } home || !TryComp<StationBankAccountComponent>(home, out var bank))
            return;

        _cargo.UpdateBankAccount((home, bank), money, bank.PrimaryAccount);
    }

    private void SpawnPayload(Entity<SurveyCargoComponent> cargo)
    {
        var coords = Transform(cargo).Coordinates;
        foreach (var proto in cargo.Comp.Payload)
        {
            SpawnAtPosition(proto, coords);
        }
    }

    private void UpdateCurses(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SurveyCargoComponent>();
        while (query.MoveNext(out var uid, out var cargo))
        {
            if (cargo.CurseAt is not { } at || now < at)
                continue;

            cargo.CurseAt = null;
            Curse((uid, cargo));
        }
    }

    private void Curse(Entity<SurveyCargoComponent> cargo)
    {
        var coords = Transform(cargo).Coordinates;
        var heat = cargo.Comp.Heat;
        SurveyWorldPrototype? world = null;
        EntityUid? station = null;

        if (cargo.Comp.Planet is { } planet && TryComp<SurveyDirectorComponent>(planet, out var director))
        {
            _proto.TryIndex(director.World, out world);
            station = director.Station;
        }

        var count = 2 + heat / 2;
        switch (cargo.Comp.Kind)
        {
            case SurveyKind.Beasts when world is { Predators.Count: > 0 }:
                for (var i = 0; i < count; i++)
                {
                    var beast = SpawnCursed(_random.Pick(world.Predators), coords);
                    if (Board(beast) is { } board)
                        board.SetValue(SurveyKeys.Threat, true);
                }
                break;

            case SurveyKind.Raiders or SurveyKind.Pirates when world is { Squads.Count: > 0 }:
                var members = _random.Pick(world.Squads).Members;
                for (var i = 0; i < Math.Min(count, members.Count + 1); i++)
                {
                    var human = SpawnCursed(members[i % members.Count], coords);
                    if (Board(human) is { } board)
                    {
                        board.SetValue(SurveyKeys.Role, SurveyRole.Hold);
                        board.SetValue(SurveyKeys.Confident, true);
                        board.SetValue(SurveyKeys.Bark, "revenge");
                    }
                }
                break;

            case SurveyKind.Factory when world is { Stamp: { } stamp }:
                var printer = SpawnAtPosition(stamp, coords);
                var comp = EnsureComp<SurveyStampComponent>(printer);
                comp.Crowd = world.Crowd;
                comp.Cap = 3 + heat / 2;
                comp.Interval = world.StampInterval * 1.5f;
                comp.NextPrint = _timing.CurTime + TimeSpan.FromSeconds(10);
                break;

            default:
                var source = EnsureComp<RadiationSourceComponent>(cargo);
                source.Intensity = 1.5f + heat * 0.5f;
                Dirty(cargo, source);
                break;
        }

        var text = Loc.GetString($"survey-curse-{cargo.Comp.Kind.ToString().ToLowerInvariant()}", ("name", Name(cargo)));
        if (station is { } home && Exists(home))
            _chat.DispatchStationAnnouncement(home, text, Loc.GetString("survey-sender"), playDefaultSound: true, colorOverride: Color.OrangeRed);
    }

    private EntityUid SpawnCursed(EntProtoId proto, EntityCoordinates near)
    {
        var uid = SpawnAtPosition(proto, near.Offset(new Vector2(_random.NextFloat(-1.5f, 1.5f), _random.NextFloat(-1.5f, 1.5f))));
        RemComp<Content.Server.Ghost.Roles.Components.GhostRoleComponent>(uid);
        RemComp<Content.Server.Ghost.Roles.Components.GhostTakeoverAvailableComponent>(uid);
        RemComp<Content.Shared.SSDIndicator.SSDIndicatorComponent>(uid);
        if (TryComp<SurveyMemberComponent>(uid, out var member) && Board(uid) is { } board && member.Voice.Length > 0)
            board.SetValue(SurveyKeys.Voice, member.Voice);
        return uid;
    }
}
