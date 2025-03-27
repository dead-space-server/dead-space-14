using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.DeadSpace.CorporateDjudo.Components;

[RegisterComponent]
public sealed partial class CorporateDjudoComponent : Component
{
    [DataField]
    public bool CanUseBaton = false;

    [DataField]
    public DamageSpecifier AddDamage = new()
    {
        DamageDict =
        {
            ["Blunt"] = 7
        }
    };

    [DataField]
    public DamageSpecifier StunBatonUse = new()
    {
        DamageDict =
        {
            ["Asphyxiation"] = 5
        }
    };

    [DataField]
    public EntityUid? ActionToBlindAttackEntity;

    [DataField("actionToBlindAttack", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToBlindId = "ActionToBlindAttack";

    [DataField]
    public bool IsReadyAtack = false;

    [DataField]
    public float SetChanceDisarm = 0.4f;

    [DataField]
    public float DefaultChanceDisarm = 0.75f;
}

[RegisterComponent]

public sealed partial class CorporateDjudoBeltComponent : Component;
