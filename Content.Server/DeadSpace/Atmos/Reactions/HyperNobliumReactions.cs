// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace._Soyuz.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HyperNobliumProductionReaction : IGasReactionEffect
{
    private const float NitrogenPerNob = 10f;
    private const float BaseTritiumPerNob = 5f;
    private const float MinTritiumPerNob = 0.005f;
    private const float ConversionDivisor = 6f;
    private const float EnergyPerNob = 200_000f;
    private const float ExplosionThresholdNob = 0.5f;
    private const float ExplosionIntensityPerNob = 6f;
    private const float MaxExplosionIntensity = 100f;
    private const float ExplosionSlope = 1.5f;
    private const float MaxTileIntensity = 8f;
    private const string ExplosionPrototype = "Default";

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var tritium = mixture.GetMoles(Gas.Tritium);
        var brizidium = mixture.GetMoles(Gas.Brizidium);
        var reductionFactor = Math.Clamp(tritium / (tritium + brizidium), 0.001f, 1f);
        var tritiumPerNob = MathF.Max(BaseTritiumPerNob * reductionFactor, MinTritiumPerNob);

        var nob = MathF.Min(nitrogen / NitrogenPerNob, tritium / tritiumPerNob) / ConversionDivisor;
        if (nob <= 0f)
            return ReactionResult.NoReaction;

        var oldTemperature = mixture.Temperature;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Nitrogen, -(nob * NitrogenPerNob));
        mixture.AdjustMoles(Gas.Tritium, -(nob * tritiumPerNob));
        mixture.AdjustMoles(Gas.HyperNoblium, nob);

        var dampedEnergy = nob * EnergyPerNob * reductionFactor;
        SoyuzGasReactionHelpers.ApplyEnergy(
            mixture,
            atmosphereSystem,
            heatScale,
            oldHeatCapacity,
            oldTemperature,
            dampedEnergy);

        if (brizidium < nob && nob >= ExplosionThresholdNob &&
            atmosphereSystem.TryGetGasReactionCoordinates(holder, out var coords))
        {
            var intensity = MathF.Min(nob * ExplosionIntensityPerNob, MaxExplosionIntensity);
            atmosphereSystem.Explosion.QueueExplosion(
                coords,
                ExplosionPrototype,
                intensity,
                ExplosionSlope,
                MaxTileIntensity,
                cause: null,
                addLog: false);
        }

        return ReactionResult.Reacting;
    }
}

[UsedImplicitly]
public sealed partial class HyperNobliumSuppressionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        return ReactionResult.StopReactions;
    }
}
