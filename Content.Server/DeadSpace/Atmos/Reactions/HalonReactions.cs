// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace._Soyuz.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HalonExtinguishReaction : IGasReactionEffect
{
    private const float OxygenPerHalon = 20f;
    private const float ConversionDivisor = 5f;
    private const float EnergyPerOxygen = 1_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var halon = mixture.GetMoles(Gas.Halon);
        var oxygen = mixture.GetMoles(Gas.Oxygen);

        var units = MathF.Min(halon, oxygen / OxygenPerHalon) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oxygenRemoved = units * OxygenPerHalon;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Halon, -units);
        mixture.AdjustMoles(Gas.Oxygen, -oxygenRemoved);
        mixture.AdjustMoles(Gas.Fixirium, oxygenRemoved);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, -(oxygenRemoved * EnergyPerOxygen));

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class HalonProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 5f;
    private const float HalonPerFrezon = 2f;
    private const float OxygenPerFrezon = 0.2f;
    private const float EnergyPerFrezon = 5_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var frezon = mixture.GetMoles(Gas.Frezon);

        var units = frezon / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Frezon, -units);
        mixture.AdjustMoles(Gas.Halon, units * HalonPerFrezon);
        mixture.AdjustMoles(Gas.Oxygen, units * OxygenPerFrezon);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, units * EnergyPerFrezon);

        return ReactionResult.Reacting;
    }
}
