using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;

namespace Content.Shared.Botany.Traits.Systems;

/// <inheritdoc cref="PlantTraitUnviableComponent"/>
public sealed partial class PlantTraitUnviableSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTraitUnviableComponent, PlantGrowEvent>(OnPlantGrow);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly PlantHarvestSystem _plantHarvest = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    // DS14-end

    private void OnPlantGrow(Entity<PlantTraitUnviableComponent> ent, ref PlantGrowEvent args)
    {
        _plantHarvest.AffectGrowth(ent.Owner, -1);
        _plantHolder.AdjustsHealth(ent.Owner, -ent.Comp.UnviableDamage);
    }
}
