// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// A fighter of the Circle squad: the cardinal's screams act on them as on allies.
/// </summary>
[RegisterComponent]
public sealed partial class CircleSquadComponent : Component;

/// <summary>
/// The cardinal's two screams. The red one rallies the squad nearby, the green one lets them see through walls and
/// marks every outsider on their screens. Both share one cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalScreamComponent : Component
{
    /// <summary>
    /// The one action: it opens the choice between the two screams.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionCardinalScream";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Until when a pick from the scream menu is accepted. Set only by using the action on the server.
    /// </summary>
    [ViewVariables]
    public TimeSpan? ChoiceExpires;

    [DataField, AutoNetworkedField]
    public TimeSpan NextScream;

    /// <summary>
    /// Fighters this close to the cardinal are caught by a scream, and lamps this close blink.
    /// </summary>
    [DataField]
    public float Radius = 7f;

    /// <summary>
    /// Shared by both screams.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan RallyDuration = TimeSpan.FromSeconds(7);

    [DataField]
    public float SpeedModifier = 1.1f;

    /// <summary>
    /// Added to the fire rate of any gun a rallied fighter holds.
    /// </summary>
    [DataField]
    public float FireRateBonus = 0.5f;

    /// <summary>
    /// Share of a bola's slowdown that a rallied fighter shrugs off.
    /// </summary>
    [DataField]
    public float EnsnareRelief = 0.1f;

    [DataField]
    public TimeSpan HuntDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Half the size of a player's screen in tiles: outsiders within it of the cardinal or of a fighter caught by
    /// the green scream are marked.
    /// </summary>
    [DataField]
    public Vector2 ScreenHalfExtents = new(10.5f, 7.5f);

    [DataField]
    public EntProtoId RallyWave = "EffectCardinalRallyScream";

    [DataField]
    public EntProtoId HuntWave = "EffectCardinalHuntScream";

    [DataField]
    public SoundSpecifier RallySound = new SoundPathSpecifier("/Audio/Effects/Screech/changeling_screech_strong.ogg",
        AudioParams.Default.WithVolume(2f).WithPitchScale(0.85f).WithMaxDistance(14f));

    [DataField]
    public SoundSpecifier HuntSound = new SoundPathSpecifier("/Audio/Effects/Screech/changeling_screech_strong.ogg",
        AudioParams.Default.WithVolume(2f).WithPitchScale(1.1f).WithMaxDistance(14f));

    [DataField]
    public TimeSpan LightBlink = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float ShakeStrength = 1.2f;
}

/// <summary>
/// The red scream on a fighter: faster on their feet, faster on the trigger, less held back by bolas.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalRallyComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.1f;

    [DataField, AutoNetworkedField]
    public float FireRateBonus = 0.5f;

    [DataField, AutoNetworkedField]
    public float EnsnareRelief = 0.1f;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CardinalRally";

    /// <summary>
    /// Guns this fighter holds that fire faster for it.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> BoostedGuns = new();
}

/// <summary>
/// A gun held by a rallied fighter.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalRallyGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FireRateBonus = 0.5f;
}

/// <summary>
/// The green scream on a fighter: sees living bodies through walls and every marked outsider, wherever they run.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalHuntComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CardinalHunt";

    /// <summary>
    /// Where the marked outsiders are, sent by the server so the arrows do not depend on the hunter receiving them.
    /// </summary>
    [AutoNetworkedField]
    public List<Vector2> Targets = new();

    [AutoNetworkedField]
    public NetEntity TargetsMap;
}

/// <summary>
/// An outsider caught on screen by the green scream: the hunting fighters see them anywhere.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalMarkedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CardinalMarked";
}

/// <summary>
/// The rings of a scream rolling out from the cardinal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardinalScreamWaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;

    [DataField, AutoNetworkedField]
    public float Radius = 7f;

    [DataField, AutoNetworkedField]
    public float Duration = 1.4f;

    [AutoNetworkedField]
    public TimeSpan InitTime;
}

public sealed partial class CardinalScreamActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum CardinalScreamKind : byte
{
    Rally,
    Hunt,
}

/// <summary>
/// The cardinal's pick from the scream menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class CardinalScreamChoiceEvent(NetEntity cardinal, CardinalScreamKind kind) : EntityEventArgs
{
    public NetEntity Cardinal = cardinal;
    public CardinalScreamKind Kind = kind;
}
