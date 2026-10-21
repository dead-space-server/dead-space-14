// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// A necropolis gate (tgstation): a massive stone door, opened and closed by hand.
/// The Legion gate is knocked on first, then opens for good.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandNecropolisGateComponent : Component
{
    /// <summary>Cannot be opened or closed by hand.</summary>
    [DataField]
    public bool Locked;

    /// <summary>Legion gate: knocking takes <see cref="KnockTime"/>, then the gate opens and stays open.</summary>
    [DataField]
    public bool LegionGate;

    [DataField]
    public TimeSpan KnockTime = TimeSpan.FromSeconds(5);

    /// <summary>A sleeping Legion this close wakes up when the Legion gate is fully open.</summary>
    [DataField]
    public float WakeRange = 40f;

    /// <summary>Who knocked on the Legion gate: the Legion lunges at them.</summary>
    [ViewVariables]
    public EntityUid? Knocker;

    /// <summary>The door grinds open (the opening animation).</summary>
    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(3.7);

    /// <summary>The door slams shut (the closing animation).</summary>
    [DataField]
    public TimeSpan CloseTime = TimeSpan.FromSeconds(0.56);

    /// <summary>When the door starts blocking the way while closing.</summary>
    [DataField]
    public TimeSpan CloseBlockDelay = TimeSpan.FromSeconds(0.2);

    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/Effects/stonedoor_openclose.ogg", AudioParams.Default.WithPitchScale(0.6f));

    [DataField]
    public SoundSpecifier? CloseSound = new SoundPathSpecifier("/Audio/Effects/stonedoor_openclose.ogg", AudioParams.Default.WithPitchScale(1.4f));

    [DataField]
    public SoundSpecifier? KnockSound = new SoundPathSpecifier("/Audio/_DeadSpace/Weapons/Melee/Shield/shieldbash.ogg");

    /// <summary>Heard by everyone on the map when the Legion gate opens.</summary>
    [DataField]
    public SoundSpecifier? ReleaseSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/tendril_destroyed.ogg");

    [ViewVariables]
    public LavalandNecropolisGateState State = LavalandNecropolisGateState.Closed;

    [ViewVariables]
    public TimeSpan? StageEnd;

    [ViewVariables]
    public TimeSpan? BlockAt;

    [ViewVariables]
    public TimeSpan? KnockEnd;
}

/// <summary>
/// Big structures drawn in two parts (tgstation gate and arch): this entity is the back, the front part is spawned
/// on the same spot and drawn over mobs.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandStructureFrontComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Front;

    [ViewVariables]
    public EntityUid? Spawned;
}
