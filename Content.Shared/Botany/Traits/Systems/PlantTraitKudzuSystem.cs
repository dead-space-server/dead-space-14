using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;

namespace Content.Shared.Botany.Traits.Systems;

/// <inheritdoc cref="PlantTraitKudzuComponent"/>
public sealed partial class PlantTraitKudzuSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTraitKudzuComponent, PlantGrowEvent>(OnPlantGrow);
        _trayQuery = GetEntityQuery<PlantTrayComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<PlantTrayComponent> _trayQuery;
    // DS14-end

    private void OnPlantGrow(Entity<PlantTraitKudzuComponent> ent, ref PlantGrowEvent args)
    {
        var trayUid = GetEntity(args.Tray);
        if (!_trayQuery.TryComp(trayUid, out var trayComp)) // DS14
            return;

        if (trayComp is { WaterLevel: > 10, NutritionLevel: > 5 })
            _plantTray.AdjustWeed(trayUid, ent.Comp.WeedGrowthAmount);

        // Handle kudzu transformation.
        if (trayComp.WeedLevel >= ent.Comp.WeedLevelThreshold)
        {
            EntityManager.PredictedSpawn(ent.Comp.KudzuPrototype, _transform.GetMapCoordinates(ent.Owner));
            RemComp<PlantTraitKudzuComponent>(ent.Owner);
            _plantHolder.KillPlant(ent.Owner);
        }
    }
}
