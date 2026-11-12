using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Used for marking regular revs as well as storing icon prototypes so you can see fellow revs.
/// </summary>
<<<<<<< ours
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRevolutionarySystem))] // DS14: networked for replay state only.
||||||| base
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
=======
[RegisterComponent, NetworkedComponent, Access(typeof(RevolutionarySystem))]
>>>>>>> theirs
public sealed partial class RevolutionaryComponent : Component
{
    /// <summary>
    /// The status icon prototype displayed for revolutionaries
    /// </summary>
<<<<<<< ours
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)] // DS14: configure through YAML; live roster updates are event-driven.
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "RevolutionaryFaction";
||||||| base
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "RevolutionaryFaction";
=======
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "RevolutionaryFaction";
>>>>>>> theirs

    /// <summary>
    /// Sound that plays when you are chosen as Rev. (Placeholder until I find something cool I guess)
    /// </summary>
    [DataField]
    public SoundSpecifier RevStartSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    /// <summary>
    /// The duration of the stun when you get deconverted.
    /// </summary>
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4f);

    public override bool SessionSpecific => true;
}
