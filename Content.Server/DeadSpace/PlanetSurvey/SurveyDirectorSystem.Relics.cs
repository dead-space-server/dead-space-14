// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Anomaly.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.PlanetSurvey;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Verbs;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> Burn = "Burn";
    private static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";
    private static readonly TimeSpan RelicTick = TimeSpan.FromSeconds(2);

    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private StationSystem _station = default!;

    private TimeSpan _nextRelicTick;

    private void InitializeRelics()
    {
        SubscribeLocalEvent<SurveyResearchSiteComponent, GetVerbsEvent<InteractionVerb>>(OnStudyVerbs);
        SubscribeLocalEvent<SurveyResearchSiteComponent, SurveyStudyDoAfterEvent>(OnStudied);
        SubscribeLocalEvent<SurveyRelicComponent, UseInHandEvent>(OnRelicUsed);
    }

    private void OnStudyVerbs(Entity<SurveyResearchSiteComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || ent.Comp.Studied)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("survey-verb-study"),
            Act = () => StartStudy(ent, user),
        });
    }

    private bool HasScanner(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<AnomalyScannerComponent>(held) || HasComp<NodeScannerComponent>(held))
                return true;
        }

        return false;
    }

    private void StartStudy(Entity<SurveyResearchSiteComponent> ent, EntityUid user)
    {
        var time = ent.Comp.Time * (HasScanner(user) ? 0.5f : 1f);
        var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time), new SurveyStudyDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("survey-study-start"), ent, user);
    }

    private void OnStudied(Entity<SurveyResearchSiteComponent> ent, ref SurveyStudyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Studied)
            return;

        args.Handled = true;
        ent.Comp.Studied = true;

        var points = (int) (ent.Comp.Points * (HasScanner(args.User) ? 1.5f : 1f));
        var planet = ent.Comp.Planet ?? Transform(ent).MapUid;
        var station = planet is { } p && TryComp<SurveyDirectorComponent>(p, out var director) ? director.Station : null;
        GivePoints(station, points);
        GiveCash(args.User, 300);

        if (TryComp<SurveyRelicComponent>(ent, out var relic) && relic.Effect == SurveyRelicEffect.Hatch)
            relic.HatchAt = null;

        if (planet is { } map)
            Progress(map, ent.Comp.Den ? "kind" : "study");

        _popup.PopupEntity(Loc.GetString("survey-study-done", ("points", points)), ent, args.User, PopupType.Medium);
    }

    private void GivePoints(EntityUid? station, int points)
    {
        EntityUid? fallback = null;
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var uid, out var server))
        {
            fallback ??= uid;
            if (station != null && _station.GetOwningStation(uid) == station)
            {
                _research.ModifyServerPoints(uid, points, server);
                return;
            }
        }

        if (fallback is { } any)
            _research.ModifyServerPoints(any, points);
    }

    private void OnRelicUsed(Entity<SurveyRelicComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Effect != SurveyRelicEffect.Bloom)
            return;

        args.Handled = true;
        var heal = new DamageSpecifier(_proto.Index(Brute), -25);
        heal += new DamageSpecifier(_proto.Index(Burn), -15);
        heal += new DamageSpecifier(_proto.Index(Cellular), 6);
        _damage.TryChangeDamage(args.User, heal, true, interruptsDoAfters: false);
        _popup.PopupEntity(Loc.GetString("survey-relic-bloom"), args.User, args.User, PopupType.Medium);

        ent.Comp.Charges--;
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("survey-relic-bloom-wither"), args.User, args.User);
            QueueDel(ent);
        }
    }

    private void UpdateRelics(TimeSpan now)
    {
        if (now < _nextRelicTick)
            return;

        _nextRelicTick = now + RelicTick;
        var query = EntityQueryEnumerator<SurveyRelicComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var relic, out var xform))
        {
            var holder = Carrier(uid);
            switch (relic.Effect)
            {
                case SurveyRelicEffect.Regen when holder is { } wearer:
                    _damage.TryChangeDamage(wearer, new DamageSpecifier(_proto.Index(Brute), -1.5), true, interruptsDoAfters: false);
                    break;

                case SurveyRelicEffect.Hatch:
                    if (relic.HatchAt == null && holder != null && !(TryComp<SurveyResearchSiteComponent>(uid, out var site) && site.Studied))
                        relic.HatchAt = now + TimeSpan.FromMinutes(relic.HatchMinutes);

                    if (relic.HatchAt is { } hatch && now >= hatch && relic.Spawn is { } spawn)
                    {
                        relic.HatchAt = null;
                        _popup.PopupCoordinates(Loc.GetString("survey-relic-hatch"), xform.Coordinates, PopupType.LargeCaution);
                        SpawnCursed(spawn, xform.Coordinates);
                        QueueDel(uid);
                    }
                    else if (relic.HatchAt is { } soon && soon - now < TimeSpan.FromSeconds(60) && holder is { } nurse)
                    {
                        _popup.PopupEntity(Loc.GetString("survey-relic-egg-kick"), nurse, nurse);
                    }
                    break;

                case SurveyRelicEffect.Idol:
                    if (now < relic.Next)
                        break;

                    relic.Next = now + TimeSpan.FromSeconds(_random.Next(150, 240));
                    if (holder == null && relic.Whispers == 0)
                        break;

                    relic.Whispers++;
                    var target = holder ?? uid;
                    _popup.PopupEntity(Loc.GetString($"survey-relic-whisper-{_random.Next(1, 5)}"), target, Filter.Pvs(target), true, PopupType.MediumCaution);
                    if (relic.Whispers % 3 == 0 && relic.Spawn is { } shade)
                        SpawnCursed(shade, Transform(target).Coordinates);
                    break;
            }
        }
    }

    private void SpawnSites(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        var map = ent.Owner;
        var centre = ent.Comp.Centre;
        var count = world.Sites.Count == 0 ? 0 : world.SiteCount;

        for (var i = 0; i < count; i++)
        {
            var away = centre + Around(random, i, count) * (world.Spread * 1.6f + 15f);
            var spot = OpenSpot(map, away, 2);
            var uid = SpawnAtPosition(world.Sites[random.Next(world.Sites.Count)], new Robust.Shared.Map.EntityCoordinates(map, FreeTile(map, spot, random)));
            if (TryComp<SurveyResearchSiteComponent>(uid, out var site))
                site.Planet = map;
        }

        if (world.Kind == SurveyKind.Beasts && world.DenSign is { } sign)
        {
            foreach (var squad in ent.Comp.Squads)
            {
                if (squad.Members.Count == 0 || TryComp<SurveyMemberComponent>(squad.Members[0], out var member) && member.Prey)
                    continue;

                var uid = SpawnAtPosition(sign, squad.Home);
                if (TryComp<SurveyResearchSiteComponent>(uid, out var site))
                {
                    site.Planet = map;
                    site.Den = true;
                }
            }
        }

        var pool = spots.Count > 0 ? spots : _guarded;
        for (var i = 0; i < world.RelicCount && world.Relics.Count > 0 && pool.Count > 0; i++)
        {
            var spot = pool[random.Next(pool.Count)];
            var uid = SpawnAtPosition(world.Relics[random.Next(world.Relics.Count)], new Robust.Shared.Map.EntityCoordinates(map, FreeTile(map, spot, random)));
            if (TryComp<SurveyRelicComponent>(uid, out var relic) && relic.Effect == SurveyRelicEffect.Idol)
                relic.Spawn = RelicSpawn(world) ?? relic.Spawn;
            if (TryComp<SurveyResearchSiteComponent>(uid, out var site))
                site.Planet = map;
        }
    }

    private static EntProtoId? RelicSpawn(SurveyWorldPrototype world)
    {
        if (world.Predators.Count > 0)
            return world.Predators[0];
        if (world.Squads.Count > 0)
            return world.Squads[0].Members.LastOrDefault();
        return world.Crowd;
    }
}
