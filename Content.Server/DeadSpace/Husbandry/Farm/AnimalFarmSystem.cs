// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Farm;

/// <summary>
/// The farm while nobody is around. The AI of an animal sleeps when no player is near (see
/// <c>NPCSystem.UpdateProximitySleep</c>), so it walks neither to the feeder nor to the trough. This feeds and waters
/// such animals from their pen without walking, so that an animal does not starve just because its keeper is away.
///
/// It is built to cost the same however many animals and players there are:
/// <list type="bullet">
/// <item>it runs once per <see cref="CCVars.HusbandryFarmInterval"/> and is off with <see cref="CCVars.HusbandryFarmEnabled"/>;</item>
/// <item>only animals that are hungry or thirsty are looked at, and at most <see cref="CCVars.HusbandryFarmBudget"/> of them per
/// cycle, the rest wait for the next one (the list is kept in order and the cycle goes on from where it stopped);</item>
/// <item>a pen is worked out once for all the animals of an enclosure (see <see cref="AnimalPenSystem"/>);</item>
/// <item>nothing is produced out of thin air: the food and the water are used up for real.</item>
/// </list>
/// Maps that are frozen are skipped by the queries, so nothing happens there, as with plants.
/// </summary>
public sealed class AnimalFarmSystem : EntitySystem
{
    [Dependency] private readonly AnimalFeedingSystem _feeding = default!;
    [Dependency] private readonly AnimalPenSystem _pens = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    private TimeSpan _next;
    private int _cursor;
    private readonly List<EntityUid> _animals = new();
    private readonly HashSet<Entity<AnimalFeederComponent>> _feeders = new();
    private readonly HashSet<Entity<EdibleComponent>> _foods = new();
    private readonly HashSet<Entity<WaterSourceComponent>> _waters = new();

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
        var hungry = needs.Satiety.Value <= needs.Satiety.SeekBelow;
        var thirsty = needs.Hydration.Value <= needs.Hydration.SeekBelow;
        var fed = !hungry || TryEat((animal, diet), pen);
        var watered = !thirsty || TryDrink((animal, needs), pen);

        TryWanderOff((animal, penComp), pen, !fed || !watered);
    }

    /// <summary>
    /// Eats from a feeder of the pen, or else what lies on the floor of it.
    /// </summary>
    private bool TryEat(Entity<DietComponent> animal, AnimalPen pen)
    {
        _pens.GetEntitiesInPen(pen, _feeders);
        foreach (var feeder in _feeders)
        {
            if (_feeding.TryFindFood(animal, feeder, out _) && _feeding.EatNow(animal, feeder, offline: true))
                return true;
        }

        _pens.GetEntitiesInPen(pen, _foods);
        foreach (var food in _foods)
        {
            if (_feeding.CanEat(animal, food) && _feeding.EatNow(animal, food, offline: true))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Drinks from a water source of the pen that has any water in it.
    /// </summary>
    private bool TryDrink(Entity<AnimalNeedsComponent> animal, AnimalPen pen)
    {
        _pens.GetEntitiesInPen(pen, _waters);
        foreach (var water in _waters)
        {
            if (_feeding.CanDrink(water) && _feeding.DrinkNow(animal, water))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Nothing but a square keeps the animal of an open pen, and it may walk off the map: it is gone. Likelier if it
    /// finds nothing to eat or drink. Only if <see cref="CCVars.HusbandryFarmEscape"/> is on, it is off by default.
    /// </summary>
    private void TryWanderOff(Entity<AnimalPenComponent> animal, AnimalPen pen, bool starving)
    {
        if (pen.Kind != PenKind.Open || !_cfg.GetCVar(CCVars.HusbandryFarmEscape))
            return;

        var perHour = starving ? animal.Comp.EscapeChanceStarvingPerHour : animal.Comp.EscapeChancePerHour;
        var hours = Math.Max(1f, _cfg.GetCVar(CCVars.HusbandryFarmInterval)) / 3600f;
        if (perHour <= 0f || !_random.Prob(perHour * hours))
            return;

        Log.Info($"{ToPrettyString(animal)} wandered off the map from its open pen (starving: {starving}).");
        QueueDel(animal);
    }
}
