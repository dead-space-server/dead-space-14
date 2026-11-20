// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryCavityComponent : Component
{
    public const string Head = "surgery-cavity-head";
    public const string Abdomen = "surgery-cavity-abdomen";
    public const string Chest = "surgery-cavity-chest";

    public const int Cap = 1;

    [AutoNetworkedField]
    public NetEntity HeadItem;

    [AutoNetworkedField]
    public NetEntity AbdomenItem;

    [AutoNetworkedField]
    public NetEntity ChestItem;

    [AutoNetworkedField]
    public string HeadProto = "";

    [AutoNetworkedField]
    public string AbdomenProto = "";

    [AutoNetworkedField]
    public string ChestProto = "";

    public bool Holds(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => HeadProto.Length > 0,
            LimbZone.Torso => ChestProto.Length > 0,
            LimbZone.Groin => AbdomenProto.Length > 0,
            _ => false,
        };
    }
}
