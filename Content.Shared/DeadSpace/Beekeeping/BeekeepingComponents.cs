// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Beekeeping;

public enum BeeState : byte
{
    Idle,
    SearchingFlower,
    MovingToFlower,
    Pollinating,
    ReturningToHive,
}

/// <summary>
/// A worker bee of a hive. Handled by BeeAISystem: it flies to a flowering plant near the hive, pollinates it, and
/// brings the pollen home. The bee does not use HTN, its brain is a small state machine that thinks twice a second
/// and does nothing at all when no player is near.
/// </summary>
[RegisterComponent]
public sealed partial class BeeComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? HiveOwner;

    [ViewVariables]
    public BeeState State = BeeState.Idle;

    [ViewVariables]
    public EntityUid? TargetFlower;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollenCarried;

    [DataField]
    public float MaxPollenCarry = 20f;

    /// <summary>
    /// How long the bee rests at the hive before the next flight, in seconds.
    /// </summary>
    [DataField]
    public float IdleCooldown = 1f;

    /// <summary>
    /// How far from the bee it looks for plants once a second, in tiles.
    /// </summary>
    [DataField]
    public float SearchRadius = 5f;

    [DataField]
    public float ArrivalThreshold = 0.5f;

    /// <summary>
    /// How long a flight to one target may take, in seconds: after that the bee gives the flower up.
    /// </summary>
    [DataField]
    public float MaxMovingTime = 10f;

    [DataField]
    public float PollinatingDuration = 2f;

    /// <summary>
    /// If the distance to the target does not shrink by <see cref="ProgressEpsilon"/> for this long, the path is
    /// planned again.
    /// </summary>
    [DataField]
    public float StuckTimeout = 3.5f;

    [DataField]
    public float ProgressEpsilon = 0.05f;

    /// <summary>
    /// How often the bee thinks, in seconds.
    /// </summary>
    [DataField]
    public float ThinkInterval = 0.5f;

    [ViewVariables]
    public float StateTimer;

    [ViewVariables]
    public float LastDistanceToTarget = float.MaxValue;

    [ViewVariables]
    public float NoProgressTime;

    /// <summary>
    /// A do-after (pollinating, depositing) is going on.
    /// </summary>
    [ViewVariables]
    public bool IsBusy;

    [ViewVariables]
    public TimeSpan NextThink;
}

/// <summary>
/// The hive. Works only while a queen is in its slot: it makes bees up to <see cref="MaxBees"/> and turns the pollen
/// they bring into honeycomb in the frames of its storage.
/// </summary>
[RegisterComponent]
public sealed partial class BeeHiveComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollenStored;

    [DataField]
    public float MaxPollen = 100f;

    [DataField]
    public float PollenPerHoney = 10f;

    /// <summary>
    /// Seconds between two turns of pollen into honeycomb.
    /// </summary>
    [DataField]
    public float ProductionInterval = 10f;

    [DataField]
    public string QueenSlotId = "queen_slot";

    [DataField]
    public int MaxBees = 5;

    /// <summary>
    /// Seconds between two new bees.
    /// </summary>
    [DataField]
    public float SpawnInterval = 30f;

    [DataField]
    public EntProtoId BeePrototype = "MobBeeWorker";

    /// <summary>
    /// How much pollen each bee brings per production interval while no player is near and the bees do not fly (the
    /// hive is then simulated, see the cvar husbandry.farm.enabled). It is paid only if there is a flowering plant
    /// within <see cref="OfflineRange"/>.
    /// </summary>
    [DataField]
    public float OfflinePollenPerBee = 5f;

    [DataField]
    public float OfflineRange = 5f;

    [ViewVariables]
    public float SpawnTimer;

    [ViewVariables]
    public float ProductionTimer;

    /// <summary>
    /// The living bees of this hive. Not saved: it is filled by the bees when they start.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Bees = new();
}

/// <summary>
/// The item that wakes a hive up when it is put into its queen slot.
/// </summary>
[RegisterComponent]
public sealed partial class QueenBeeComponent : Component;

[Serializable, NetSerializable]
public enum HiveFrameVisuals : byte
{
    HasHoneycomb,
}

[Serializable, NetSerializable]
public enum HiveFrameVisualLayers : byte
{
    Base,
}

[RegisterComponent]
public sealed partial class HiveFrameComponent : Component
{
    [DataField]
    public float HoneycombAmount;

    [DataField]
    public float MaxCapacity = 50f;

    [DataField]
    public EntProtoId Honeycomb = "MaterialHoneycomb";
}

/// <summary>
/// A plant that bees can pollinate. It is on every plant (see BasePlant), a plant that should not be visited turns
/// <see cref="IsFlowering"/> off. A visit speeds the plant up and, for a while, makes it more potent.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollinationComponent : Component
{
    [DataField]
    public bool IsFlowering = true;

    /// <summary>
    /// How much pollen a bee takes from one visit.
    /// </summary>
    [DataField]
    public float PollenYield = 10f;

    /// <summary>
    /// Growth cycles added to the age of the plant at once.
    /// </summary>
    [DataField]
    public int AgeBonus = 2;

    /// <summary>
    /// Share of its potency the plant gains while the boost lasts.
    /// </summary>
    [DataField]
    public float PotencyBonus = 0.2f;

    [DataField]
    public TimeSpan BoostDuration = TimeSpan.FromSeconds(200);

    /// <summary>
    /// How long after a visit the plant is not visited again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(230);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAvailable;

    /// <summary>
    /// When the potency boost ends. Zero: there is none.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan BoostEnd;

    /// <summary>
    /// The potency that the boost really added, so that exactly it is taken back.
    /// </summary>
    [DataField]
    public float AppliedPotencyDelta;

    [DataField]
    public string Sound = "/Audio/Effects/Fluids/splat.ogg";
}

[Serializable, NetSerializable]
public sealed partial class BeePollinatingDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HiveFrameDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Raised on a plant when a bee pollinates it.
/// </summary>
[ByRefEvent]
public readonly record struct PlantPollinatedEvent(EntityUid Bee, float Pollen);

/// <summary>
/// Raised on a hive when a bee brings pollen.
/// </summary>
[ByRefEvent]
public readonly record struct BeePollenDepositedEvent(EntityUid Bee, float Amount);
