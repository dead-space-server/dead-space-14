// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.PlanetSurvey;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SurveyDirectorComponent : Component
{
    [ViewVariables]
    public ProtoId<SurveyWorldPrototype> World;

    [ViewVariables]
    public SurveyKind Kind;

    [ViewVariables]
    public SurveyMotive Motive;

    [ViewVariables]
    public SurveyPhase Phase;

    [ViewVariables]
    public EntityUid? Station;

    [ViewVariables]
    public bool Ready;

    [ViewVariables]
    public bool Dormant;

    [ViewVariables]
    public System.Numerics.Vector2 Centre;

    [ViewVariables]
    public System.Numerics.Vector2 Edge;

    [ViewVariables]
    public System.Numerics.Vector2 Landing;

    [ViewVariables]
    public System.Numerics.Vector2 Far;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextTick;

    [ViewVariables]
    public int Heat;

    [ViewVariables]
    public int Kills;

    [ViewVariables]
    public bool ReissueAll;

    [ViewVariables]
    public readonly List<SurveySquad> Squads = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Cargo = new();

    [ViewVariables]
    public readonly Dictionary<EntityUid, EntityUid> CargoHolders = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Shuttles = new();

    [ViewVariables]
    public EntityUid? Stash;

    [ViewVariables, AutoPausedField]
    public TimeSpan DealEnds;

    [ViewVariables]
    public bool DealPaid;

    [ViewVariables, AutoPausedField]
    public TimeSpan? BetrayAt;

    [ViewVariables]
    public readonly List<EntityUid> Stamps = new();

    [ViewVariables, AutoPausedField]
    public TimeSpan NextAlarm;

    [ViewVariables]
    public bool Storm;

    [ViewVariables]
    public bool StormWarned;

    [ViewVariables, AutoPausedField]
    public TimeSpan StormAt;

    [ViewVariables, AutoPausedField]
    public TimeSpan StormEnds;

    [ViewVariables]
    public int Storms;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextStormBite;

    [ViewVariables]
    public readonly List<SurveyPoi> Pois = new();

    [ViewVariables]
    public readonly List<SurveyObjective> Objectives = new();

    [ViewVariables]
    public string ObjectivesShown = string.Empty;

    [ViewVariables]
    public int NativesTotal;

    [ViewVariables]
    public int StampsTotal;

    [ViewVariables]
    public bool StormLoss;

    [ViewVariables]
    public float Threat = 1f;

    [ViewVariables]
    public int Waves;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextWave;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextJudge;

    [ViewVariables, AutoPausedField]
    public TimeSpan LastContact;

    [ViewVariables]
    public readonly List<SurveyOutpost> Outposts = new();

    [ViewVariables]
    public readonly List<EntityUid> Lamps = new();

    [ViewVariables]
    public System.Numerics.Vector2 BaseCentre;

    [ViewVariables]
    public float BaseRadius;

    [ViewVariables]
    public float ArenaRadius;

    [ViewVariables]
    public float Extent;

    [ViewVariables]
    public bool Blackout;

    [ViewVariables]
    public bool CommsCut;

    [ViewVariables]
    public bool Alarm;

    [ViewVariables]
    public bool AlarmEver;

    [ViewVariables, AutoPausedField]
    public TimeSpan? AlarmAt;

    [ViewVariables, AutoPausedField]
    public TimeSpan AlarmEnds;

    [ViewVariables]
    public SurveySquad? AlarmSquad;

    [ViewVariables]
    public bool Rain;

    [ViewVariables, AutoPausedField]
    public TimeSpan? QrfAt;

    [ViewVariables]
    public System.Numerics.Vector2 QrfPos;

    [ViewVariables]
    public int QrfBudget = -1;

    [ViewVariables]
    public bool CoreDown;

    [ViewVariables]
    public bool CoreSpoke;

    [ViewVariables, AutoPausedField]
    public TimeSpan? Arrived;

    [ViewVariables]
    public readonly Queue<TimeSpan> KillLog = new();

    [ViewVariables]
    public readonly Queue<TimeSpan> DownLog = new();

    [ViewVariables]
    public readonly List<System.Numerics.Vector2> Anchors = new();

    [ViewVariables]
    public readonly List<EntityUid> Scouts = new();

    [ViewVariables]
    public readonly List<EntityUid> Autodocs = new();

    [ViewVariables, AutoPausedField]
    public TimeSpan NextSquadGrenade;

    [ViewVariables]
    public readonly Queue<Robust.Shared.Maths.Vector2i> Bake = new();

    [ViewVariables]
    public readonly Dictionary<EntityUid, System.Numerics.Vector2> Trail = new();

    [ViewVariables, AutoPausedField]
    public TimeSpan NextSitrep;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextRadioChatter;

    [ViewVariables]
    public readonly Queue<(EntityUid Speaker, string Text, TimeSpan At)> RadioQueue = new();

    [ViewVariables]
    public readonly Dictionary<EntityUid, SurveyEavesdrop> Eavesdroppers = new();
}

public sealed class SurveyEavesdrop
{
    public int Strikes;
    public int Limit;
    public TimeSpan Next;
}

public enum SurveyPoiKind : byte
{
    Settlement,
    Stamp,
    Den,
    Shelter,
    Site,
    Device,
}

public sealed class SurveyPoi
{
    public System.Numerics.Vector2 Pos;
    public SurveyPoiKind Kind;
    public bool Found;
}

public sealed class SurveyObjective
{
    public string Id = string.Empty;
    public LocId Text;
    public int Done;
    public int Goal = 1;
    public bool Shown;
    public bool Complete;
    public int Money;
    public int Personal;
}

public enum SurveyRelicEffect : byte
{
    Inert,
    Regen,
    Bloom,
    Hatch,
    Idol,
}

public enum SurveyDeviceRole : byte
{
    Power,
    Comms,
}

public sealed class SurveyOutpost
{
    public System.Numerics.Vector2 Pos;
    public SurveyDeviceRole? Role;
    public SurveySquad? Squad;
    public bool Taken;
}

[RegisterComponent]
public sealed partial class SurveyDeviceComponent : Component
{
    [DataField]
    public SurveyDeviceRole Role;

    [DataField]
    public float Time = 10f;

    [ViewVariables]
    public bool Broken;
}

[RegisterComponent]
public sealed partial class SurveyBaseLampComponent : Component;

[RegisterComponent]
public sealed partial class SurveyFactoryCoreComponent : Component;

[RegisterComponent]
public sealed partial class SurveyNativeDoorComponent : Component;

[RegisterComponent]
public sealed partial class SurveyNativeMineComponent : Component;

[RegisterComponent]
public sealed partial class SurveyNativeWeaponComponent : Component
{
    [DataField]
    public EntProtoId? Drop;

    [DataField]
    public float DropChance;
}

[RegisterComponent]
public sealed partial class SurveyResearchSiteComponent : Component
{
    [DataField]
    public int Points = 3000;

    [DataField]
    public float Time = 12f;

    [DataField]
    public bool Den;

    [ViewVariables]
    public bool Studied;

    [ViewVariables]
    public EntityUid? Planet;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SurveyRelicComponent : Component
{
    [DataField]
    public SurveyRelicEffect Effect;

    [DataField]
    public int Charges = 3;

    [DataField]
    public EntProtoId? Spawn;

    [DataField]
    public float HatchMinutes = 12f;

    [ViewVariables, AutoPausedField]
    public TimeSpan? HatchAt;

    [ViewVariables, AutoPausedField]
    public TimeSpan Next;

    [ViewVariables]
    public int Whispers;
}

public sealed class SurveySquad
{
    public readonly List<EntityUid> Members = new();
    public readonly HashSet<EntityUid> Wounded = new();
    public EntityCoordinates Home;
    public EntityUid? Target;
    public bool Ambush;
    public bool Engaged;
    public bool Withdrawn;
    public bool Armed;
    public System.Numerics.Vector2 LastPos;
    public System.Numerics.Vector2? LastKnown;
    public TimeSpan NextOrders;
    public int Alive;
    public int Size;
    public TimeSpan LastSeen;
    public TimeSpan NextChatter;
    public float Health = 1f;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SurveyMemberComponent : Component
{
    [ViewVariables]
    public EntityUid Planet;

    [ViewVariables]
    public int Squad = -1;

    [DataField]
    public bool Elite;

    [DataField]
    public bool Robot;

    [DataField]
    public bool Prey;

    [DataField]
    public bool Native = true;

    [DataField]
    public string Voice = string.Empty;

    [DataField]
    public float Sight = 12f;

    [ViewVariables]
    public bool Blind;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextCall;

    [ViewVariables]
    public float LastHealth = 1f;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextShift;

    [DataField]
    public bool Medic;

    [DataField]
    public bool Scout;

    [DataField]
    public List<EntProtoId> Grenades = new();

    [DataField]
    public EntProtoId? Sidearm;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextGrenade;

    [ViewVariables]
    public EntityUid? Patient;

    [ViewVariables]
    public bool Dragging;

    [ViewVariables]
    public System.Numerics.Vector2? Haven;

    [ViewVariables]
    public int Waypoint;

    [ViewVariables]
    public EntityUid? Spotted;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextReport;

    [DataField]
    public int Medkits;

    [DataField]
    public List<string> Stims = new();

    [ViewVariables, AutoPausedField]
    public TimeSpan? SelfHealAt;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextSelfCare;

    [ViewVariables, AutoPausedField]
    public TimeSpan ToughUntil;

    [ViewVariables, AutoPausedField]
    public TimeSpan DodgeUntil;

    [ViewVariables]
    public EntityUid? Dodged;

    [ViewVariables]
    public bool Approach;

    [ViewVariables]
    public int BlockedTicks;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextReplan;

    [ViewVariables]
    public bool ReplanPending;

    [ViewVariables]
    public System.Numerics.Vector2? DodgeTo;

    [ViewVariables, AutoPausedField]
    public TimeSpan DodgeRush;

    [ViewVariables]
    public bool Rebuilt;

    [ViewVariables]
    public int RepairProgress;

    [ViewVariables, AutoPausedField]
    public TimeSpan? DisarmedAt;

    [ViewVariables]
    public System.Numerics.Vector2? StuckPos;

    [ViewVariables, AutoPausedField]
    public TimeSpan StuckSince;

    [ViewVariables, AutoPausedField]
    public TimeSpan CoverUntil;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextCover;
}

[RegisterComponent]
public sealed partial class SurveyAutodocComponent : Component
{
    [DataField]
    public float Heal = 4f;

    [DataField]
    public float Range = 1.6f;

    [DataField]
    public bool Robots;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SurveyCargoComponent : Component
{
    [DataField]
    public LocId Need = "survey-need-science";

    [DataField]
    public List<EntProtoId> Payload = new();

    [DataField]
    public int Money = 2000;

    [ViewVariables]
    public EntityUid? Planet;

    [ViewVariables]
    public SurveyKind Kind;

    [ViewVariables]
    public bool Stolen;

    [ViewVariables]
    public bool Departed;

    [ViewVariables]
    public SurveyMotive Outcome;

    [ViewVariables]
    public int Heat;

    [ViewVariables]
    public bool Resolved;

    [ViewVariables, AutoPausedField]
    public TimeSpan? CurseAt;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SurveyStampComponent : Component
{
    [DataField]
    public EntProtoId? Crowd;

    [DataField]
    public List<EntProtoId> Pool = new();

    [DataField]
    public int Cap = 4;

    [DataField]
    public float Interval = 12f;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextPrint;

    [ViewVariables]
    public readonly HashSet<EntityUid> Printed = new();
}
