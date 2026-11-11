// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.DeadSpace.Husbandry.Growth;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.DeadSpace.Husbandry.Growth;
using Content.Server.Popups;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Husbandry.Breeding;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Breeding;

/// <summary>
/// Breeding of animals, see <see cref="AnimalBreedingComponent"/>. Cheap on purpose: a female looks around only once
/// in a minute or two, a lookup is done only if she is able to breed at all, and the number of animals is capped for
/// the area and for the whole server.
/// </summary>
public sealed class AnimalBreedingSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly GrowthSystem _growth = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly AnimalPenSystem _pens = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<AnimalBreedingComponent>> _nearby = new();
    private readonly List<EntityUid> _births = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalBreedingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnimalBreedingComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMapInit(Entity<AnimalBreedingComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextAttempt = _timing.CurTime + RandomInterval(ent.Comp);
    }

    // A player took the animal over: no more pregnancy.
    private void OnMindAdded(Entity<AnimalBreedingComponent> ent, ref MindAddedMessage args)
    {
        ent.Comp.Gestating = false;
        ent.Comp.GestationEndTime = null;
    }

    private TimeSpan RandomInterval(AnimalBreedingComponent comp)
    {
        return _random.Next(comp.MinAttemptInterval, comp.MaxAttemptInterval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(CCVars.HusbandryBreedingEnabled))
            return;

        var now = _timing.CurTime;
        _births.Clear();

        var query = EntityQueryEnumerator<AnimalBreedingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Gestating)
            {
                if (comp.GestationEndTime is { } end && now >= end)
                    _births.Add(uid);

                continue;
            }

            if (now < comp.NextAttempt)
                continue;

            comp.NextAttempt = now + RandomInterval(comp);
            TryStartPregnancy((uid, comp));
        }

        foreach (var uid in _births)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<AnimalBreedingComponent>(uid, out var comp))
                GiveBirth((uid, comp));
        }
    }

    /// <summary>
    /// Whether this animal alone is able to breed now: alive, nobody plays it, past its cooldown, grown up, fed and
    /// watered.
    /// </summary>
    public bool IsReady(Entity<AnimalBreedingComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Gestating || _timing.CurTime < comp.NextAllowed)
            return false;

        if (!_mobState.IsAlive(ent) || HasComp<ActorComponent>(ent))
            return false;

        if (TryComp<MindContainerComponent>(ent, out var mind) && mind.HasMind)
            return false;

        if (!_growth.IsGrownUp(ent.Owner, comp.AdultStage))
            return false;

        if (TryComp<AnimalNeedsComponent>(ent, out var needs))
        {
            var animal = (ent.Owner, needs);
            if (!_needs.IsSatietyAtLeast(animal, comp.MinSatiety) || !_needs.IsHydrationAtLeast(animal, comp.MinHydration))
                return false;
        }

        return true;
    }

    private static AnimalSexKind SexOf(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent<AnimalSexComponent>(uid, out var sex) ? sex.Sex : AnimalSexKind.Female;
    }

    private bool TryStartPregnancy(Entity<AnimalBreedingComponent> mother)
    {
        // Only the female looks for a partner, so each pair is looked at once.
        if (SexOf(EntityManager, mother) != AnimalSexKind.Female || !IsReady(mother))
            return false;

        var comp = mother.Comp;
        // The partner has to be in the same pen: the whole enclosure, or the square round the home. Nobody has to
        // watch, and a male on the other side of the room counts. An animal with no pen falls back to the radius.
        var pen = TryComp<AnimalPenComponent>(mother, out var penComp) ? _pens.GetPen((mother.Owner, penComp)) : null;
        if (pen != null)
        {
            _pens.GetEntitiesInPen(pen, _nearby);
        }
        else
        {
            _nearby.Clear();
            _lookup.GetEntitiesInRange(Transform(mother).Coordinates, Math.Max(comp.BreedRange, comp.CrowdRange), _nearby);
        }

        var crowd = 0;
        Entity<AnimalBreedingComponent>? father = null;
        var motherPos = _transform.GetWorldPosition(mother);

        foreach (var other in _nearby)
        {
            if (other.Owner == mother.Owner || other.Comp.Species != comp.Species)
                continue;

            crowd++;

            if (father != null ||
                SexOf(EntityManager, other) != AnimalSexKind.Male ||
                (pen == null && Vector2.Distance(_transform.GetWorldPosition(other), motherPos) > comp.BreedRange) ||
                !IsValidFather(mother, other) ||
                !IsReady(other))
                continue;

            father = other;
        }

        if (father is not { } dad)
            return false;

        if (!BreedingRules.HasRoom(crowd, comp.Capacity, Count<AnimalBreedingComponent>(), _cfg.GetCVar(CCVars.HusbandryPopulationCap)))
            return false;

        if (!_random.Prob(comp.BreedChance))
            return false;

        var attempt = new AnimalBreedingAttemptEvent(dad);
        RaiseLocalEvent(mother, ref attempt);
        if (attempt.Cancelled)
            return false;

        PayFor(mother);
        PayFor(dad);

        comp.Gestating = true;
        comp.GestationEndTime = _timing.CurTime + comp.GestationDuration;

        var started = new AnimalBreedingStartedEvent(dad);
        RaiseLocalEvent(mother, ref started);
        _adminLog.Add(LogType.Action, $"{ToPrettyString(mother)} (mother) and {ToPrettyString(dad)} (father) bred.");
        return true;
    }

    private bool IsValidFather(Entity<AnimalBreedingComponent> mother, EntityUid father)
    {
        return mother.Comp.PartnerWhitelist == null || _whitelist.IsWhitelistPass(mother.Comp.PartnerWhitelist, father);
    }

    private void PayFor(Entity<AnimalBreedingComponent> animal)
    {
        if (TryComp<AnimalNeedsComponent>(animal, out var needs))
            _needs.ModifySatiety((animal.Owner, needs), -animal.Comp.SatietyCost);
    }

    private void GiveBirth(Entity<AnimalBreedingComponent> mother)
    {
        var comp = mother.Comp;

        if (!_mobState.IsAlive(mother))
        {
            comp.Gestating = false;
            comp.GestationEndTime = null;
            return;
        }

        var before = new AnimalBeforeBirthEvent();
        RaiseLocalEvent(mother, ref before);
        if (before.Cancelled)
        {
            // Try again a bit later, not every tick.
            comp.GestationEndTime = _timing.CurTime + comp.MinAttemptInterval;
            return;
        }

        var spawns = EntitySpawnCollection.GetSpawns(comp.Offspring, _random);
        var allowed = BreedingRules.LitterSize(spawns.Count, Count<AnimalBreedingComponent>(), _cfg.GetCVar(CCVars.HusbandryPopulationCap));
        var coordinates = Transform(mother).Coordinates;

        for (var i = 0; i < allowed; i++)
        {
            var baby = Spawn(spawns[i], coordinates.Offset(_random.NextVector2(0.3f)));
            _transform.AttachToGridOrMap(baby);

            if (TryComp<GrowthComponent>(baby, out var growth))
                _growth.SetStageById((baby, growth), comp.NewbornStage, first: true);

            _adminLog.Add(LogType.Action, $"{ToPrettyString(mother)} gave birth to {ToPrettyString(baby)}.");
        }

        _popup.PopupEntity(Loc.GetString(comp.BirthPopup, ("parent", Identity.Entity(mother, EntityManager))), mother);

        comp.Gestating = false;
        comp.GestationEndTime = null;
        comp.NextAllowed = _timing.CurTime + comp.Cooldown;

        var born = new AnimalBornEvent(allowed);
        RaiseLocalEvent(mother, ref born);
    }
}
