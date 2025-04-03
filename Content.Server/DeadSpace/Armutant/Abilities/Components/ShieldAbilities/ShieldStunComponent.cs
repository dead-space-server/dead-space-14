using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;

[RegisterComponent]
public sealed partial class ShieldStunComponent : Component
{
    public DamageSpecifier Damage = new DamageSpecifier
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Blunt", 10 }
        }
    };

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(1.0);

    [DataField]
    public float ShortRange = 0.5f;

    [DataField]
    public float Range = 2.82f;

    [DataField]
    public EntProtoId? SelfEffect = "EffectSelfStun";

    [DataField]
    public EntProtoId? Effect = "EffectStun";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effects_meteorimpact.ogg");

    [DataField]
    public EntityUid? ActionShieldStunEntity;

    [DataField("actionToggleShieldArmor", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleShieldStunSpawn = "ActionToggleShieldStun";

    [DataField("knockbackForce")]
    public float KnockbackForce = 15f;
}
