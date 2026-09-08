using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Fishing;

[Serializable, NetSerializable]
public enum LavaFishingPhase : byte
{
    Idle,
    Waiting,
    Bite,
    Reeling,
    Caught,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class LavaFishingRodComponent : Component
{
    [DataField]
    public float CastRange = 4;

    [DataField]
    public int BaitCapacity = 5;

    [DataField]
    public FixedPoint2 BaitPortion = 5;

    [DataField, AutoNetworkedField]
    public int Bait;

    [DataField, AutoNetworkedField]
    public EntityUid? Fisher;

    [DataField, AutoNetworkedField]
    public LavaFishingPhase Phase;

    [AutoNetworkedField]
    public EntityCoordinates Origin;

    [AutoNetworkedField]
    public EntityCoordinates Spot;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPhase;

    [DataField, AutoNetworkedField]
    public bool Reeling;

    [DataField, AutoNetworkedField]
    public float ZonePosition = 0.5f;

    [DataField, AutoNetworkedField]
    public float FishPosition = 0.5f;

    [DataField, AutoNetworkedField]
    public float Progress = 0.25f;

    [DataField, AutoNetworkedField]
    public float Elapsed;

    [DataField, AutoNetworkedField]
    public float Difficulty;

    [DataField, AutoNetworkedField]
    public float Rhythm;

    [DataField, AutoNetworkedField]
    public bool IsFish;

    [DataField, AutoNetworkedField]
    public EntityUid? Float;

    [DataField]
    public string CatchContainer = "fishing_catch";

    [DataField]
    public List<LavaFishingReward> Rewards = new();
}

[DataDefinition]
public sealed partial class LavaFishingReward
{
    [DataField(required: true)]
    public EntityTableSelector Table = default!;

    [DataField]
    public float Weight = 1;

    [DataField]
    public float Difficulty = 1;

    [DataField]
    public bool Fish = true;
}
