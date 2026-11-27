// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbRoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Pellet;

    [DataField, AutoNetworkedField]
    public bool Sever;

    [DataField, AutoNetworkedField]
    public float Bone;

    [DataField, AutoNetworkedField]
    public float Organ = 1f;

    [DataField, AutoNetworkedField]
    public float Bleed;

    /// <summary>
    /// Chance (0-1) that this round stays stuck in the limb it hits. Null - the general bullet chance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? Embed;
}
