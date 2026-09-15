using Content.Shared.Atmos.Components;
using Robust.Shared.Audio;

namespace Content.Server.Ghost.Components;

/// <summary>
/// Causes an entity to react to a ghost's "Boo!" action by extinguishing.
/// </summary>
/// <seealso cref="FlammableComponent"/>
/// <seealso cref="GhostBooEvent"/>
[RegisterComponent]
// DS14: the current GhostBooEvent keeps its legacy handled/target-count budget, so no intensity field is used.
public sealed partial class SpookyExtinguishableComponent : Component
{
    /// <summary>
    /// The likelihood that a <see cref="GhostBooEvent"/> extinguishes this entity.
    /// </summary>
    [DataField]
    public float ExtinguishChance = 0.8f;

    /// <summary>
    /// An optional sound that plays when this entity is extinguished.
    /// </summary>
    [DataField]
    public SoundSpecifier? ExtinguishSound = new SoundPathSpecifier("/Audio/Effects/quick_exhale.ogg");
}
