using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Halloween;

/// <summary>
/// Effects that can be applied to the player after eating a Halloween candy.
/// </summary>
[Serializable, NetSerializable]
public enum CandyEffectKind : byte
{
    GoodAftertaste,
    PerfectAftertaste,
    ChocolateCoin,
    Cinnamon,
    Duplicate,
    Glowstick,
    MoneyWalk,
    CornSticks,
    Regurgitation,
    ToothBreaker,
    ForAmateurs,
    Fragility,
    YourElement,
    Nothing,
    Nausea,
    Oops,
    CrispyToffee,
    ControlInvert,
    TooSweet,
    Earplugs,
    Butterfingers,
    Jinx,
    ImAdequate,
    VulpEater,
}

/// <summary>
/// Sent to the player who ate a candy so their client can show the effect name at the top of the screen with a sound.
/// </summary>
[Serializable, NetSerializable]
public sealed class CandyAnnounceEvent(CandyEffectKind effect) : EntityEventArgs
{
    public CandyEffectKind Effect = effect;
}

/// <summary>
/// Sent to make the client's screen slightly green for the given duration.
/// </summary>
[Serializable, NetSerializable]
public sealed class CandyScreenTintEvent(TimeSpan duration) : EntityEventArgs
{
    public TimeSpan Duration = duration;
}

/// <summary>
/// Sent to mute all client sounds for the given duration.
/// </summary>
[Serializable, NetSerializable]
public sealed class CandyMuteEvent(TimeSpan duration) : EntityEventArgs
{
    public TimeSpan Duration = duration;
}

/// <summary>
/// Marks a candy that grants a random effect when eaten.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CandyComponent : Component
{
    /// <summary>
    /// Force a specific effect for this candy prototype. If null, the effect is random on eat.
    /// </summary>
    [DataField]
    public CandyEffectKind? Effect;
}

/// <summary>
/// Marks a player currently green from the "YourElement" candy effect.
/// The client visualizer tints the entity and everything on it. Applied permanently.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CandyGreenComponent : Component;

/// <summary>
/// Permanent additive movement speed bonus granted by candies (Cinnamon, Fragility).
/// </summary>
[RegisterComponent]
public sealed partial class CandySpeedBoostComponent : Component
{
    [DataField]
    public float Bonus;
}

/// <summary>
/// Temporary movement speed multiplier granted by a candy, removed at EndTime.
/// </summary>
[RegisterComponent]
public sealed partial class CandyTimedSpeedComponent : Component
{
    [DataField]
    public float Modifier = 1f;

    [DataField]
    public TimeSpan EndTime;
}

/// <summary>
/// Marks a player that leaves one credit behind them while walking (MoneyWalk effect).
/// </summary>
[RegisterComponent]
public sealed partial class CandyMoneyTrailComponent : Component
{
    [DataField]
    public TimeSpan EndTime;

    [DataField]
    public TimeSpan NextDrop;
}
/// <summary>
/// Marks a player whose movement controls are inverted (CandyControlInvert effect).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CandyInvertControlsComponent : Component;
