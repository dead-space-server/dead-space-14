using Content.Shared.DeadSpace.Armutant.Objectives.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent]
public sealed partial class ObjectiveCreateMapComponent : SharedObjectiveCreateMapComponent
{
    [DataField(customTypeSerializer: typeof(ResPathSerializer))]
    public ResPath MapPath { get; private set; } = new("/Maps/DungeonArmutant/dungeon_1.yml");

    /// <summary>
    /// Countdown audio stream.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Stream = null;

    /// <summary>
    /// Sound that plays when the mission end is imminent.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound;

    [DataField]
    public EntProtoId? SelfEffect = "VoidTeleportSelfEffect";
}
