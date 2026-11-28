// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Stack;
using Content.Shared.DeadSpace.PlanetSurvey;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const float DiscoverRange = 13f;
    private const string Cash = "SpaceCash";

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StackSystem _stack = default!;

    private void AddPoi(Entity<SurveyDirectorComponent> ent, Vector2 pos, SurveyPoiKind kind)
    {
        foreach (var poi in ent.Comp.Pois)
        {
            if (poi.Kind == kind && Vector2.Distance(poi.Pos, pos) < 15f)
                return;
        }

        ent.Comp.Pois.Add(new SurveyPoi { Pos = pos, Kind = kind });
    }

    private void BuildObjectives(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world)
    {
        var director = ent.Comp;
        director.NativesTotal = director.Squads.Sum(s => s.Size);
        director.StampsTotal = director.Stamps.Count;

        var homeKind = world.Kind switch
        {
            SurveyKind.Beasts => SurveyPoiKind.Den,
            SurveyKind.Factory => SurveyPoiKind.Stamp,
            _ => SurveyPoiKind.Settlement,
        };

        foreach (var squad in director.Squads)
        {
            AddPoi(ent, squad.Home.Position, homeKind);
        }

        var sites = 0;
        var dens = 0;
        var query = EntityQueryEnumerator<SurveyResearchSiteComponent, TransformComponent>();
        while (query.MoveNext(out _, out var site, out var xform))
        {
            if (xform.MapUid != ent.Owner)
                continue;

            AddPoi(ent, _xform.GetWorldPosition(xform), SurveyPoiKind.Site);
            if (site.Den)
                dens++;
            else
                sites++;
        }

        Objective(director, "explore", "survey-obj-explore", Math.Min(5, Math.Max(1, director.Pois.Count)), 1500, 200, true);

        if (director.Motive == SurveyMotive.Retrieve)
            Objective(director, "cargo", "survey-obj-retrieve", Math.Max(1, director.Cargo.Count), 0, 300, true);
        else
            Objective(director, "cargo", "survey-obj-cargo", Math.Max(1, director.Cargo.Count), 0, 300, false);

        switch (world.Kind)
        {
            case SurveyKind.Raiders:
                Objective(director, "kind", "survey-obj-raiders", Math.Max(1, (int) MathF.Ceiling(director.NativesTotal * 0.6f)), 3000, 400, false);
                break;
            case SurveyKind.Pirates:
                Objective(director, "kind", "survey-obj-pirates", 1, 2500, 400, false);
                break;
            case SurveyKind.Factory:
                Objective(director, "kind", "survey-obj-stamps", Math.Max(1, director.StampsTotal), 4000, 500, false);
                break;
            case SurveyKind.Beasts when dens > 0:
                Objective(director, "kind", "survey-obj-dens", dens, 2000, 300, false);
                break;
            case SurveyKind.DeadElement:
                Objective(director, "kind", "survey-obj-storm", 1, 2500, 400, false);
                break;
        }

        if (sites > 0)
            Objective(director, "study", "survey-obj-study", sites, 1000, 300, false);

        if (world.FactoryHalls)
            Objective(director, "core", "survey-obj-core", 1, 6000, 800, false);

        if (director.Outposts.Any(o => o.Role == SurveyDeviceRole.Power))
            Objective(director, "power", "survey-obj-power", 1, 1500, 300, false);

        if (director.Outposts.Any(o => o.Role == SurveyDeviceRole.Comms))
            Objective(director, "comms", "survey-obj-comms", 1, 1500, 300, false);

        if (director.Outposts.Any(o => o.Squad != null))
            Objective(director, "stealth", "survey-obj-stealth", 1, 2500, 500, false);
    }

    private static void Objective(SurveyDirectorComponent director, string id, LocId text, int goal, int money, int personal, bool shown)
    {
        director.Objectives.Add(new SurveyObjective
        {
            Id = id,
            Text = text,
            Goal = goal,
            Money = money,
            Personal = personal,
            Shown = shown,
        });
    }

    private SurveyObjective? GetObjective(SurveyDirectorComponent director, string id)
    {
        foreach (var objective in director.Objectives)
        {
            if (objective.Id == id)
                return objective;
        }

        return null;
    }

    private void Reveal(SurveyDirectorComponent director, string id)
    {
        if (GetObjective(director, id) is { } objective)
            objective.Shown = true;
    }

    public void Progress(EntityUid planet, string id, int amount = 1)
    {
        if (TryComp<SurveyDirectorComponent>(planet, out var director) && GetObjective(director, id) is { } objective)
        {
            objective.Shown = true;
            objective.Done += amount;
        }
    }

    private void TickObjectives(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;

        foreach (var poi in director.Pois)
        {
            if (poi.Found || NearestPlayer(poi.Pos, DiscoverRange, out _) == null)
                continue;

            poi.Found = true;
            switch (poi.Kind)
            {
                case SurveyPoiKind.Site:
                    Reveal(director, "study");
                    break;
                case SurveyPoiKind.Shelter:
                    break;
                case SurveyPoiKind.Device:
                    Reveal(director, "power");
                    Reveal(director, "comms");
                    Reveal(director, "core");
                    break;
                default:
                    Reveal(director, "kind");
                    if (!director.AlarmEver)
                        Reveal(director, "stealth");
                    break;
            }
        }

        if (director.AlarmEver && GetObjective(director, "stealth") is { Complete: false } stealth)
            stealth.Shown = false;

        if (GetObjective(director, "explore") is { } explore)
            explore.Done = director.Pois.Count(p => p.Found);

        if (GetObjective(director, "cargo") is { } cargo)
        {
            var aboard = 0;
            foreach (var uid in director.Cargo)
            {
                if (Transform(uid).GridUid is { } grid && director.Shuttles.Contains(grid))
                    aboard++;
            }

            if (director.CargoHolders.Count > 0)
                cargo.Shown = true;
            cargo.Done = Math.Max(cargo.Done, aboard);
        }

        if (GetObjective(director, "kind") is { } kind)
        {
            switch (director.Kind)
            {
                case SurveyKind.Raiders:
                    kind.Done = director.Kills;
                    break;
                case SurveyKind.Pirates:
                    var alive = director.Squads.Sum(s => s.Alive);
                    if (director.Phase != SurveyPhase.Quiet || director.DealPaid)
                        kind.Shown = true;
                    if (director.DealPaid || alive * 3 <= director.NativesTotal)
                        kind.Done = 1;
                    break;
                case SurveyKind.Factory:
                    kind.Done = director.StampsTotal - director.Stamps.Count;
                    break;
            }
        }

        foreach (var objective in director.Objectives)
        {
            if (objective.Complete || objective.Done < objective.Goal)
                continue;

            objective.Complete = true;
            objective.Shown = true;
            Pay(director.Station, objective.Money);
            PayPlayers(objective.Personal);
            Warn(ent.Owner, Loc.GetString("survey-obj-done", ("text", ObjectiveText(objective))));
        }

        PushObjectives(ent);
    }

    private string ObjectiveText(SurveyObjective objective)
    {
        return Loc.GetString(objective.Text, ("done", Math.Min(objective.Done, objective.Goal)), ("goal", objective.Goal));
    }

    private void PushObjectives(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var title = Loc.GetString("survey-ui-title", ("planet", Name(ent.Owner)));
        var threat = Loc.GetString(director.Threat switch
        {
            < 0.9f => "survey-ui-threat-low",
            < 1.5f => "survey-ui-threat-mid",
            _ => "survey-ui-threat-high",
        });

        if (director.Kind == SurveyKind.Pirates && director.Phase == SurveyPhase.Deal)
            threat = Loc.GetString("survey-ui-deal", ("seconds", Math.Max(0, (int) (director.DealEnds - _timing.CurTime).TotalSeconds)));
        else if (director.AlarmAt is { } at)
            threat = Loc.GetString("survey-ui-spotted", ("seconds", Math.Max(0, (int) (at - _timing.CurTime).TotalSeconds)));
        else if (director.Alarm)
            threat = Loc.GetString("survey-ui-alarm");

        if (director.Blackout)
            threat += " · " + Loc.GetString("survey-ui-blackout");
        if (director.CommsCut)
            threat += " · " + Loc.GetString("survey-ui-comms");

        var lines = new List<SurveyObjectiveLine>();
        foreach (var objective in director.Objectives)
        {
            if (objective.Shown)
                lines.Add(new SurveyObjectiveLine(ObjectiveText(objective), objective.Complete));
        }

        var key = threat + string.Join('|', lines.Select(l => l.Text + l.Done));
        var changed = key != director.ObjectivesShown;
        director.ObjectivesShown = key;

        foreach (var (uid, _) in _players)
        {
            var had = HasComp<SurveyObjectivesComponent>(uid);
            if (had && !changed)
                continue;

            var comp = EnsureComp<SurveyObjectivesComponent>(uid);
            comp.Title = title;
            comp.Threat = threat;
            comp.Lines = lines;
            Dirty(uid, comp);
        }
    }

    private void ClearObjectiveViewers()
    {
        var query = EntityQueryEnumerator<SurveyObjectivesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<SurveyDirectorComponent>(map))
                RemCompDeferred<SurveyObjectivesComponent>(uid);
        }
    }

    private void PayPlayers(int amount)
    {
        if (amount <= 0)
            return;

        foreach (var (uid, _) in _players)
        {
            GiveCash(uid, amount);
        }
    }

    private void GiveCash(EntityUid uid, int amount)
    {
        if (amount <= 0 || !HasComp<ActorComponent>(uid))
            return;

        var cash = SpawnAtPosition(Cash, Transform(uid).Coordinates);
        if (TryComp<StackComponent>(cash, out var stack))
            _stack.SetCount((cash, stack), amount);
        _hands.PickupOrDrop(uid, cash);
    }
}
