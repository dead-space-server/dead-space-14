// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneCasterComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RunePrototype>> Known = new()
    {
        "RuneFormProjectile",
        "RuneFormTouch",
        "RuneFormWall",
        "RuneFormSelf",
        "RuneFormUltimate",
        "RuneElementFire",
        "RuneElementLife",
        "RuneElementWater",
        "RuneElementAir",
        "RuneElementEarth",
    };

    [DataField]
    public List<ProtoId<RunePrototype>> AffinityPool = new()
    {
        "RuneElementFire",
        "RuneElementIce",
        "RuneElementEarth",
        "RuneElementLightning",
        "RuneElementLife",
        "RuneElementVoid",
        "RuneElementChaos",
        "RuneElementWater",
        "RuneElementAir",
        "RuneElementBlood",
        "RuneElementPoison",
    };

    [DataField, AutoNetworkedField]
    public ProtoId<RunePrototype>? Affinity;

    [DataField, AutoNetworkedField]
    public ProtoId<RunePrototype>? Weakness;

    [DataField, AutoNetworkedField]
    public float Strain;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan StrainChanged;

    [DataField]
    public float MaxStrain = 100f;

    [DataField]
    public float Decay = 4f;

    [DataField]
    public float DecayDelay = 3f;

    [DataField]
    public float Recognition = 0.6f;

    [DataField]
    public float ShakenRecognition = 0.7f;

    [DataField]
    public int MaxRunes = 5;

    [DataField]
    public float FatigueStep = 0.35f;

    [DataField]
    public float ScrollStrainScale = 1.5f;

    [DataField]
    public int MaxScrolls = 3;

    [DataField]
    public float InscribeTime = 6f;

    [DataField]
    public ProtoId<RuneSurgePrototype> Surge = "WizardRuneSurge";

    [DataField]
    public float FatalOverload = 1.6f;

    [DataField]
    public float EchoDelay = 1.5f;

    [DataField]
    public float EchoPower = 0.6f;

    public EntityUid? Recalled;

    [DataField]
    public ProtoId<RunePrototype> BloodRune = "RuneElementBlood";

    [DataField]
    public List<string> BloodReagents = new()
    {
        "Blood",
        "InsectBlood",
        "CopperBlood",
        "AmmoniaBlood",
        "SulfurBlood",
        "ZombieBlood",
    };

    [DataField]
    public float BloodCost = 15f;

    [DataField, AutoNetworkedField]
    public bool InBlood;

    [DataField]
    public float UltimateCooldown = 300f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan UltimateReady;

    public EntityCoordinates? Home;

    public EntityUid? PortalPending;

    public EntityUid? PortalA;

    public EntityUid? PortalB;

    [DataField]
    public EntProtoId CircleAction = "ActionWizardRuneCircle";

    [DataField, AutoNetworkedField]
    public EntityUid? CircleActionEntity;

    [DataField, AutoNetworkedField]
    public List<EntityUid> Scrolls = new();

    public ProtoId<RunePrototype>? LastElement;

    public int ElementStreak;

    [AutoNetworkedField]
    public Dictionary<string, RuneCooldown> Cooldowns = new();

    public TimeSpan NextSyllable;
}
