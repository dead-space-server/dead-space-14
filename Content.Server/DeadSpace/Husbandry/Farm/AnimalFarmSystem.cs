// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Prometheus;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Farm;

/// <summary>
/// The farm while nobody is around. The AI of an animal sleeps when no player is near (see
/// <c>NPCSystem.UpdateProximitySleep</c>), so it walks neither to the feeder nor to the trough. This feeds and waters
/// such animals from their pen without walking, so that an animal does not starve just because its keeper is away.
/// What it takes is the same that a watched animal takes (the food that the animal eats, only what it drinks), it
/// goes into the stomach the same way, bite by bite, and the food and the water are used up for real: nothing is
/// made out of thin air, and nothing here makes an animal breed or leave its pen.
///
/// It is built to cost the same however many animals and players there are:
/// <list type="bullet">
/// <item>it runs once per <see cref="CCVars.HusbandryFarmInterval"/> and is off with <see cref="CCVars.HusbandryFarmEnabled"/>;</item>
/// <item>only animals that are hungry or thirsty are looked at, and at most <see cref="CCVars.HusbandryFarmBudget"/> of
/// them per cycle, the rest wait for the next one (the cycle goes on from where it stopped);</item>
/// <item>a pen is worked out once for all the animals of an enclosure (see <see cref="AnimalPenSystem"/>).</item>
/// </list>
/// Maps that are frozen are skipped by the queries, so nothing happens there, as with plants.
/// </summary>
public sealed class AnimalFarmSystem : EntitySystem
{
    [Dependency] private readonly AnimalFeedingSystem _feeding = default!;
    [Dependency] private readonly AnimalPenSystem _pens = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;

    private static readonly Gauge CycleMsGauge = Metrics.CreateGauge(
        "husbandry_farm_cycle_ms",
        "Time the last cycle of the animal farm took, in milliseconds");

    private static readonly Gauge ProcessedGauge = Metrics.CreateGauge(
        "husbandry_farm_animals_processed",
        "Animals the last cycle of the animal farm looked at");

    private static readonly Gauge WaitingGauge = Metrics.CreateGauge(
        "husbandry_farm_animals_waiting",
        "Hungry or thirsty animals with a sleeping AI at the last cycle of the animal farm");

    private TimeSpan _next;
    private int _cursor;
    private readonly List<EntityUid> _animals = new();
    private readonly System.Diagnostics.Stopwatch _watch = new();

    // What lies in a pen, looked up once per cycle for all the animals of that pen.
    private readonly Dictionary<AnimalPen, PenView> _views = new();
    private readonly Stack<PenView> _spareViews = new();
    private readonly HashSet<Entity<AnimalFeederComponent>> _feederSet = new();
    private readonly HashSet<Entity<EdibleComponent>> _foodSet = new();
    private readonly HashSet<Entity<DrainableSolutionComponent>> _waterSet = new();

    private sealed class PenView
    {
        public readonly List<Entity<AnimalFeederComponent>> Feeders = new();
        public readonly List<Entity<EdibleComponent>> Foods = new();
        public readonly List<Entity<DrainableSolutionComponent>> Waters = new();
        public bool FeedersKnown;
        public bool FoodsKnown;
        public bool WatersKnown;

        public void Reset()
        {
            Feeders.Clear();
            Foods.Clear();
            Waters.Clear();
            FeedersKnown = FoodsKnown = WatersKnown = false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + TimeSpan.FromSeconds(Math.Max(1f, _cfg.GetCVar(CCVars.HusbandryFarmInterval)));

        if (!_cfg.GetCVar(CCVars.HusbandryFarmEnabled))
            return;

        _watch.Restart();
        _animals.Clear();
        var query = EntityQueryEnumerator<AnimalNeedsComponent, DietComponent, AnimalPenComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var needs, out _, out _, out var htn))
        {
            // The cheapest tests first: most animals are not hungry and cost nothing here.
            if (!WantsToSearch((uid, needs)) || !IsAsleepOnTheFarm(uid, htn))
                continue;

            _animals.Add(uid);
        }

        WaitingGauge.Set(_animals.Count);

        if (_animals.Count == 0)
        {
            _cursor = 0;
            ProcessedGauge.Set(0);
            CycleMsGauge.Set(_watch.Elapsed.TotalMilliseconds);
            return;
        }

        var budget = Math.Clamp(_cfg.GetCVar(CCVars.HusbandryFarmBudget), 1, _animals.Count);
        if (_cursor >= _animals.Count)
            _cursor = 0;

        var done = 0;
        for (; done < budget; done++)
        {
            var animal = _animals[(_cursor + done) % _animals.Count];
            if (!TerminatingOrDeleted(animal))
                Simulate(animal);
        }

        _cursor = (_cursor + done) % _animals.Count;

        foreach (var view in _views.Values)
        {
            view.Reset();
            _spareViews.Push(view);
        }

        _views.Clear();

        ProcessedGauge.Set(done);
        CycleMsGauge.Set(_watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// The animal is hungry (thirsty) and has not looked for it lately: a search that found nothing, or a meal, is
    /// put off with <see cref="SharedAnimalNeedsSystem.DelaySearch"/>, so an empty pen costs nothing for a while.
    /// </summary>
    private bool WantsToSearch(Entity<AnimalNeedsComponent> animal)
    {
        var needs = animal.Comp;
        return needs.Satiety.Value <= needs.Satiety.SeekBelow && _needs.CanSearch(animal, true) ||
               needs.Hydration.Value <= needs.Hydration.SeekBelow && _needs.CanSearch(animal, false);
    }

    private PenView ViewOf(AnimalPen pen)
    {
        if (_views.TryGetValue(pen, out var view))
            return view;

        view = _spareViews.Count > 0 ? _spareViews.Pop() : new PenView();
        _views[pen] = view;
        return view;
    }

    /// <summary>
    /// The animal is left alone: its AI sleeps, it is alive and nobody plays it.
    /// </summary>
    private bool IsAsleepOnTheFarm(EntityUid animal, HTNComponent htn)
    {
        if (_npc.IsAwake(animal, htn) || HasComp<ActorComponent>(animal) || !_mobState.IsAlive(animal))
            return false;

        return !(TryComp<MindContainerComponent>(animal, out var mind) && mind.HasMind);
    }

    private void Simulate(EntityUid animal)
    {
        if (!TryComp<AnimalNeedsComponent>(animal, out var needs) ||
            !TryComp<DietComponent>(animal, out var diet) ||
            !TryComp<AnimalPenComponent>(animal, out var penComp) ||
            _pens.GetPen((animal, penComp)) is not { } pen)
            return;

        var view = ViewOf(pen);

        // Each need on its own: what it takes, where it takes it from and how much is different. An animal that
        // found nothing does not look again for a while (an empty pen is remembered); one that ate goes on at the
        // next cycle, until it is full, so the animals of a pen eat together.
        if (needs.Satiety.Value <= needs.Satiety.SeekBelow && _needs.CanSearch((animal, needs), true) &&
            !TryEat((animal, diet), pen, view))
        {
            _needs.DelaySearch((animal, needs), true);
        }

        if (needs.Hydration.Value <= needs.Hydration.SeekBelow && _needs.CanSearch((animal, needs), false) &&
            !TryDrink((animal, diet), pen, view))
        {
            _needs.DelaySearch((animal, needs), false);
        }
    }

    /// <summary>
    /// Eats from a feeder of the pen, or else what lies on the floor of it, a few bites at most.
    /// </summary>
    private bool TryEat(Entity<DietComponent> animal, AnimalPen pen, PenView view)
    {
        var bites = animal.Comp.OfflineBites;

        if (!view.FeedersKnown)
        {
            _pens.GetEntitiesInPen(pen, _feederSet, adjacent: true);
            view.Feeders.AddRange(_feederSet);
            view.FeedersKnown = true;
        }

        foreach (var feeder in view.Feeders)
        {
            while (bites > 0 && !TerminatingOrDeleted(feeder) && _feeding.TryFindFood(animal, feeder, out var item) && _feeding.EatNow(animal, item))
            {
                bites--;
            }

            if (bites <= 0)
                return true;
        }

        if (!view.FoodsKnown)
        {
            _pens.GetEntitiesInPen(pen, _foodSet, adjacent: true);
            view.Foods.AddRange(_foodSet);
            view.FoodsKnown = true;
        }

        foreach (var food in view.Foods)
        {
            while (bites > 0 && !TerminatingOrDeleted(food) && _feeding.CanEat(animal, food) && _feeding.EatNow(animal, food))
            {
                bites--;
            }

            if (bites <= 0)
                return true;
        }

        return bites < animal.Comp.OfflineBites;
    }

    /// <summary>
    /// Drinks from a container of the pen that has a drink in it, a few sips at most.
    /// </summary>
    private bool TryDrink(Entity<DietComponent> animal, AnimalPen pen, PenView view)
    {
        var sips = animal.Comp.OfflineBites;

        if (!view.WatersKnown)
        {
            _pens.GetEntitiesInPen(pen, _waterSet, adjacent: true);
            view.Waters.AddRange(_waterSet);
            view.WatersKnown = true;
        }

        foreach (var water in view.Waters)
        {
            while (sips > 0 && !TerminatingOrDeleted(water) && _feeding.CanDrink(animal, water) && _feeding.DrinkNow(animal, water))
            {
                sips--;
            }

            if (sips <= 0)
                return true;
        }

        return sips < animal.Comp.OfflineBites;
    }
}
