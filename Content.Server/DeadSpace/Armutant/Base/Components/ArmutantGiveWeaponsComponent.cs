using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Armutant.Base.Components;

[RegisterComponent]
public sealed partial class ArmutantGiveWeaponsComponent : Component
{
    public EntProtoId? TrackedWeapon;
}
