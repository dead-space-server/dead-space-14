// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.EntityEffects;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Heats or chills the target's body by <see cref="Kelvin"/> degrees, whatever its size - the heat it takes is
/// worked out from the body's heat capacity (unlike <c>AdjustTemperature</c>, which hands over joules: a few
/// joules are nothing to a body). Heat resistance of the clothes counts.
/// </summary>
public sealed partial class LavalandAdjustBodyTemperature : EntityEffectBase<LavalandAdjustBodyTemperature>
{
    /// <summary>Degrees to add; negative chills.</summary>
    [DataField]
    public float Kelvin;
}

public sealed partial class LavalandAdjustBodyTemperatureEntityEffectSystem
    : EntityEffectSystem<TemperatureComponent, LavalandAdjustBodyTemperature>
{
    [Dependency] private readonly SharedTemperatureSystem _temperature = default!;

    protected override void Effect(Entity<TemperatureComponent> entity, ref EntityEffectEvent<LavalandAdjustBodyTemperature> args)
    {
        var heat = args.Effect.Kelvin * args.Scale * _temperature.GetHeatCapacity(entity, entity.Comp);
        _temperature.ChangeHeat(entity, heat, false, entity.Comp);
    }
}
