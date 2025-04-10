using Content.Shared.DeadSpace.MartialArts.SmokingCarp;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MartialArts.Arkalyse;

[RegisterComponent]
public sealed partial class ArkalyseActionComponent : Component
{
    [DataField]
    public ArkalyseList List { get; set; } = ArkalyseList.DamageAtack;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public int HitDamage;

    [DataField]
    public float ParalyzeTime;

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
public sealed partial class ArkalyseComponent : Component
{
    [DataField]
    public ArkalyseList? SelectedCombo;

    [DataField]
    public ArkalyseActionComponent? SelectedComboComp;

    public readonly List<EntProtoId> BaseArkalyse = new()
    {
        "ActionDamageArkalyseAttack",
        "ActionStunArkalyseAttack",
        "ActionMutedArkalyseAttack",
    };

    public readonly List<EntityUid> ArkalyseActionEntities = new()
    {
    };
}

public enum ArkalyseList
{
    DamageAtack,
    StunAtack,
    MuteAtack,
}
