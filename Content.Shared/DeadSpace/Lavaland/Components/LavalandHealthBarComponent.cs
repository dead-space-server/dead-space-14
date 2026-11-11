// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// The health bar over its head (medical and other HUDs) counts from this health, not from its damage thresholds:
/// a boss that keeps its own health (the Ash Wolf's thresholds are never reached).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandHealthBarComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxHealth = 100f;

    /// <summary>No bar at all for now (the Ash Wolf up in the sky: there is nothing to see where it was).</summary>
    [DataField, AutoNetworkedField]
    public bool Hidden;
}
