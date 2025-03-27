using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.SmokingCarp.Abilities.Components;

[RegisterComponent]
public sealed partial class SmokePunchComponent : Component
{
    [DataField]
    public EntityUid? ActionSmokePunchCarpAttackEntity;

    [DataField("actionSmokePunchCarp", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionSmokePunchCarpAttack = "ActionSmokePunchCarp";

    [DataField]
    public bool IsDamageAttack = false;

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict =
        {
            ["Slash"] = 5
        }
    };

    [DataField]
    public EntProtoId? EffectPunch = "WeaponArcBite";
}
