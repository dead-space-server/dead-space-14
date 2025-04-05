using Content.Server.DeadSpace.Armutant.Abilities.Gun;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.GunAbilities
{
    [RegisterComponent]
    [Access(typeof(GunSmokeSystem))]
    public sealed partial class GunSmokeComponent : Component
    {
        [DataField]
        public EntityUid? ActionGunSmokeEntity;

        [DataField("actionToggleGunSmoke", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string ActionToggleGunSmoke = "ActionToggleGunSmoke";

        [DataField]
        public EntProtoId? SmokePrototype = "GunSmokeInstantEffect";
    }
}
