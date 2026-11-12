using Content.Shared.RoundEnd;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(ZombieRuleSystem))]
public sealed partial class ZombieRuleComponent : Component
{
    /// <summary>
    /// When the round will next check for round end.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextRoundEndCheck;

    /// <summary>
    /// The amount of time between each check for the end of the round.
    /// </summary>
    [DataField]
    public TimeSpan EndCheckDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// After this amount of the crew become zombies, the shuttle will be automatically called.
    /// </summary>
    [DataField]
    public float ZombieShuttleCallPercentage = 0.7f;
<<<<<<< ours

    // DS14-start
    /// <summary>
    /// Whether the automatic zombie shuttle call threshold has already been handled.
    /// </summary>
    public bool ZombieShuttleAutoCallHandled;

    /// <summary>
    /// Whether automatic zombie shuttle calling was disabled because round admins were online.
    /// </summary>
    public bool ZombieShuttleAutoCallDisabled;
    // DS14-end
||||||| base
=======

    /// <summary>
    /// What will happen if zombies get more than 80%
    /// </summary>
    [DataField]
    public RoundEndBehavior ZombieRoundEndBehavior = RoundEndBehavior.ShuttleCall;

    /// <summary>
    /// Shuttle timer for when shuttle is called
    /// </summary>
    [DataField]
    public TimeSpan ZombieEvacShuttleTime = TimeSpan.FromMinutes(5);
>>>>>>> theirs
}
