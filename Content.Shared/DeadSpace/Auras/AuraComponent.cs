// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Auras;

/// <summary>
/// Defines an area of effect around the entity.
/// Managed predictively by AuraSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AuraComponent : Component
{
    /// <summary>
    /// The radius of the aura.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 3f;

    /// <summary>
    /// An entity that is entirely immune to this aura (e.g. Host of a Guardian).
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? IgnoredEntity;

    /// <summary>
    /// Optional unnetworked visual entity prototype to attach to affected targets on the client.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? VisualEffectPrototype;
}
