using System.Linq;
using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles plant behavior and growth processing.
/// </summary>
public sealed partial class PlantSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlantComponent, PlantCrossPollinateEvent>(OnCrossPollinate);
        SubscribeLocalEvent<PlantComponent, PlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<PlantComponent, ExaminedEvent>(OnExamined);
        _holderQuery = GetEntityQuery<PlantHolderComponent>();
        _trayQuery = GetEntityQuery<PlantTrayComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PlantMutationSystem _mutation = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;

    private EntityQuery<PlantHolderComponent> _holderQuery;
    private EntityQuery<PlantTrayComponent> _trayQuery;
    // DS14-end

    private readonly List<Entity<PlantHolderComponent>> _holdersToUpdate = [];

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _holdersToUpdate.Clear();
        var query = EntityQueryEnumerator<PlantHolderComponent>();
        while (query.MoveNext(out var uid, out var plantHolder))
        {
            if (plantHolder.NextUpdate > _gameTiming.CurTime)
                continue;

            plantHolder.NextUpdate = _gameTiming.CurTime + plantHolder.CycleDelay; // DS14
            DirtyField(uid, plantHolder, nameof(plantHolder.NextUpdate));
            _holdersToUpdate.Add((uid, plantHolder));
        }

        foreach (var ent in _holdersToUpdate)
        {
            UpdatePlant(ent.AsNullable());
        }
    }

    private void OnMapInit(Entity<PlantComponent> ent, ref MapInitEvent args)
    {
        PlantingPlant(ent.AsNullable());
    }

    private void OnCrossPollinate(Entity<PlantComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        // DS14-start
        _mutation.CrossInt(ref ent.Comp.Yield, pollenData.Yield);
        _mutation.CrossFloat(ref ent.Comp.Endurance, pollenData.Endurance);
        _mutation.CrossFloat(ref ent.Comp.Lifespan, pollenData.Lifespan);
        _mutation.CrossFloat(ref ent.Comp.Maturation, pollenData.Maturation);
        _mutation.CrossFloat(ref ent.Comp.Production, pollenData.Production);
        _mutation.CrossFloat(ref ent.Comp.Potency, pollenData.Potency);
        // DS14-end
        _mutation.CrossTrait(ent.Owner, args.PollenData);
        Dirty(ent);
    }

    private void OnPlantGrow(Entity<PlantComponent> ent, ref PlantGrowEvent args)
    {
        if (!_holderQuery.TryComp(ent.Owner, out var holder)) // DS14
            return;

        // Check if plant is too old.
        if (holder.Age > ent.Comp.Lifespan)
            _plantHolder.AdjustsHealth(ent.Owner, -ent.Comp.OldAgeDamage);
    }

    private void OnExamined(Entity<PlantComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_holderQuery.TryComp(ent.Owner, out var holder)) // DS14
            return;

        using (args.PushGroup(nameof(PlantComponent)))
        {
            if (_plantHolder.IsDead(ent.Owner))
                args.PushMarkup(Loc.GetString("plant-component-dead-plant-matter-message"));

            if (_plantHolder.GetHealthThreshold(ent.Owner))
            {
                args.PushMarkup(Loc.GetString(
                    "plant-component-something-already-growing-low-health-message",
                    ("healthState",
                        Loc.GetString(holder.Age > ent.Comp.Lifespan
                            ? "plant-component-plant-old-adjective"
                            : "plant-component-plant-unhealthy-adjective"))));
            }

            foreach (var trait in AllComps<PlantTraitsComponent>(ent.Owner)
                         .OrderBy(trait => trait.GetType().FullName))
            {
                if (trait.TraitState is { } traitState)
                    args.PushMarkup(Loc.GetString(traitState));
            }
        }
    }

    /// <summary>
    /// Processes one plant's growth cycle and related effects.
    /// </summary>
    public void UpdatePlant(Entity<PlantHolderComponent?> ent, bool force = false)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        // DS14-start
        // Old maps can still contain PlantHolder on the tray itself. New holders belong to the plant entity.
        if (!HasComp<PlantComponent>(ent.Owner))
            return;
        // DS14-end

        var curTime = _gameTiming.CurTime;

        if (!force && curTime < ent.Comp.LastCycle + ent.Comp.CycleDelay)
            return;

        ent.Comp.LastCycle = curTime;
        DirtyField(ent, ent.Comp, nameof(ent.Comp.LastCycle));

        if (_plantHolder.IsDead(ent.Owner))
            return;

        // DS14-start
        if (TryGetTray(ent.Owner, out var trayEnt))
        {
            var plantGrow = new PlantGrowEvent(GetNetEntity(trayEnt.Owner));
            RaiseLocalEvent(ent.Owner, ref plantGrow);
        }
        // DS14-end

        // Process mutations.
        if (ent.Comp.MutationLevel > 0)
        {
            _mutation.CheckRandomMutations(ent.Owner, Math.Min(ent.Comp.MutationLevel, ent.Comp.MaxMutationLevel));
            ent.Comp.MutationLevel = 0;
            DirtyField(ent, ent.Comp, nameof(ent.Comp.MutationLevel));
        }

        if (ent.Comp.Health <= 0)
            _plantHolder.KillPlant(ent.Owner);
    }

    /// <summary>
    /// Forces an update of the tray by external cause.
    /// </summary>
    [PublicAPI]
    public void ForceUpdate(Entity<PlantComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        _plantHolder.AdjustsSkipAging(ent.Owner, 1);
        UpdatePlant(ent.Owner, force: true);
    }

    /// <summary>
    /// Tries to get the tray entity that the plant is in.
    /// </summary>
    [PublicAPI]
    public bool TryGetTray(Entity<PlantComponent?> ent, out Entity<PlantTrayComponent> trayEnt)
    {
        trayEnt = default!;
        if (!Resolve(ent.Owner, ref ent.Comp, false)) // DS14
            return false;

        trayEnt.Owner = Transform(ent.Owner).ParentUid;
        if (!_trayQuery.TryComp(trayEnt.Owner, out var trayComp)) // DS14
            return false;

        trayEnt.Comp = trayComp;
        return true;
    }

    /// <summary>
    /// Adjusts the potency of a plant component.
    /// </summary>
    [PublicAPI]
    public void AdjustPotency(Entity<PlantComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Potency = Math.Max(0, ent.Comp.Potency + amount);
        DirtyField(ent, nameof(ent.Comp.Potency));
    }

    /// <summary>
    /// Adjusts the lifespan of a plant component.
    /// </summary>
    [PublicAPI]
    public void AdjustLifespan(Entity<PlantComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Lifespan = Math.Max(0, ent.Comp.Lifespan + amount);
        DirtyField(ent, nameof(ent.Comp.Lifespan));
    }

    /// <summary>
    /// Adjusts the endurance of a plant component.
    /// </summary>
    [PublicAPI]
    public void AdjustEndurance(Entity<PlantComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Endurance = MathF.Max(0f, ent.Comp.Endurance + amount);
        DirtyField(ent, nameof(ent.Comp.Endurance));
    }

    /// <summary>
    /// Adjusts the yield of a plant component.
    /// </summary>
    [PublicAPI]
    public void AdjustYield(Entity<PlantComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Yield = Math.Max(0, ent.Comp.Yield + amount);
        DirtyField(ent, nameof(ent.Comp.Yield));
    }

    /// <summary>
    /// Adjusts the maturation time of a plant component.
    /// Must be at least 1 to prevent divide-by-zero in growth stage calculations.
    /// </summary>
    [PublicAPI]
    public void AdjustMaturation(Entity<PlantComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Maturation = MathF.Max(1f, ent.Comp.Maturation + amount);
        DirtyField(ent, nameof(ent.Comp.Maturation));

        if (ent.Comp.Production < ent.Comp.Maturation)
        {
            ent.Comp.Production = ent.Comp.Maturation;
            DirtyField(ent, nameof(ent.Comp.Production));
        }
    }

    /// <summary>
    /// Adjusts the production time of a plant component.
    /// Should not be lower than <see cref="PlantComponent.Maturation"/>.
    /// </summary>
    [PublicAPI]
    public void AdjustProduction(Entity<PlantComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Production = MathF.Max(ent.Comp.Maturation, ent.Comp.Production + amount);
        DirtyField(ent, nameof(ent.Comp.Production));
    }

    /// <summary>
    /// Removes the plant from the tray.
    /// </summary>
    [PublicAPI]
    public void RemovePlant(Entity<PlantComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        PredictedQueueDel(ent);
    }

    /// <summary>
    /// Gets the growth stage value of the plant.
    /// </summary>
    [PublicAPI]
    public int GetGrowthStageValue(Entity<PlantComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return 1;

        if (!_holderQuery.TryComp(ent.Owner, out var plantHolder)) // DS14
            return 1;

        int growthStage;
        if (plantHolder.Age < ent.Comp.Maturation)
            growthStage = (int)(plantHolder.Age * ent.Comp.GrowthStages / ent.Comp.Maturation);
        else
            growthStage = ent.Comp.GrowthStages;

        return Math.Max(1, growthStage);
    }

    /// <summary>
    /// Planting a plant.
    /// </summary>
    [PublicAPI]
    public void PlantingPlant(Entity<PlantComponent?> ent, float? healthOverride = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        if (!_holderQuery.TryComp(ent.Owner, out var plantHolder)) // DS14
            return;

        plantHolder.Health = healthOverride ?? ent.Comp.Endurance;
        DirtyFields(ent, plantHolder, null, nameof(plantHolder.Health));
    }

    /// <summary>
    /// Gets the warnings markup of the plant.
    /// </summary>
    [PublicAPI]
    public string GetPlantWarningsMarkup(Entity<PlantHolderComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return string.Empty;

        var markup = new List<string>();
        if (ent.Comp.ImproperHeat)
            markup.Add(Loc.GetString("plant-component-heat-improper-warning"));

        if (ent.Comp.ImproperPressure)
            markup.Add(Loc.GetString("plant-component-pressure-improper-warning"));

        if (ent.Comp.MissingGas)
            markup.Add(Loc.GetString("plant-component-gas-missing-warning"));

        return string.Join("\n", markup);
    }

    /// <summary>
    /// Gets the states markup of the plant.
    /// </summary>
    [PublicAPI]
    public string GetPlantStateMarkup(EntityUid uid, PlantComponent? component = null)
    {
        if (component == null && !Resolve(uid, ref component, false))
            return string.Empty;

        var markup = new List<string>();
        markup.Add(Loc.GetString("seed-component-plant-yield-text", ("seedYield", component.Yield)));
        markup.Add(Loc.GetString("seed-component-plant-potency-text", ("seedPotency", component.Potency)));

        return string.Join("\n", markup);
    }
}
