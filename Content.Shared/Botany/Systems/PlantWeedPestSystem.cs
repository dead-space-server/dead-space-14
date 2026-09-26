using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Robust.Shared.Random;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Manages weed growth and pest damage per growth tick, and handles tray-level
/// weed spawning.
/// </summary>
public sealed partial class PlantWeedPestSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantWeedPestComponent, PlantCrossPollinateEvent>(OnCrossPollinate);
        SubscribeLocalEvent<PlantWeedPestComponent, PlantGrowEvent>(OnPlantGrow);
        _trayQuery = GetEntityQuery<PlantTrayComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PlantMutationSystem _mutation = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;

    private EntityQuery<PlantTrayComponent> _trayQuery;
    // DS14-end

    private void OnCrossPollinate(Entity<PlantWeedPestComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantWeedPestComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        // DS14-start
        _mutation.CrossFloat(ref ent.Comp.WeedTolerance, pollenData.WeedTolerance);
        _mutation.CrossFloat(ref ent.Comp.PestTolerance, pollenData.PestTolerance);
        // DS14-end
        Dirty(ent);
    }

    private void OnPlantGrow(Entity<PlantWeedPestComponent> ent, ref PlantGrowEvent args)
    {
        var trayUid = GetEntity(args.Tray);
        if (!_trayQuery.TryComp(trayUid, out var tray)) // DS14
            return;

        if (_random.Prob(ent.Comp.PestGrowthChance))
            _plantTray.AdjustPest((trayUid, tray), ent.Comp.PestGrowthAmount);

        if (tray.PestLevel > ent.Comp.PestTolerance)
            _plantHolder.AdjustsHealth(ent.Owner, -ent.Comp.PestDamageAmount);
    }

    /// <summary>
    /// Adjusts maximum weed level the plant can tolerate before taking damage.
    /// </summary>
    [PublicAPI]
    public void AdjustWeedTolerance(Entity<PlantWeedPestComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.WeedTolerance = MathF.Max(0f, ent.Comp.WeedTolerance + amount);
        DirtyField(ent, nameof(ent.Comp.WeedTolerance));
    }

    /// <summary>
    /// Adjusts maximum pest level the plant can tolerate before taking damage.
    /// </summary>
    [PublicAPI]
    public void AdjustPestTolerance(Entity<PlantWeedPestComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.PestTolerance = MathF.Max(0f, ent.Comp.PestTolerance + amount);
        DirtyField(ent, nameof(ent.Comp.PestTolerance));
    }
}
