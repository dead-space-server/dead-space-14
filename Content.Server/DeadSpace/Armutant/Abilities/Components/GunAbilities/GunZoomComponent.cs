using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Server.DeadSpace.Armutant.Abilities.Gun;

namespace Content.Server.DeadSpace.Armutant.Abilities.Components.GunAbilities;

[RegisterComponent]
[Access(typeof(GunZoomSystem))]
public sealed partial class GunZoomComponent : Component
{
    [DataField]
    public EntityUid? ActionGunZoomEntity;

    [DataField("actionToggleGunZoom", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionToggleGunZoom = "ActionToggleGunZoom";

    [DataField]
    public bool Enabled;

    [DataField]
    public Vector2 Zoom = new(1.8f, 1.8f);

    [DataField]
    public Vector2 Offset;
}
