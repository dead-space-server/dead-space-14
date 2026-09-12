using Robust.Shared.GameStates;
namespace Content.Shared._CM14.Weapons.Ranged.IFF;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IFFFactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Faction;
}
