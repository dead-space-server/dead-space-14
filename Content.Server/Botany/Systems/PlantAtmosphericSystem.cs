using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;

namespace Content.Server.Botany.Systems;

public sealed partial class PlantAtmosphericSystem : SharedPlantAtmosphericSystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAtmosphericComponent, PlantGrowEvent>(OnPlantGrow);
        _holderQuery = GetEntityQuery<PlantHolderComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;

    private EntityQuery<PlantHolderComponent> _holderQuery;
    // DS14-end

    private void OnPlantGrow(Entity<PlantAtmosphericComponent> ent, ref PlantGrowEvent args)
    {
        if (!_holderQuery.TryComp(ent.Owner, out var holder))
            return;

        var environment = _atmosphere.GetContainingMixture(ent.Owner, true, true) ?? GasMixture.SpaceGas;
        if (environment.Temperature < ent.Comp.LowHeatTolerance || environment.Temperature > ent.Comp.HighHeatTolerance)
        {
            _plantHolder.AdjustsHealth((ent.Owner, holder), -ent.Comp.HeatToleranceDamage);
            holder.ImproperHeat = true;
        }
        else
            holder.ImproperHeat = false;

        var pressure = environment.Pressure;
        if (pressure < ent.Comp.LowPressureTolerance || pressure > ent.Comp.HighPressureTolerance)
        {
            _plantHolder.AdjustsHealth((ent.Owner, holder), -ent.Comp.PressureToleranceDamage);
            holder.ImproperPressure = true;
        }
        else
            holder.ImproperPressure = false;

        Dirty(ent);
    }
}
