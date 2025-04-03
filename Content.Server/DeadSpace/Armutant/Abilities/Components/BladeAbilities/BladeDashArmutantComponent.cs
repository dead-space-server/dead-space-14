using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;

[RegisterComponent]
public sealed partial class BladeDashArmutantComponent : Component
{
    [DataField]
    public EntityUid? ActionDashEntity;

    [DataField("actionToggleDash", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleDashBlade = "ActionToggleDash";

    [DataField]
    public string DashSound = "/Audio/_DeadSpace/Armutant/sound_items_weapons_dash.ogg";

    public EntityUid? ActionEntity;

    [DataField("maxDashRange")]
    public float MaxDashRange = 10f;

    [DataField("collisionRadius")]
    public float CollisionRadius = 1.5f;

    public DamageSpecifier DashDamage = new DamageSpecifier
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Piercing", 25 }
        }
    };

    [DataField("knockbackForce")]
    public float KnockbackForce = 10f;

    [DataField]
    public EntProtoId? SelfEffect = "BladeDashSelfEffect";
}
