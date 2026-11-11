// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Visuals;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Nutrition.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Husbandry.Feeding;

/// <summary>
/// Lets animals eat food that fits their diet and drink from water sources. This replaces the usual eating
/// and drinking for them, it only touches their own satiety and hydration.
/// </summary>
public sealed class AnimalFeedingSystem : EntitySystem
{
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DietComponent, AnimalEatDoAfterEvent>(OnEaten);
        SubscribeLocalEvent<AnimalNeedsComponent, AnimalDrinkDoAfterEvent>(OnDrunk);
        SubscribeLocalEvent<DietComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DietComponent, AnimalHandFeedDoAfterEvent>(OnHandFed);
    }

    /// <summary>
    /// Whether the animal may eat this. Does not check if it is hungry or can reach it.
    /// </summary>
    public bool CanEat(Entity<DietComponent> animal, EntityUid food)
    {
        if (!HasComp<EdibleComponent>(food) || (_container.IsEntityInContainer(food) && !IsInFeeder(food)))
            return false;

        if (!_whitelist.IsWhitelistPassOrNull(animal.Comp.Whitelist, food) ||
            !_whitelist.IsWhitelistFailOrNull(animal.Comp.Blacklist, food))
            return false;

        return GetNutrition(animal, food) > 0f;
    }

    /// <summary>
    /// How much satiety the animal gets out of the food.
    /// </summary>
    public float GetNutrition(Entity<DietComponent> animal, EntityUid food)
    {
        if (!_solutions.TryGetSolution(food, animal.Comp.Solution, out _, out var solution))
            return 0f;

        return solution.Volume.Float() * animal.Comp.NutritionPerUnit;
    }

    /// <summary>
    /// Whether the thing lies in the food container of a feeder.
    /// </summary>
    private bool IsInFeeder(EntityUid thing)
    {
        return _container.TryGetContainingContainer(thing, out var container) &&
               TryComp<AnimalFeederComponent>(container.Owner, out var feeder) &&
               container.ID == feeder.Container;
    }

    /// <summary>
    /// The food the animal would eat from the source: the source itself if it is food, the first thing in it that the
    /// animal can eat if it is a feeder.
    /// </summary>
    public bool TryFindFood(Entity<DietComponent> animal, EntityUid source, out EntityUid food)
    {
        food = default;

        if (TryComp<AnimalFeederComponent>(source, out var feeder))
        {
            if (!_container.TryGetContainer(source, feeder.Container, out var container))
                return false;

            foreach (var item in container.ContainedEntities)
            {
                if (!CanEat(animal, item))
                    continue;

                food = item;
                return true;
            }

            return false;
        }

        if (!CanEat(animal, source))
            return false;

        food = source;
        return true;
    }

    /// <summary>
    /// Whether the source has enough liquid to drink from.
    /// </summary>
    public bool CanDrink(Entity<WaterSourceComponent> source)
    {
        return _solutions.TryGetSolution(source.Owner, source.Comp.Solution, out _, out var solution) &&
               solution.Volume > 0;
    }

    /// <summary>
    /// Starts eating. The animal gets the food once the do-after finishes.
    /// </summary>
    /// <param name="source">The food, or a feeder: then it is the first thing in it that the animal eats, picked
    /// again when it is done, somebody may have taken it by then.</param>
    /// <param name="delay">How long it takes.</param>
    public bool TryEat(EntityUid animal, EntityUid source, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!TryComp<DietComponent>(animal, out var diet) ||
            !HasComp<AnimalNeedsComponent>(animal) ||
            _mobState.IsIncapacitated(animal) ||
            !TryFindFood((animal, diet), source, out var food))
            return false;

        var args = new DoAfterArgs(EntityManager, animal, diet.EatDelay, new AnimalEatDoAfterEvent(), animal, target: source)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        delay = diet.EatDelay;
        _appearance.SetData(animal, AnimalVisuals.Eating, true);

        var started = new AnimalEatStartedEvent(food);
        RaiseLocalEvent(animal, ref started);
        return true;
    }

    /// <summary>
    /// Starts drinking. The animal gets the water once the do-after finishes.
    /// </summary>
    /// <param name="delay">How long it takes.</param>
    public bool TryDrink(EntityUid animal, EntityUid source, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!TryComp<WaterSourceComponent>(source, out var water) ||
            !HasComp<AnimalNeedsComponent>(animal) ||
            _mobState.IsIncapacitated(animal) ||
            !CanDrink((source, water)))
            return false;

        var args = new DoAfterArgs(EntityManager, animal, water.Delay, new AnimalDrinkDoAfterEvent(), animal, target: source)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        delay = water.Delay;
        _appearance.SetData(animal, AnimalVisuals.Eating, true);
        return true;
    }

    // A player feeds the animal with food from the hand.
    private void OnInteractUsing(Entity<DietComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<AnimalNeedsComponent>(ent) || _mobState.IsIncapacitated(ent) || !CanEat(ent, args.Used))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.EatDelay, new AnimalHandFeedDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnHandFed(Entity<DietComponent> ent, ref AnimalHandFeedDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } food || !CanEat(ent, food))
            return;

        args.Handled = true;
        EatNow(ent, food);
    }

    private void OnEaten(Entity<DietComponent> ent, ref AnimalEatDoAfterEvent args)
    {
        _appearance.SetData(ent, AnimalVisuals.Eating, false);

        if (args.Cancelled)
        {
            var interrupted = new AnimalEatInterruptedEvent();
            RaiseLocalEvent(ent, ref interrupted);
            return;
        }

        if (args.Handled || args.Target is not { } source)
            return;

        args.Handled = true;
        EatNow(ent, source);
    }

    /// <summary>
    /// The animal eats at once: the food, or the first thing it eats in a feeder, is gone and its satiety goes up.
    /// Done when the do-after of eating ends,.
    /// </summary>
    /// <returns>False if there was nothing it could eat.</returns>
    public bool EatNow(Entity<DietComponent> animal, EntityUid source)
    {
        if (!TryComp<AnimalNeedsComponent>(animal, out var needs) || !TryFindFood(animal, source, out var food))
            return false;

        var nutrition = GetNutrition(animal, food);
        _needs.ModifySatiety((animal.Owner, needs), nutrition);
        QueueDel(food);

        var ev = new AnimalAteEvent(food, nutrition);
        RaiseLocalEvent(animal, ref ev);
        return true;
    }

    private void OnDrunk(Entity<AnimalNeedsComponent> ent, ref AnimalDrinkDoAfterEvent args)
    {
        _appearance.SetData(ent, AnimalVisuals.Eating, false);

        if (args.Cancelled || args.Handled || args.Target is not { } source)
            return;

        args.Handled = true;
        DrinkNow(ent, source);
    }

    /// <summary>
    /// The animal drinks at once: the water is taken from the source (really, the liquid goes down) and its hydration
    /// goes up. Done when the do-after of drinking ends,.
    /// </summary>
    /// <returns>False if the source has no water.</returns>
    public bool DrinkNow(Entity<AnimalNeedsComponent> animal, EntityUid source)
    {
        if (!TryComp<WaterSourceComponent>(source, out var water) ||
            !_solutions.TryGetSolution(source, water.Solution, out var soln, out _))
            return false;

        var taken = _solutions.SplitSolution(soln.Value, FixedPoint2.New(water.AmountPerDrink)).Volume.Float();
        if (taken <= 0f)
            return false;

        _needs.ModifyHydration(animal, taken * water.HydrationPerUnit);

        var ev = new AnimalDrankEvent(source, taken);
        RaiseLocalEvent(animal, ref ev);
        return true;
    }
}
