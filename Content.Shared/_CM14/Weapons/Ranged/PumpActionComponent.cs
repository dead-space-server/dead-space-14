using Robust.Shared.Audio;
using Robust.Shared.GameStates;
namespace Content.Shared._CM14.Weapons.Ranged;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PumpActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Pumped = false;
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Weapons/Guns/Misc/slap.ogg");
}
