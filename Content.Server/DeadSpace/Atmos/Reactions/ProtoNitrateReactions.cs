// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace._Soyuz.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateProductionReaction : IGasReactionEffect
{
    private const float HydrogenPerFixirium = 10f;
    private const float ConversionDivisor = 5f;
    private const float ProtoNitratePerUnit = 5f;
    private const float EnergyPerUnit = 15_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var fixirium = mixture.GetMoles(Gas.Fixirium);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);

        var units = MathF.Min(fixirium, hydrogen / HydrogenPerFixirium) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Fixirium, -units);
        mixture.AdjustMoles(Gas.Hydrogen, -(units * HydrogenPerFixirium));
        mixture.AdjustMoles(Gas.ProtoNitrate, units * ProtoNitratePerUnit);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, units * EnergyPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class ProtoNitrateBrizidiumResponseReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 5f;
    private const float PlasmaPerUnit = 4f;
    private const float EnergyPerUnit = 30_000f;
    private const float RadiationPerUnit = 0.5f;
    private const float MaxRadiation = 1.5f;
    private const float HallucinationRange = 5f;
    private const float HallucinationSeconds = 20f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var brizidium = mixture.GetMoles(Gas.Brizidium);

        var units = MathF.Min(protoNitrate, brizidium) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.ProtoNitrate, -units);
        mixture.AdjustMoles(Gas.Brizidium, -units);
        mixture.AdjustMoles(Gas.Nitrogen, units);
        mixture.AdjustMoles(Gas.Helium, units);
        mixture.AdjustMoles(Gas.Plasma, units * PlasmaPerUnit);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, units * EnergyPerUnit);

        atmosphereSystem.EmitRadiationPulse(holder, MathF.Min(units * RadiationPerUnit, MaxRadiation));

        if (atmosphereSystem.TryGetGasReactionCoordinates(holder, out var coords))
            atmosphereSystem.Hallucinations.CauseHallucinationsInRange(
                coords, HallucinationRange, TimeSpan.FromSeconds(HallucinationSeconds));

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class ProtoNitrateTritiumResponseReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 5f;
    private const float HydrogenPerUnit = 2f;
    private const float EnergyPerUnit = 20_000f;
    private const float RadiationPerUnit = 2f;
    private const float MaxRadiation = 3f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var tritium = mixture.GetMoles(Gas.Tritium);

        var units = MathF.Min(protoNitrate, tritium) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.ProtoNitrate, -units);
        mixture.AdjustMoles(Gas.Tritium, -units);
        mixture.AdjustMoles(Gas.Hydrogen, units * HydrogenPerUnit);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, units * EnergyPerUnit);

        atmosphereSystem.EmitRadiationPulse(holder, MathF.Min(units * RadiationPerUnit, MaxRadiation));

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class ProtoNitrateHydrogenResponseReaction : IGasReactionEffect
{
    private const float HydrogenPerUnit = 10f;
    private const float ConversionDivisor = 5f;
    private const float EnergyPerUnit = 15_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);
        var units = MathF.Min(protoNitrate, hydrogen / HydrogenPerUnit) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Hydrogen, -(units * HydrogenPerUnit));
        mixture.AdjustMoles(Gas.ProtoNitrate, units);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture, atmosphereSystem, heatScale, oldHeatCapacity, oldTemperature, -(units * EnergyPerUnit));

        return ReactionResult.Reacting;
    }
}
