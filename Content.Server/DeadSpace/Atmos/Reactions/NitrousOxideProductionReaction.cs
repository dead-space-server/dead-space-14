// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace._Soyuz.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class NitrousOxideProductionReaction : IGasReactionEffect
{
    private const float OxygenPerNitrogen = 0.5f;
    private const float ConversionDivisor = 3f;
    private const float EnergyPerMole = 25_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var oxygen = mixture.GetMoles(Gas.Oxygen);

        var units = MathF.Min(nitrogen, oxygen / OxygenPerNitrogen) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Nitrogen, -units);
        mixture.AdjustMoles(Gas.Oxygen, -(units * OxygenPerNitrogen));
        mixture.AdjustMoles(Gas.NitrousOxide, units);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyPerMole);

        return ReactionResult.Reacting;
    }
}
