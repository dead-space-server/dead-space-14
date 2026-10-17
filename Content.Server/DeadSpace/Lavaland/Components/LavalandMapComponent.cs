using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class LavalandMapComponent : Component
{
    [DataField]
    public EntityUid Station;

    [DataField]
    public ProtoId<LavalandPlanetPrototype> Planet;

    [DataField]
    public int Seed;

    [DataField]
    public LavalandWeatherStage WeatherStage = LavalandWeatherStage.Calm;

    [DataField]
    public bool WeatherInitialized;

    /// <summary>The current storm is a blizzard, not an ash storm.</summary>
    [DataField]
    public bool SnowStorm;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextWeatherTime = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan WeatherStageEndTime = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextWeatherDamageTime = TimeSpan.Zero;
}

public enum LavalandWeatherStage : byte
{
    Calm,
    Warning,
    Active,
}
