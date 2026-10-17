// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Necromorphs.PlasmaCutter;

/// <summary>
/// Makes a hitscan apply progressive dismemberment or percentage damage to necromorphs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecromorphPlasmaCutterComponent : Component
{
    [DataField]
    public float DetachedPartImpulse = 8f;
}

/// <summary>
/// Tracks progressive plasma-cutter injuries on a humanoid necromorph.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NecromorphPlasmaCutterWoundsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int RemovedLegs;

    [DataField, AutoNetworkedField]
    public float OriginalWalkSpeed;

    [DataField, AutoNetworkedField]
    public float OriginalSprintSpeed;

    [DataField, AutoNetworkedField]
    public float OriginalAcceleration;
}

/// <summary>
/// A corpse with this marker cannot be transformed or restored by necromorph infection.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecromorphMissingHeadComponent : Component;

/// <summary>
/// Tracks hits against non-humanoid necromorphs.
/// </summary>
[RegisterComponent]
public sealed partial class NecromorphPlasmaCutterDamageComponent : Component
{
    [DataField]
    public int Hits;
}

[RegisterComponent]
public sealed partial class NecromorphPlasmaCutterReducedDamageComponent : Component
{
    [DataField]
    public float FirstHitFraction = 0.25f;

    [DataField]
    public float LaterHitFraction = 0.2f;

    [DataField]
    public int HitsToKill = 5;
}

/// <summary>
/// Raised on a necromorph hit by a plasma cutter before the usual wounds: a necromorph with limbs of its own kind
/// (not body parts) can tear one off here and set <see cref="Handled"/>.
/// </summary>
[ByRefEvent]
public record struct NecromorphPlasmaCutterHitAttemptEvent(System.Numerics.Vector2 ShotDirection, float Impulse)
{
    public bool Handled;
}
