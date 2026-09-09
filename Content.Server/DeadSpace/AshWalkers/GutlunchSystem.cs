using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.AnimalHusbandry;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class GutlunchSystem : SharedGutlunchSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GutlunchComponent, ReproductionAttemptEvent>(OnReproductionAttempt);
        SubscribeLocalEvent<GutlunchComponent, ReproductionStartedEvent>(OnReproductionStarted);
        SubscribeLocalEvent<GutlunchComponent, BeforeAnimalBirthEvent>(OnBeforeBirth);
    }

    protected override void OnExamined(Entity<GutlunchComponent> ent, ref ExaminedEvent args)
    {
        base.OnExamined(ent, ref args);
        if (args.IsInDetailsRange && TryComp<ReproductiveComponent>(ent, out var reproductive) && reproductive.Gestating)
            args.PushMarkup(Loc.GetString("gutlunch-examine-pregnant"));
    }

    protected override void OnMobStateChanged(Entity<GutlunchComponent> ent, ref MobStateChangedEvent args)
    {
        base.OnMobStateChanged(ent, ref args);
        if (args.NewMobState != MobState.Dead || !TryComp<ReproductiveComponent>(ent, out var reproductive))
            return;

        reproductive.Gestating = false;
        reproductive.GestationEndTime = null;
    }

    private void OnReproductionAttempt(Entity<GutlunchComponent> ent, ref ReproductionAttemptEvent args)
    {
        if (ent.Comp.IsMale || !TryComp<GutlunchComponent>(args.Partner, out var partner) || !partner.IsMale ||
            HasComp<ActorComponent>(args.Partner) ||
            TryComp<MindContainerComponent>(args.Partner, out var mind) && mind.HasMind ||
            _containers.IsEntityInContainer(ent) ||
            _containers.IsEntityInContainer(args.Partner) ||
            !_interaction.InRangeAndAccessible(ent.Owner, args.Partner) ||
            !TryGetMilk(ent, out var milk) || milk.Value.Comp.Solution.Volume == 0 ||
            milk.Value.Comp.Solution.AvailableVolume != 0 ||
            !TryGetMilk(args.Partner, out var partnerMilk) || partnerMilk.Value.Comp.Solution.Volume < ent.Comp.PartnerMilkCost ||
            CountPopulation(ent) >= ent.Comp.PopulationLimit)
        {
            args.Cancelled = true;
        }
    }

    private void OnReproductionStarted(Entity<GutlunchComponent> ent, ref ReproductionStartedEvent args)
    {
        if (TryGetMilk(ent, out var milk))
            _solutions.RemoveAllSolution(milk.Value);

        if (TryGetMilk(args.Partner, out var partnerMilk))
            _solutions.SplitSolution(partnerMilk.Value, ent.Comp.PartnerMilkCost);
    }

    private void OnBeforeBirth(Entity<GutlunchComponent> ent, ref BeforeAnimalBirthEvent args)
    {
        if (_containers.IsEntityInContainer(ent) || CountPopulation(ent) > ent.Comp.PopulationLimit)
            args.Cancelled = true;
    }

    private int CountPopulation(Entity<GutlunchComponent> ent)
    {
        var nearby = new HashSet<Entity<GutlunchComponent>>();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.PopulationRadius, nearby, LookupFlags.All);
        var count = 0;
        foreach (var animal in nearby)
        {
            if (TerminatingOrDeleted(animal) || _mobState.IsDead(animal))
                continue;

            count++;
            if (TryComp<ReproductiveComponent>(animal, out var reproductive) && reproductive.Gestating)
                count++;
        }

        return count;
    }
}
