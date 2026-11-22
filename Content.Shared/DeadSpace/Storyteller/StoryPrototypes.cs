// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Storyteller;

[Prototype("storySettings")]
public sealed partial class StorySettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Width = 15;

    [DataField]
    public int Height = 10;

    [DataField]
    public int DoorY = 5;

    [DataField]
    public float WalkSpeed = 2.4f;

    [DataField]
    public float RunSpeed = 5f;

    [DataField]
    public float FadeTime = 0.6f;

    [DataField]
    public float JoinRange = 8f;

    [DataField]
    public float LeaveRange = 14f;

    [DataField]
    public float LookRange = 12f;

    [DataField]
    public int MaxActors = 8;

    [DataField]
    public int MaxProps = 80;

    [DataField]
    public float CloseUp = 1.7f;

    [DataField]
    public float MaxZoom = 2f;

    [DataField]
    public float StepX = 3f;

    [DataField]
    public float StepY = 2f;

    [DataField]
    public List<float> Spots = new() { 7.5f, 5.5f, 9.5f, 4.5f, 10.5f, 6.5f, 8.5f, 3.5f, 11.5f, 2.5f, 12.5f };

    [DataField(required: true)]
    public ProtoId<StoryKindPrototype> DefaultKind;

    [DataField(required: true)]
    public ProtoId<StoryRolePrototype> DefaultRole;

    [DataField(required: true)]
    public ProtoId<StoryLocationPrototype> DefaultLocation;

    [DataField(required: true)]
    public ProtoId<StoryLanguagePrototype> Language;

    [DataField]
    public List<ProtoId<JobPrototype>> TellerJobs = new();

    [DataField]
    public SpriteSpecifier? TellerIcon;

    [DataField]
    public float TellerIconScale = 0.75f;

    [DataField]
    public float TellerIconHeight = 0.18f;

    [DataField]
    public float TellerIconSpeed = 4f;

    [DataField]
    public float TellerIconOffset = 0.05f;

    [DataField]
    public float FootstepVolume = -9f;

    [DataField]
    public float FootstepInterval = 0.32f;

    [DataField]
    public SoundSpecifier? DoorSound;

    [DataField]
    public SoundSpecifier? PunchSound;

    [DataField]
    public SoundSpecifier? SmashSound;

    [DataField]
    public SoundSpecifier? BulletSound;

    [DataField]
    public SoundSpecifier? LaserSound;

    [DataField]
    public SoundSpecifier? HugSound;

    [DataField]
    public SoundSpecifier? FallSound;

    [DataField]
    public SoundSpecifier? GiveSound;

    [DataField]
    public SoundSpecifier? ShakeSound;

    [DataField]
    public SpriteSpecifier? PunchArc;

    [DataField]
    public SpriteSpecifier? SlashArc;

    [DataField]
    public SpriteSpecifier? Hearts;

    [DataField]
    public SpriteSpecifier? Pointer;

    [DataField]
    public SpriteSpecifier? BulletMuzzle;

    [DataField]
    public SpriteSpecifier? LaserMuzzle;

    [DataField]
    public SpriteSpecifier? BulletImpact;

    [DataField]
    public SpriteSpecifier? LaserImpact;

    [DataField]
    public Color BulletTrail = Color.FromHex("#ffeb8cd9");

    [DataField]
    public Color LaserTrail = Color.FromHex("#ff4033e6");
}

[Prototype("storyLocation")]
public sealed partial class StoryLocationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public List<string> Keywords = new();

    [DataField]
    public int Order;

    [DataField]
    public ProtoId<ContentTileDefinition>? Floor = "FloorSteel";

    [DataField]
    public Vector2i FloorFrom = new(1, 1);

    [DataField]
    public Vector2i FloorTo = new(13, 9);

    [DataField]
    public EntProtoId? Wall = "WallSolid";

    [DataField]
    public EntProtoId? Door = "AirlockGlass";

    [DataField]
    public ResPath? Parallax;

    [DataField]
    public float Darkness = 0.15f;

    [DataField]
    public Color Tint = Color.Transparent;

    [DataField]
    public List<StoryProp> Props = new();
}

[DataDefinition]
public partial struct StoryProp
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public Vector2 Pos;

    [DataField]
    public Direction Dir = Direction.South;
}

[Prototype("storyKind")]
public sealed partial class StoryKindPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public List<string> Keywords = new();

    [DataField]
    public int Order;

    [DataField]
    public ProtoId<SpeciesPrototype>? Species;

    [DataField]
    public EntProtoId? Mob;
}

[Prototype("storyRole")]
public sealed partial class StoryRolePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public List<string> Keywords = new();

    [DataField]
    public int Order;

    [DataField]
    public ProtoId<JobPrototype>? Job;

    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;
}

public enum StoryItemUse : byte
{
    None,
    Melee,
    Bullet,
    Laser,
}

[Prototype("storyItem")]
public sealed partial class StoryItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public List<string> Keywords = new();

    [DataField]
    public int Order;

    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public StoryItemUse Use;
}

[Prototype("storyEmote")]
public sealed partial class StoryEmotePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public List<string> Stems = new();

    [DataField]
    public List<string> Phrases = new();
}

[Prototype("storyLight")]
public sealed partial class StoryLightPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public float? Darkness;

    [DataField]
    public Color Pulse = Color.Transparent;

    [DataField]
    public List<string> Words = new();

    [DataField]
    public List<string> Stems = new();

    [DataField]
    public List<string> Phrases = new();
}

[Prototype("storyTint")]
public sealed partial class StoryTintPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public Color? Color;
}

[Prototype("storyPropGroup")]
public sealed partial class StoryPropGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Order;

    [DataField]
    public List<EntProtoId> Props = new();
}

public enum StoryVerb : byte
{
    None,
    Enter,
    Leave,
    Walk,
    Run,
    Fall,
    Die,
    Kneel,
    Sit,
    Lie,
    Stand,
    Point,
    Hit,
    Shoot,
    Hug,
    Kiss,
    Give,
    Take,
    Say,
    Scream,
    Emote,
    Dance,
    Turn,
    Be,
}

[Prototype("storyLanguage")]
public sealed partial class StoryLanguagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Narrator = "";

    [DataField]
    public Dictionary<StoryVerb, List<string>> Stems = new();

    [DataField]
    public Dictionary<StoryVerb, List<string>> Words = new();

    [DataField]
    public Dictionary<StoryVerb, List<string>> Phrases = new();

    [DataField]
    public List<string> PickUpStems = new();

    [DataField]
    public List<string> Connectors = new();

    [DataField]
    public List<string> Subordinators = new();

    [DataField]
    public List<string> Stopwords = new();

    [DataField]
    public List<string> FemaleWords = new();

    [DataField]
    public List<string> MaleWords = new();

    [DataField]
    public List<string> MaleNamesOnA = new();

    [DataField]
    public List<string> FemaleNameEndings = new();

    [DataField]
    public List<string> FemalePronouns = new();

    [DataField]
    public List<string> MalePronouns = new();

    [DataField]
    public List<string> OtherPronouns = new();

    [DataField]
    public List<string> GroupWords = new();

    [DataField]
    public List<string> SelfWords = new();

    [DataField]
    public List<string> SelfObjectWords = new();

    [DataField]
    public List<string> ReflexiveWords = new();

    [DataField]
    public List<string> TargetPronouns = new();

    [DataField]
    public List<string> FemaleVerbEndings = new();

    [DataField]
    public List<string> FirstPersonEndings = new();

    [DataField]
    public List<string> AdjectiveEndings = new();

    [DataField]
    public List<string> LocationPrepositions = new();

    [DataField]
    public List<string> DeathWords = new();

    [DataField]
    public List<string> BackWords = new();

    [DataField]
    public List<string> FrontWords = new();

    [DataField]
    public List<string> CameraWords = new();

    [DataField]
    public List<string> CloseUpStems = new();

    [DataField]
    public List<string> CloseUpWords = new();

    [DataField]
    public List<string> WideStems = new();

    [DataField]
    public List<string> WideWords = new();

    [DataField]
    public List<string> ShakeStems = new();

    [DataField]
    public Dictionary<StoryMove, List<string>> MoveWords = new();

    [DataField]
    public Dictionary<StoryMove, List<string>> MovePhrases = new();

    [DataField]
    public Dictionary<StoryMove, List<string>> SideWords = new();

    [DataField]
    public Dictionary<StoryMove, List<string>> SidePhrases = new();

    [DataField]
    public Dictionary<string, string> DativeEndings = new();

    [DataField]
    public Dictionary<string, string> AccusativeEndings = new();
}
