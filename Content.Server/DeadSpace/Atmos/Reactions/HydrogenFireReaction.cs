// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Reactions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class HydrogenFireReaction : IGasReactionEffect
{
    [DataField]
    public float EnergyPerMole = Atmospherics.FireHydrogenEnergyReleased;

    [DataField]
    public float ExplosionThresholdMoles = 1f;

    [DataField]
    public float ExplosionIntensityPerMole = 0.75f;

    [DataField]
    public float MaxExplosionIntensity = 75f;

    [DataField]
    public float ExplosionSlope = 0.75f;

    [DataField]
    public float MaxTileIntensity = 6f;

    [DataField]
    public string ExplosionPrototype = "Default";

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;

        var hydrogen = mixture.GetMoles(Gas.Hydrogen);
        var oxygen = mixture.GetMoles(Gas.Oxygen);

        var hydrogenBurned = MathF.Min(hydrogen, oxygen * 2f);
        if (hydrogenBurned <= 0f)
            return ReactionResult.NoReaction;

        var oxygenBurned = hydrogenBurned / 2f;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;

        mixture.AdjustMoles(Gas.Hydrogen, -hydrogenBurned);
        mixture.AdjustMoles(Gas.Oxygen, -oxygenBurned);
        mixture.AdjustMoles(Gas.WaterVapor, hydrogenBurned);
        mixture.ReactionResults[(byte)GasReaction.Fire] = hydrogenBurned;

        var energyReleased = EnergyPerMole * oxygenBurned / heatScale;
        if (energyReleased > 0f)
        {
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
        }

        if (holder is TileAtmosphere tile)
        {
            var mixTemperature = mixture.Temperature;
            if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                atmosphereSystem.HotspotExpose(tile, mixTemperature, mixture.Volume);
        }
        
        if (hydrogenBurned >= ExplosionThresholdMoles &&
            atmosphereSystem.TryGetGasReactionCoordinates(holder, out var coords))
        {
            var intensity = MathF.Min(hydrogenBurned * ExplosionIntensityPerMole, MaxExplosionIntensity);
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
