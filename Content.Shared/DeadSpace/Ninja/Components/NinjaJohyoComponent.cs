using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaJohyoComponent : Component
{
    [DataField]
    public List<LocId> ShotPhrases = new();

    [DataField]
    public float PullAcceleration = 10f;

    [DataField]
    public float MaxPullSpeed = 15f;

    [DataField]
    public float ArrivalDistance = 1.5f;

    [DataField]
    public SpriteSpecifier ChainSprite =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaJohyoProjectileComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? Shooter;

    [AutoNetworkedField]
    public EntityUid? Target;

    public float PullAcceleration;
    public float MaxPullSpeed;
    public float ArrivalDistance;
    public bool Pulling;
}