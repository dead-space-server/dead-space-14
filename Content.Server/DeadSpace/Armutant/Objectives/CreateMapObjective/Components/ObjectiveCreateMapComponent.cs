using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent]
public sealed partial class ObjectiveCreateMapComponent : Component
{
    [DataField(customTypeSerializer: typeof(ResPathSerializer))]
    public ResPath MapPath { get; private set; } = new("/Maps/DungeonArmutant/dungeon_1.yml");

    [DataField, AutoNetworkedField]
    public EntityUid? Stream = null;

    [DataField]
    public SoundSpecifier Sound;

    [DataField]
    public EntProtoId? SelfEffect = "VoidTeleportSelfEffect";

    [DataField]
    public bool UsingItem = false;
}
