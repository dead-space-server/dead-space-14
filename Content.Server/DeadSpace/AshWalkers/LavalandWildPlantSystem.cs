using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class LavalandWildPlantSystem : EntitySystem
{
    [Dependency] private readonly PlantTraySystem _trays = default!;
    [Dependency] private readonly PlantHolderSystem _holders = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandWildPlantComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<LavalandWildPlantComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PlantTrayComponent>(ent, out var tray) || _trays.TryGetPlant((ent, tray), out _))
            return;

        var children = Transform(ent).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!HasComp<PlantComponent>(child))
                continue;

            var health = TryComp<PlantHolderComponent>(child, out var holder) ? holder.Health : (float?) null;
            _trays.PlantingPlantInTray((ent, tray), child, health);
            return;
        }

        var plant = Spawn(ent.Comp.Plant, Transform(ent).Coordinates);
        _trays.PlantingPlantInTray((ent, tray), plant);
        if (TryComp<PlantComponent>(plant, out var growth))
            _holders.AdjustsAge(plant, (int) MathF.Ceiling(growth.Maturation + growth.Production));
    }
}
