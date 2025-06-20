using Content.Server.DeadSpace.MartialArts;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MartialArts.SmokingCarp;

[RegisterComponent]
public sealed partial class SmokingCarpActionComponent : Component
{
    [DataField]
    public SmokingCarpList List { get; set; } = SmokingCarpList.PowerPunch;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public List<LocId> PackMessageOnHit = [];

    [DataField]
    public float PushStrength;

    [DataField]
    public int HitDamage;

    [DataField]
    public float ParalyzeTime;

    [DataField]
    public float MaxPushDistance;

    [DataField]
    public bool IgnoreResist = true;

    [DataField]
    public string DamageType = "Slash";

    [DataField]
    public EntProtoId? EffectPunch;

    [DataField]
    public SoundSpecifier? HitSound;
}

[RegisterComponent]
public sealed partial class SmokingCarpTripPunchComponent : Component
{
    [DataField]
    public EntProtoId? SelfEffect = "EffectTripPunchCarp";

    [DataField]
    public SoundSpecifier? TripSound = new SoundPathSpecifier("/Audio/_DeadSpace/SmokingCarp/sound_items_weapons_slam.ogg");

    [DataField]
    public float Range = 1.0f;

    [DataField]
    public float ParalyzeTime = 1.2f;
}

[RegisterComponent]
public sealed partial class SmokingCarpComponent : Component
{
    [DataField]
    public SmokingCarpList? SelectedCombo;

    [DataField]
    public SmokingCarpActionComponent? SelectedComboComp;

    public readonly List<EntProtoId> BaseSmokingCarp = new()
    {
        "ActionPowerPunchCarp",
        "ActionSmokePunchCarp",
        "ActionTripPunchCarp",
        "ActionReflectCarp",
    };

    public readonly List<EntityUid> SmokeCarpActionEntities = new()
    {
    };

    [DataField]
    public MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.SmokingCarp;
}

public enum SmokingCarpList
{
    PowerPunch,
    SmokePunch,
}
