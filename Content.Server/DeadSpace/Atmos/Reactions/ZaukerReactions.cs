// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace._Soyuz.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ZaukerProductionReaction : IGasReactionEffect
{
    private const float NitriumPerNoblium = 50f;
    private const float ConversionDivisor = 5f;
    private const float ZaukerPerUnit = 10f;
    private const float EnergyPerUnit = 40_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var noblium = mixture.GetMoles(Gas.HyperNoblium);
        var nitrium = mixture.GetMoles(Gas.Nitriatium);

        var units = MathF.Min(noblium, nitrium / NitriumPerNoblium) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.HyperNoblium, -units);
        mixture.AdjustMoles(Gas.Nitriatium, -(units * NitriumPerNoblium));
        mixture.AdjustMoles(Gas.Zauker, units * ZaukerPerUnit);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, -(units * EnergyPerUnit));

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class ZaukerDecompositionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 5f;
    private const float OxygenShare = 0.3f;
    private const float NitrogenShare = 0.7f;
    private const float EnergyPerUnit = 25_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var zauker = mixture.GetMoles(Gas.Zauker);

        var units = zauker / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Zauker, -units);
        mixture.AdjustMoles(Gas.Oxygen, units * OxygenShare);
        mixture.AdjustMoles(Gas.Nitrogen, units * NitrogenShare);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, units * EnergyPerUnit);

        return ReactionResult.Reacting;
    }
}
