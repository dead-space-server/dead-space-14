using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;

namespace Content.Server.Botany.Systems;

public sealed partial class PlantConsumeExudeGasSystem : SharedPlantConsumeExudeGasSystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantConsumeExudeGasComponent, PlantGrowEvent>(OnPlantGrow);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    // DS14-end

    private void OnPlantGrow(Entity<PlantConsumeExudeGasComponent> ent, ref PlantGrowEvent args)
    {
        if (!TryComp<PlantComponent>(ent.Owner, out var plant)
            || !TryComp<PlantHolderComponent>(ent.Owner, out var plantHolder))
            return;

        var environment = _atmosphere.GetContainingMixture(ent.Owner, true, true) ?? GasMixture.SpaceGas;

        // Consume Gasses.
        plantHolder.MissingGas = false;
        var missingGas = 0;
        if (ent.Comp.ConsumeGasses.Count > 0)
        {
            foreach (var (gas, amount) in ent.Comp.ConsumeGasses)
            {
                if (environment.GetMoles(gas) < amount)
                {
                    missingGas++;
                    continue;
                }

                environment.AdjustMoles(gas, -amount);
            }

            if (missingGas > 0)
            {
                _plantHolder.AdjustsHealth(ent.Owner, -missingGas);
                plantHolder.MissingGas = true;
            }
        }

        // Exude Gasses.
        var exudeCount = ent.Comp.ExudeGasses.Count;
        if (exudeCount > 0)
        {
            foreach (var (gas, amount) in ent.Comp.ExudeGasses)
            {
                environment.AdjustMoles(gas,
                    MathF.Max(1f, MathF.Round(amount * MathF.Round(plant.Potency) / exudeCount)));
            }
        }

        Dirty(ent);
    }
}
