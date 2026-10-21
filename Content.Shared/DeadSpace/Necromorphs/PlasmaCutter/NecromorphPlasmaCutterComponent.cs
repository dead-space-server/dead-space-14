// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

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
/// A necromorph without a humanoid appearance that the plasma cutter still takes apart like a humanoid one:
/// the legs of its body come off one by one, then its head. Needs a body with legs and a head (the mad miner).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecromorphPlasmaCutterDismemberableComponent : Component
{
    /// <summary>Where its legs and head are drawn in its sprite, for hiding the ones cut off.</summary>
    [DataField]
    public NecromorphSpriteLayout Layout = NecromorphSpriteLayout.Humanoid;
}

/// <summary>Parts a plasma cutter took off a necromorph.</summary>
[Flags, Serializable, NetSerializable]
public enum NecromorphSeveredPart : byte
{
    None = 0,
    LegRight = 1 << 0,
    LegLeft = 1 << 1,
    Head = 1 << 2,
}

/// <summary>Which drawing of a body its sprite follows: where its legs and head are in each direction.</summary>
[Serializable, NetSerializable]
public enum NecromorphSpriteLayout : byte
{
    /// <summary>A humanoid's layers, clothes included.</summary>
    Humanoid,
    /// <summary>The mad miner's one-piece sprite.</summary>
    MadMiner,
}

/// <summary>
/// The legs and head a plasma cutter took off this necromorph. The body part layers go with the parts, but a suit or
/// a helmet keeps drawing them, so the client cuts those parts out of every layer of its sprite with a shader.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NecromorphSeveredPartsComponent : Component
{
    [DataField, AutoNetworkedField]
    public NecromorphSeveredPart Severed;

    [DataField, AutoNetworkedField]
    public NecromorphSpriteLayout Layout = NecromorphSpriteLayout.Humanoid;
}
