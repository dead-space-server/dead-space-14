using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chasm;

/// <summary>
///     Marks a component that will cause entities to fall into them on a step trigger activation
/// </summary>
[NetworkedComponent, RegisterComponent, Access(typeof(ChasmSystem))]
public sealed partial class ChasmComponent : Component
{
    /// <summary>
    ///     Sound that should be played when an entity falls into the chasm
    /// </summary>
    [DataField("fallingSound")]
    public SoundSpecifier FallingSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");

    /// <summary>
    /// Optional emote that should play when an entity falls into the chasm.
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype>? Emote = "Scream";

    // DS14-start
    /// <summary>
    ///     Someone hovering over the chasm in anti-gravity clothing who drops into pre-crit or crit gets this long to
    ///     reach solid ground or get back up before falling in.
    /// </summary>
    [DataField]
    public TimeSpan CritHoverGrace = TimeSpan.FromSeconds(10);
    // DS14-end
}
