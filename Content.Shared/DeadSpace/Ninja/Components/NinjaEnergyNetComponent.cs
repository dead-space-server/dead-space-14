using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class NinjaEnergyNetGunComponent : Component
{
    [DataField]
    public EntProtoId NetProto = "NinjaNet";

    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan RechargeTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public float Range = 10f;

    [DataField]
    public float Charge = 100f;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Effects/PowerSink/electric.ogg");

    [DataField]
    public SpriteSpecifier NetBeamSprite =
        new SpriteSpecifier.Rsi(new ResPath("_DeadSpace/Objects/Weapons/Guns/ninja.rsi"), "n_beam");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaEnergyNetComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan BeamDeleteTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan BeamDuration = TimeSpan.FromSeconds(2);
}