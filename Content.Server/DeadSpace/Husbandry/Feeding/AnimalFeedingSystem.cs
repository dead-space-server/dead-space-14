// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Husbandry.Feeding;

/// <summary>
/// Lets animals eat and drink the way anybody does: through the stomach.
/// <list type="bullet">
/// <item>Food is eaten by the usual <see cref="IngestionSystem"/>: bite after bite goes into the stomach, the
/// stomach digests it and its reagents do what they do (see <see cref="AnimalSatiateEffectSystem"/>).</item>
/// <item>A drink comes from any open container with a liquid in it (a trough, a bucket, a bowl): a sip is taken from
/// its solution into the stomach. Whatever the liquid is, it is drunk, and it works on the animal like on a
/// human: the animal does not know acid from water.</item>
/// </list>
/// This class only picks what the animal can eat or drink and starts the action. How full the stomach is
/// is what stops the animal, and the hunger and the thirst go down as the stomach digests.
/// </summary>
public sealed class AnimalFeedingSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalNeedsComponent, AnimalDrinkDoAfterEvent>(OnDrunk);
    }

    /// <summary>
    /// After a meal or a drink the animal does not look for the same again for a while: the stomach has to digest it
    /// before the hunger or the thirst goes down, and the animal must not go round the area searching meanwhile.
    /// </summary>
    public void DelaySearch(EntityUid animal, bool food)
    {
        if (TryComp<AnimalNeedsComponent>(animal, out var needs))
            _needs.DelaySearch((animal, needs), food);
    }

    #region Eating

    /// <summary>
    /// Whether the animal may eat this. Does not check if it is hungry or can reach it.
    /// </summary>
    public bool CanEat(Entity<DietComponent> animal, EntityUid food)
    {
        if (!TryComp<EdibleComponent>(food, out var edible) ||
            _ingestion.GetEdibleType((food, edible)) != IngestionSystem.Food ||
            (_container.IsEntityInContainer(food) && !IsInFeeder(food)))
            return false;

        if (!_whitelist.IsWhitelistPassOrNull(animal.Comp.Whitelist, food) ||
            !_whitelist.IsWhitelistFailOrNull(animal.Comp.Blacklist, food))
            return false;

        // What the stomach of the animal can digest: a ruminant has a stomach for grain and grass only.
        return !_ingestion.IsEmpty((food, edible)) && _ingestion.CanIngest(animal, food);
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
    /// Starts eating: the usual eating of anything edible, bite after bite into the stomach until the stomach is
    /// full or the food is gone. The animal goes on with the thing it began (<see cref="DietComponent.Eating"/>) and
    /// takes the next one from a feeder only when that is eaten up: it does not spill the feeder one item at a time.
    /// Food in a feeder is eaten where it lies if that works, and otherwise taken out of it first.
    /// </summary>
    /// <param name="delay">How long one bite takes.</param>
    public bool TryEat(EntityUid animal, EntityUid source, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!TryComp<DietComponent>(animal, out var diet) ||
            !HasComp<AnimalNeedsComponent>(animal) ||
            _mobState.IsIncapacitated(animal))
            return false;

        // What it began, if there is something left of it.
        EntityUid food;
        if (diet.Eating is { } eating &&
            !TerminatingOrDeleted(eating) &&
            Transform(eating).Coordinates.TryDistance(EntityManager, Transform(animal).Coordinates, out var nearby) &&
            nearby <= 1.5f &&
            CanEat((animal, diet), eating))
        {
            food = eating;
        }
        else if (!TryFindFood((animal, diet), source, out food))
        {
            diet.Eating = null;
            return false;
        }

        if (!_ingestion.TryIngest(animal, food))
        {
            // It cannot be eaten in the feeder: it is taken out and eaten on the floor next to it.
            if (!_container.IsEntityInContainer(food) || !_container.TryRemoveFromContainer(food) || !_ingestion.TryIngest(animal, food))
            {
                diet.Eating = null;
                return false;
            }
        }

        diet.Eating = food;
        delay = TryComp<EdibleComponent>(food, out var edible) ? edible.Delay : TimeSpan.FromSeconds(1);
        return true;
    }

    /// <summary>
    /// The animal takes a bite at once, without walking and without the do-after: what the usual eating would put into
    /// the stomach with one bite goes into it, and the food is used up the same way. For the farm, which looks after
    /// animals that nobody watches.
    /// </summary>
    /// <returns>False if the animal cannot eat it or the stomach is full.</returns>
    public bool EatNow(Entity<DietComponent> animal, EntityUid food)
    {
        if (!CanEat(animal, food) ||
            !TryComp<EdibleComponent>(food, out var edible) ||
            !TryGetStomach(animal, out var stomach) ||
            !_solutions.TryGetSolution(food, edible.Solution, out var soln, out var sol) ||
            !_solutions.ResolveSolution(stomach.Owner, StomachSystem.DefaultSolutionName, ref stomach.Comp1.Solution, out var stomachSolution))
            return false;

        var bite = edible.TransferAmount ?? sol.Volume;
        var amount = FixedPoint2.Min(bite, FixedPoint2.Min(sol.Volume, stomachSolution.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return false;

        var taken = _solutions.SplitSolution(soln.Value, amount);
        if (!_stomach.TryTransferSolution(stomach.Owner, taken, stomach.Comp1))
        {
            _solutions.TryAddSolution(soln.Value, taken);
            return false;
        }

        if (_ingestion.IsEmpty((food, edible)))
        {
            _ingestion.SpawnTrash((food, edible));
            QueueDel(food);
        }

        return true;
    }

    /// <summary>
    /// The meal is over: the animal does not hold on to the thing it was eating.
    /// </summary>
    public void StopEating(EntityUid animal)
    {
        if (TryComp<DietComponent>(animal, out var diet))
            diet.Eating = null;
    }

    /// <summary>
    /// Whether the animal is in the middle of an action (a bite, a sip).
    /// </summary>
    public bool IsBusy(EntityUid animal)
    {
        if (!TryComp<DoAfterComponent>(animal, out var doAfters))
            return false;

        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (!doAfter.Cancelled && !doAfter.Completed)
                return true;
        }

        return false;
    }

    #endregion

    #region Drinking

    /// <summary>
    /// The solution that the animal would drink from the container: any open container that has a drainable
    /// solution (or says which one it is) with only drinks in it.
    /// </summary>
    public bool TryGetDrinkSolution(Entity<DietComponent> animal, EntityUid source, out Entity<SolutionComponent> solution)
    {
        solution = default;

        string? name = null;
        if (TryComp<WaterSourceComponent>(source, out var water))
            name = water.Solution;

        if (name == null && TryComp<DrainableSolutionComponent>(source, out var drainable))
            name = drainable.Solution;

        if (name == null)
            return false;

        // A closed bottle is not a drink for an animal.
        if (TryComp<OpenableComponent>(source, out var openable) && !openable.Opened)
            return false;

        if (!_solutions.TryGetSolution(source, name, out var soln, out var sol) || sol.Volume <= FixedPoint2.Zero)
            return false;

        // Only what an animal drinks: everything in the container has to be on the list.
        foreach (var (reagent, _) in sol.Contents)
        {
            if (!animal.Comp.DrinkReagents.Contains(reagent.Prototype))
                return false;
        }

        solution = soln.Value;
        return true;
    }

    /// <summary>
    /// Whether there is something in the container that the animal drinks.
    /// </summary>
    public bool CanDrink(Entity<DietComponent> animal, EntityUid source)
    {
        return TryGetDrinkSolution(animal, source, out _);
    }

    /// <summary>
    /// Starts drinking. A sip goes into the stomach once the do-after finishes.
    /// </summary>
    /// <param name="delay">How long it takes.</param>
    public bool TryDrink(EntityUid animal, EntityUid source, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!HasComp<AnimalNeedsComponent>(animal) ||
            !TryComp<DietComponent>(animal, out var diet) ||
            _mobState.IsIncapacitated(animal) ||
            !CanDrink((animal, diet), source) ||
            !TryGetStomach(animal, out _))
            return false;

        delay = TryComp<WaterSourceComponent>(source, out var water) ? water.Delay : diet.DrinkTime;

        var args = new DoAfterArgs(EntityManager, animal, delay, new AnimalDrinkDoAfterEvent(), animal, target: source)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            DistanceThreshold = 2f,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    private void OnDrunk(Entity<AnimalNeedsComponent> ent, ref AnimalDrinkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } source)
            return;

        args.Handled = true;
        DrinkNow(ent, source);
    }

    /// <summary>
    /// The animal takes a sip at once: a part of the solution of the container goes into the stomach (it is a drink: see <see cref="DietComponent.DrinkReagents"/>).
    /// </summary>
    /// <returns>False if there was nothing to drink or the stomach is full.</returns>
    public bool DrinkNow(EntityUid animal, EntityUid source)
    {
        if (!TryComp<DietComponent>(animal, out var diet) ||
            !TryGetStomach(animal, out var stomach) ||
            !TryGetDrinkSolution((animal, diet), source, out var soln) ||
            !_solutions.ResolveSolution(stomach.Owner, StomachSystem.DefaultSolutionName, ref stomach.Comp1.Solution, out var stomachSolution))
            return false;

        var sip = TryComp<WaterSourceComponent>(source, out var water) ? water.AmountPerDrink : diet.SipAmount;
        var amount = FixedPoint2.Min(FixedPoint2.New(sip), FixedPoint2.Min(soln.Comp.Solution.Volume, stomachSolution.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return false;

        var taken = _solutions.SplitSolution(soln, amount);
        if (_stomach.TryTransferSolution(stomach.Owner, taken, stomach.Comp1))
            return true;

        // It does not fit after all: put it back.
        _solutions.TryAddSolution(soln, taken);
        return false;
    }

    /// <summary>
    /// The stomach of the animal that has the most room.
    /// </summary>
    private bool TryGetStomach(EntityUid animal, out Entity<StomachComponent, OrganComponent> stomach)
    {
        stomach = default;
        if (!_body.TryGetBodyOrganEntityComps<StomachComponent>(animal, out var stomachs))
            return false;

        var room = FixedPoint2.Zero;
        var found = false;
        foreach (var candidate in stomachs)
        {
            if (!_solutions.ResolveSolution(candidate.Owner, StomachSystem.DefaultSolutionName, ref candidate.Comp1.Solution, out var sol) ||
                sol.AvailableVolume < room)
                continue;

            room = sol.AvailableVolume;
            stomach = candidate;
            found = true;
        }

        return found;
    }

    #endregion
}
