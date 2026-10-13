// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Temperature;

/// <summary>
/// Remembers which burn stages this body has announced, even after healing or revival.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CorpseBurningComponent : Component
{
    public const int FirstStageDamage = 400;

    [DataField]
    public string MessageType = "skin";

    [DataField]
    public int LastPopupStage;

    /// <summary>
    /// Serious burns hide freshness until fully healed, independently of the lifetime popup history.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SuppressFreshDescription;
}
