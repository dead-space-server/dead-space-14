// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Weather;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Storm visor: worn over the eyes, it makes the ash (and snow) storm almost see-through for its wearer.
/// That is all it does - no protection, no other vision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandStormVisorComponent : Component
{
    /// <summary>How much of the storm the wearer still sees (1 - all of it, 0 - none).</summary>
    [DataField]
    public float StormOpacity = 0.15f;

    /// <summary>The weather it clears; empty - any weather.</summary>
    [DataField]
    public List<ProtoId<WeatherPrototype>> Weathers = new();
}
