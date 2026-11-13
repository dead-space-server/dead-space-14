// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
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

    private TimeSpan _next;
    private int _cursor;
    private readonly List<EntityUid> _animals = new();
    private readonly HashSet<Entity<AnimalFeederComponent>> _feeders = new();
    private readonly HashSet<Entity<EdibleComponent>> _foods = new();
    private readonly HashSet<Entity<DrainableSolutionComponent>> _waters = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + TimeSpan.FromSeconds(Math.Max(1f, _cfg.GetCVar(CCVars.HusbandryFarmInterval)));

        if (!_cfg.GetCVar(CCVars.HusbandryFarmEnabled))
            return;

        _animals.Clear();
        var query = EntityQueryEnumerator<AnimalNeedsComponent, DietComponent, AnimalPenComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var needs, out _, out _, out var htn))
        {
            // The cheapest tests first: most animals are not hungry and cost nothing here.
            if (!IsHungryOrThirsty(needs) || !IsAsleepOnTheFarm(uid, htn))
                continue;

            _animals.Add(uid);
        }

        if (_animals.Count == 0)
        {
            _cursor = 0;
            return;
        }

        var budget = Math.Clamp(_cfg.GetCVar(CCVars.HusbandryFarmBudget), 1, _animals.Count);
        if (_cursor >= _animals.Count)
            _cursor = 0;

        for (var i = 0; i < budget; i++)
        {
            var animal = _animals[(_cursor + i) % _animals.Count];
            if (!TerminatingOrDeleted(animal))
                Simulate(animal);
        }

        _cursor = (_cursor + budget) % _animals.Count;
    }

    private static bool IsHungryOrThirsty(AnimalNeedsComponent needs)
    {
        return needs.Satiety.Value <= needs.Satiety.SeekBelow || needs.Hydration.Value <= needs.Hydration.SeekBelow;
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

        // Each need on its own: what it takes, where it takes it from and how much is different.
        if (needs.Satiety.Value <= needs.Satiety.SeekBelow)
            TryEat((animal, diet), pen);

        if (needs.Hydration.Value <= needs.Hydration.SeekBelow)
            TryDrink((animal, diet), pen);
    }

    /// <summary>
    /// Eats from a feeder of the pen, or else what lies on the floor of it, a few bites at most.
    /// </summary>
    private void TryEat(Entity<DietComponent> animal, Content.Shared.DeadSpace.Husbandry.Pen.AnimalPen pen)
    {
        var bites = animal.Comp.OfflineBites;

        _pens.GetEntitiesInPen(pen, _feeders, adjacent: true);
        foreach (var feeder in _feeders)
        {
            while (bites > 0 && _feeding.TryFindFood(animal, feeder, out var item) && _feeding.EatNow(animal, item))
            {
                bites--;
            }

            if (bites <= 0)
                return;
        }

        _pens.GetEntitiesInPen(pen, _foods, adjacent: true);
        foreach (var food in _foods)
        {
            while (bites > 0 && !TerminatingOrDeleted(food) && _feeding.CanEat(animal, food) && _feeding.EatNow(animal, food))
            {
                bites--;
            }

            if (bites <= 0)
                return;
        }
    }

    /// <summary>
    /// Drinks from a container of the pen that has a drink in it, a few sips at most.
    /// </summary>
    private void TryDrink(Entity<DietComponent> animal, Content.Shared.DeadSpace.Husbandry.Pen.AnimalPen pen)
    {
        var sips = animal.Comp.OfflineBites;

        _pens.GetEntitiesInPen(pen, _waters, adjacent: true);
        foreach (var water in _waters)
        {
            while (sips > 0 && _feeding.CanDrink(animal, water) && _feeding.DrinkNow(animal, water))
            {
                sips--;
            }

            if (sips <= 0)
                return;
        }
    }
}
