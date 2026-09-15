// SPDX-FileCopyrightText: 2026 Kofeecheks
// SPDX-License-Identifier: LicenseRef-Kofeecheks

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace._Soyuz.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class FixiriumProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 12f;
    private const float EnergyReleasedPerUnit = 80_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var tritium = mixture.GetMoles(Gas.Tritium);
        var carbonDioxide = mixture.GetMoles(Gas.CarbonDioxide);
        var oxygen = mixture.GetMoles(Gas.Oxygen);

        var units = MathF.Min(oxygen / 100f, MathF.Min(carbonDioxide / 50f, tritium)) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Oxygen, -(units * 100f));
        mixture.AdjustMoles(Gas.CarbonDioxide, -(units * 50f));
        mixture.AdjustMoles(Gas.Tritium, -units);
        mixture.AdjustMoles(Gas.Fixirium, units * 100f);
        mixture.AdjustMoles(Gas.Nitrogen, units * 50f);
        mixture.AdjustMoles(Gas.Hydrogen, units * 0.1f);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyReleasedPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class BrizidiumProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 10f;
    private const float EnergyReleasedPerUnit = 70_000f;
    private const float OptimalPressure = 10f;
    private const float MaxPressure = 40f;
    private const float MaxPressureFactor = 5f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var pressure = mixture.Pressure;
        if (pressure > MaxPressure)
            return ReactionResult.NoReaction;

        var plasma = mixture.GetMoles(Gas.Plasma);
        var nitrousOxide = mixture.GetMoles(Gas.NitrousOxide);

        var units = MathF.Min(plasma / 2f, nitrousOxide) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var pressureFactor = MathF.Min(OptimalPressure / MathF.Max(pressure, 1f), MaxPressureFactor);
        units *= pressureFactor;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Plasma, -(units * 2f));
        mixture.AdjustMoles(Gas.NitrousOxide, -units);
        mixture.AdjustMoles(Gas.Brizidium, units * 2f);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyReleasedPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class NitriatiumProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 6f;
    private const float EnergyReleasedPerUnit = -60_000f;
    private const float EfficiencyReferenceTemperature = 1500f;
    private const float MaxEfficiency = 3f;
    private const float BaseYield = 20f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var tritium = mixture.GetMoles(Gas.Tritium);
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var brizidium = mixture.GetMoles(Gas.Brizidium);

        var units = MathF.Min(tritium / 20f, MathF.Min(nitrogen / 20f, brizidium)) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var efficiency = Math.Clamp(mixture.Temperature / EfficiencyReferenceTemperature, 1f, MaxEfficiency);

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Tritium, -(units * 20f));
        mixture.AdjustMoles(Gas.Nitrogen, -(units * 20f));
        mixture.AdjustMoles(Gas.Brizidium, -units);
        mixture.AdjustMoles(Gas.Nitriatium, units * BaseYield * efficiency);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyReleasedPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class NitriatiumDecompositionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 10f;
    private const float EnergyReleasedPerUnit = 40_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitriatium = mixture.GetMoles(Gas.Nitriatium);

        var units = nitriatium / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Nitriatium, -units);
        mixture.AdjustMoles(Gas.Nitrogen, units);
        mixture.AdjustMoles(Gas.Hydrogen, units * 0.1f);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyReleasedPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class HiliumProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 5f;
    private const float EnergyReleasedPerUnit = 60_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var frezon = mixture.GetMoles(Gas.Frezon);
        var brizidium = mixture.GetMoles(Gas.Brizidium);

        var units = MathF.Min(frezon / 11f, brizidium) / ConversionDivisor;
        if (units <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Frezon, -(units * 11f));
        mixture.AdjustMoles(Gas.Brizidium, -units);
        mixture.AdjustMoles(Gas.Hilium, units * 11f);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            units * EnergyReleasedPerUnit);

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class IpritDecayReaction : IGasReactionEffect
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var iprit = mixture.GetMoles(Gas.Iprit);
        if (iprit < Atmospherics.GasMinMoles)
            return ReactionResult.NoReaction;

        var now = atmosphereSystem.CurrentSimulationTime;
        if (mixture.IpritDecayDeadline == TimeSpan.Zero)
        {
            mixture.EnsureIpritDecayDeadline(now + Lifetime);
            return ReactionResult.NoReaction;
        }

        if (now < mixture.IpritDecayDeadline)
            return ReactionResult.NoReaction;

        mixture.SetMoles(Gas.Iprit, 0f);
        mixture.AdjustMoles(Gas.Oxygen, iprit);
        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class IpritProductionReaction : IGasReactionEffect
{
    private const float ConversionDivisor = 10f;
    private const float EnergyPerMole = 90_000f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var tritium = mixture.GetMoles(Gas.Tritium);
        var fixirium = mixture.GetMoles(Gas.Fixirium);
        var reacted = MathF.Min(tritium, fixirium) / ConversionDivisor;

        if (reacted <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Tritium, -reacted);
        mixture.AdjustMoles(Gas.Fixirium, -reacted);
        // Kofeecheks Iprit decay: LicenseRef-Kofeecheks
        var produced = reacted * 2f;
        mixture.AdjustMoles(Gas.Iprit, produced);
        mixture.BlendIpritDecayDeadline(mixture.GetMoles(Gas.Iprit) - produced, atmosphereSystem.CurrentSimulationTime + IpritDecayReaction.Lifetime, produced);

        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            reacted * 2f * EnergyPerMole);

        return ReactionResult.Reacting;
    }
}