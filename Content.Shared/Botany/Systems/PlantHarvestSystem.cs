using JetBrains.Annotations;
using Content.Shared.Administration.Logs;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Manages harvest readiness and execution for plants, including repeat/self-harvest
/// logic and produce spawning, responding to growth and interaction events.
/// </summary>
public sealed partial class PlantHarvestSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTrayComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<PlantHolderComponent, PlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<PlantHolderComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<PlantHolderComponent, DoHarvestEvent>(OnHandledDoHarvest);

        _holderQuery = GetEntityQuery<PlantHolderComponent>();
        _harvestQuery = GetEntityQuery<PlantHarvestComponent>();
        _plantQuery = GetEntityQuery<PlantComponent>();
        _dataQuery = GetEntityQuery<PlantDataComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly PlantSystem _plant = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;

    private EntityQuery<PlantHolderComponent> _holderQuery;
    private EntityQuery<PlantHarvestComponent> _harvestQuery;
    private EntityQuery<PlantComponent> _plantQuery;
    private EntityQuery<PlantDataComponent> _dataQuery;
    // DS14-end

    private void OnInteractHand(Entity<PlantTrayComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_plantTray.TryGetPlant(ent.AsNullable(), out var plantUid)
            // DS14-start
            || !_holderQuery.TryComp(plantUid, out var holder)
            || !holder.ReadyForHarvest)
            // DS14-end
            return;

        // TODO: Remove this once trays have a proper UI.
        TryHandleHarvest(plantUid.Value, args.User);
        args.Handled = true;
    }

    private void OnPlantGrow(Entity<PlantHolderComponent> ent, ref PlantGrowEvent args) // DS14
    {
        // DS14-start
        if (!_harvestQuery.TryComp(ent.Owner, out var harvest)
            || !_plantQuery.TryComp(ent.Owner, out var plant))
        // DS14-end
            return;

        // If the plant is not mature, set the last harvest to the current age.
        // DS14-start
        if (ent.Comp.Age < plant.Maturation)
            ent.Comp.LastHarvest = ent.Comp.Age;
        // DS14-end

        TryAutoHarvest((ent.Owner, harvest), ent.Owner); // DS14

        // Update whether the plant is ready for harvest.
        var timeLastHarvest = ent.Comp.Age - ent.Comp.LastHarvest; // DS14
        if (timeLastHarvest > plant.Production && !ent.Comp.ReadyForHarvest)
        {
            ent.Comp.ReadyForHarvest = true;
            ent.Comp.LastHarvest = ent.Comp.Age; // DS14
        }

        Dirty(ent);
    }

    private void OnInteractHand(Entity<PlantHolderComponent> ent, ref InteractHandEvent args) // DS14
    {
        if (args.Handled)
            return;

        if (_plantHolder.IsDead(ent.Owner))
        {
            args.Handled = true;
            _plant.RemovePlant(ent.Owner);
            return;
        }

        if (!ent.Comp.ReadyForHarvest)
            return;

        var ev = new DoHarvestEvent(args.User, ent.Owner);
        RaiseLocalEvent(ent.Owner, ref ev);
        args.Handled = true;
    }

    private void OnHandledDoHarvest(Entity<PlantHolderComponent> ent, ref DoHarvestEvent args) // DS14
    {
        if (args.Cancelled)
            return;

        TryHandleHarvest(ent, args.User);
    }

    private void TryAutoHarvest(Entity<PlantHarvestComponent> ent, EntityUid user)
    {
        if (ent.Comp.HarvestRepeat != HarvestType.SelfHarvest)
            return;

        if (_dataQuery.TryComp(ent.Owner, out var plantData) && plantData.HarvestLogImpact != null) // DS14
            _adminLogger.Add(LogType.Botany, plantData.HarvestLogImpact.Value, $"Auto-harvested {Loc.GetString(plantData.Name):seed} at Pos:{Transform(ent.Owner).Coordinates}.");

        DoHarvest(ent.Owner, user); // DS14
    }

    /// <summary>
    /// Handles harvesting a plant for the specified user.
    /// </summary>
    [PublicAPI]
    public void TryHandleHarvest(EntityUid plant, EntityUid user)
    {
        if (_dataQuery.TryComp(plant, out var plantData) && plantData.HarvestLogImpact != null) // DS14
            _adminLogger.Add(LogType.Botany, plantData.HarvestLogImpact.Value, $"Auto-harvested {Loc.GetString(plantData.Name):seed} at Pos:{Transform(plant).Coordinates}.");

        DoHarvest(plant, user);
    }

    /// <summary>
    /// Harvests the plant and produces the produce.
    /// </summary>
    [PublicAPI]
    public void DoHarvest(Entity<PlantHolderComponent?> ent, EntityUid user) // DS14
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        // DS14-start
        if (!_plantQuery.TryComp(ent.Owner, out var plant)
            || !_dataQuery.TryComp(ent.Owner, out var plantData)
            || !_harvestQuery.TryComp(ent.Owner, out var harvest))
        // DS14-end
            return;

        if (!ent.Comp.ReadyForHarvest || plantData.ProductPrototypes.Count == 0 || plant.Yield == 0)
            return;

        var name = Loc.GetString(plantData.Name);
        _popup.PopupCursor(Loc.GetString("botany-harvest-success-message", ("name", name)), user, PopupType.Medium);

        var totalYield = 0;
        if (plant.Yield >= 0)
        {
            totalYield = ent.Comp.YieldMod < 0 ? plant.Yield : plant.Yield * ent.Comp.YieldMod; // DS14
            totalYield = Math.Max(1, totalYield);
        }

        // DS14-start
        var position = Transform(user).Coordinates;
        _botany.SpawnProduce(ent.Owner, position, totalYield);
        // DS14-end

        ent.Comp.ReadyForHarvest = false;
        ent.Comp.LastHarvest = ent.Comp.Age; // DS14
        Dirty(ent);

        if (harvest.HarvestRepeat == HarvestType.NoRepeat) // DS14
            _plant.RemovePlant(ent.Owner);

        var ev = new AfterDoHarvestEvent(user, ent.Owner);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    /// <summary>
    /// Affects the growth of a plant by modifying its age or production timing.
    /// </summary>
    [PublicAPI]
    public void AffectGrowth(Entity<PlantHolderComponent?> ent, int amount) // DS14
    {
        if (amount == 0)
            return;

        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        if (!_plantQuery.TryComp(ent.Owner, out var plant)) // DS14
            return;

        if (amount > 0)
        {
            if (ent.Comp.Age < plant.Maturation) // DS14
                _plantHolder.AdjustsAge(ent.Owner, amount);
            else if (!ent.Comp.ReadyForHarvest && plant.Yield <= 0f)
                ent.Comp.LastHarvest -= amount;
        }
        else
        {
            if (ent.Comp.Age < plant.Maturation) // DS14
                _plantHolder.AdjustsSkipAging(ent.Owner, 1);
            else if (!ent.Comp.ReadyForHarvest && plant.Yield <= 0f)
                ent.Comp.LastHarvest += amount;
        }

        DirtyField(ent, nameof(ent.Comp.LastHarvest));
    }

    /// <summary>
    /// Changes the harvest repeat of a plant.
    /// </summary>
    [PublicAPI]
    public void ChangeHarvestRepeat(Entity<PlantHarvestComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.HarvestRepeat = ent.Comp.HarvestRepeat switch
        {
            HarvestType.NoRepeat => HarvestType.Repeat,
            HarvestType.Repeat => HarvestType.SelfHarvest,
            _ => ent.Comp.HarvestRepeat,
        };

        DirtyField(ent, nameof(ent.Comp.HarvestRepeat));
    }
}
