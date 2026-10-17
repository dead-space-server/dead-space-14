// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Lavaland.ChromiteCaves;

public enum LavalandPitJumperStage : byte
{
    /// <summary>Standing about, looking out for somebody.</summary>
    Idle,
    /// <summary>Spotted somebody: the exclamation mark and the whistle.</summary>
    Alert,
    /// <summary>Jumping into the nearest chasm.</summary>
    Diving,
    /// <summary>Somewhere down below.</summary>
    Hidden,
}

/// <summary>
/// A chromite caves creature (the argocytes) that hunts through the chasms: when it is not fighting and spots a player,
/// it whistles, dives into a chasm and a few seconds later jumps out next to him. If he got far enough meanwhile that
/// the spot is off his screen, it comes back out where it dived and stands there again.
/// The more a player dances it around the chasms, the shorter its trip below gets - but never under a second,
/// so it never simply teleports to him.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandPitJumperComponent : Component
{
    [DataField]
    public float NoticeRange = 8f;

    /// <summary>Only a chasm this close is used to dive.</summary>
    [DataField]
    public float ChasmRange = 6f;

    [DataField]
    public TimeSpan AlertTime = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan DiveTime = TimeSpan.FromSeconds(0.9);

    [DataField]
    public TimeSpan HiddenTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan EmergeTime = TimeSpan.FromSeconds(1.2);

    /// <summary>Between two dives.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Out of a chasm by the player: this soon it may dive again through the next chasm on its way to him.
    /// Long enough to reach him and bite first, so it does not keep diving the moment it is out.
    /// </summary>
    [DataField]
    public TimeSpan HuntDiveCooldown = TimeSpan.FromSeconds(3);

    /// <summary>Every dive in a row at the same player is this much shorter below.</summary>
    [DataField]
    public TimeSpan HiddenTimeReduction = TimeSpan.FromSeconds(1.5);

    /// <summary>However often it dives, it stays below at least this long.</summary>
    [DataField]
    public TimeSpan MinHiddenTime = TimeSpan.FromSeconds(1);

    /// <summary>Dives count as in a row while no longer than this apart.</summary>
    [DataField]
    public TimeSpan StreakWindow = TimeSpan.FromSeconds(30);

    /// <summary>Half size of a player's screen in tiles: off it the player has run away.</summary>
    [DataField]
    public Vector2 ViewHalfSize = new(11f, 8f);

    /// <summary>It jumps out of a chasm this close to the player - a random one of them, to catch him off guard.</summary>
    [DataField]
    public float EmergePitRange = 7f;

    [DataField]
    public SoundSpecifier? WhistleSound = new SoundCollectionSpecifier("BaseWhistle");

    [DataField]
    public SoundSpecifier? DiveSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [ViewVariables]
    public LavalandPitJumperStage Stage;

    [ViewVariables]
    public TimeSpan StageEnd;

    [ViewVariables]
    public TimeSpan NextDive;

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>Dives in a row at <see cref="StreakTarget"/>, and when the last one ended.</summary>
    [ViewVariables]
    public int Streak;

    [ViewVariables]
    public EntityUid? StreakTarget;

    [ViewVariables]
    public TimeSpan LastEmerge;

    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Fighting with its own AI; otherwise it only wanders about, waiting to spot somebody.</summary>
    [ViewVariables]
    public bool Hunting;

    /// <summary>The player it runs at while hunting (it chases by itself, not by its pathfinding AI).</summary>
    [ViewVariables]
    public EntityUid? Prey;

    /// <summary>Ambling about while waiting: where, until when, and when the next stroll begins.</summary>
    [ViewVariables]
    public Vector2 StrollDirection;

    [ViewVariables]
    public TimeSpan StrollUntil;

    [ViewVariables]
    public TimeSpan NextStroll;

    /// <summary>Close enough to bite instead of running.</summary>
    [DataField]
    public float BiteRange = 1.3f;

    /// <summary>Last time a player was near while hunting: after a while alone it stands and waits again.</summary>
    [ViewVariables]
    public TimeSpan LastPlayerNear;

    /// <summary>Hunting stops after this long without a player near.</summary>
    [DataField]
    public TimeSpan CalmDownTime = TimeSpan.FromSeconds(8);

    /// <summary>Where it dived from, and the chasm it dived into.</summary>
    [ViewVariables]
    public MapCoordinates Home;

    [ViewVariables]
    public Vector2 ChasmPosition;
}
