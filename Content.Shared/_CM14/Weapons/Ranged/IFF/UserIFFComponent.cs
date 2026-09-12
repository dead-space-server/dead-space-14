using Robust.Shared.GameStates;
namespace Content.Shared._CM14.Weapons.Ranged.IFF;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UserIFFComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Faction;
}
