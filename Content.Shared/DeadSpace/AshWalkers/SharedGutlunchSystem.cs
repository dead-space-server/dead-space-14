using System.Diagnostics.CodeAnalysis;
using Content.Shared.Animals;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.AshWalkers;

public abstract class SharedGutlunchSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GutlunchComponent, AttemptIngestEvent>(OnAttemptIngest);
        SubscribeLocalEvent<GutlunchComponent, GutlunchFeedDoAfterEvent>(OnFeedDoAfter);
        SubscribeLocalEvent<GutlunchComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<GutlunchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GutlunchComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GutlunchComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<GutlunchComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    protected virtual void OnMobStateChanged(Entity<GutlunchComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnSolutionChanged(Entity<GutlunchComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (TryComp<UdderComponent>(ent, out var udder) && args.SolutionId == udder.SolutionName)
            UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<GutlunchComponent> ent)
    {
        var full = _mobState.IsAlive(ent) && TryGetMilk(ent, out var milk) &&
                   milk.Value.Comp.Solution.Volume > 0 && milk.Value.Comp.Solution.AvailableVolume == 0;
        _appearance.SetData(ent, GutlunchVisuals.Full, full);
    }

    protected virtual void OnExamined(Entity<GutlunchComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_mobState.IsAlive(ent) || !TryGetMilk(ent, out var milk))
            return;

        args.PushMarkup(Loc.GetString("gutlunch-examine-milk",
            ("amount", milk.Value.Comp.Solution.Volume), ("capacity", milk.Value.Comp.Solution.MaxVolume)));
    }

    public bool TryGetMilk(EntityUid uid, [NotNullWhen(true)] out Entity<SolutionComponent>? milk)
    {
        milk = null;
        return TryComp<UdderComponent>(uid, out var udder) &&
               _solutions.TryGetSolution(uid, udder.SolutionName, out milk);
    }

    private bool TryGetFood(Entity<GutlunchComponent> ent, EntityUid food, EntityUid user,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = null;
        return !TerminatingOrDeleted(food) && !HasComp<StackComponent>(food) &&
               !HasComp<MobStateComponent>(food) && HasComp<EdibleComponent>(food) &&
               _whitelist.IsWhitelistPass(ent.Comp.FoodWhitelist, food) &&
               _ingestion.CanAccessSolution(food, user, out solution, out _) &&
               solution.Value.Comp.Solution.GetTotalPrototypeQuantity(ent.Comp.FoodReagent) > 0;
    }

    private bool TryGetFeed(Entity<GutlunchComponent> ent, EntityUid food, EntityUid user,
        [NotNullWhen(true)] out Entity<SolutionComponent>? milk,
        [NotNullWhen(true)] out Entity<SolutionComponent>? foodSolution)
    {
        milk = null;
        foodSolution = null;
        return _mobState.IsAlive(ent) && TryGetMilk(ent, out milk) &&
               milk.Value.Comp.Solution.AvailableVolume > 0 && TryGetFood(ent, food, user, out foodSolution);
    }

    private bool CanReachFood(EntityUid animal, EntityUid food, EntityUid user)
    {
        return _interaction.InRangeAndAccessible(user, animal) &&
               _interaction.InRangeAndAccessible(user, food) &&
               _interaction.InRangeUnobstructed(animal, food);
    }

    private void OnAttemptIngest(Entity<GutlunchComponent> ent, ref AttemptIngestEvent args)
    {
        if (args.Handled || !TryGetFeed(ent, args.Ingested, args.User, out _, out _))
            return;

        if (!args.Ingest)
        {
            args.Handled = true;
            return;
        }

        if (!CanReachFood(ent, args.Ingested, args.User))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.FeedDelay,
            new GutlunchFeedDoAfterEvent(), ent, ent, args.Ingested)
        {
            NeedHand = args.User != ent.Owner,
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnFeedDoAfter(Entity<GutlunchComponent> ent, ref GutlunchFeedDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used is not { } food ||
            !CanReachFood(ent, food, args.User) ||
            !TryGetFeed(ent, food, args.User, out var milk, out var foodSolution))
            return;

        args.Handled = true;
        var nutrients = foodSolution.Value.Comp.Solution.GetTotalPrototypeQuantity(ent.Comp.FoodReagent);
        var produced = ent.Comp.MilkPerNutrient.Clone();
        produced.ScaleSolution(nutrients);
        var consumed = _solutions.SplitSolution(foodSolution.Value, foodSolution.Value.Comp.Solution.Volume);
        produced = produced.SplitSolution(FixedPoint2.Min(produced.Volume, milk.Value.Comp.Solution.AvailableVolume));
        _solutions.TryAddSolution(milk.Value, produced);

        var ingested = new IngestedEvent(args.User, ent, consumed, args.User != ent.Owner);
        RaiseLocalEvent(food, ref ingested);
        if (!ingested.Destroy)
            return;

        var attempt = new DestructionAttemptEvent();
        RaiseLocalEvent(food, attempt);
        if (attempt.Cancelled)
            return;

        var eaten = new FullyEatenEvent(args.User);
        RaiseLocalEvent(food, ref eaten);
        RaiseLocalEvent(food, new DestructionEventArgs());
        PredictedQueueDel(food);
    }
}
