// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MartialArts.CQC.Components;

[RegisterComponent]
public sealed partial class CQCStepPunchComponent : Component
{
    [DataField]
    public EntProtoId? SelfEffect = "EffectTripPunchCarp";

    [DataField]
    public SoundSpecifier? StepSound = new SoundPathSpecifier("/Audio/_DeadSpace/SmokingCarp/sound_items_weapons_slam.ogg");

    [DataField]
    public float Range = 1.0f;

    [DataField]
    public float ParalyzeTime = 1.2f;
}

[RegisterComponent]
public sealed partial class CQCComponent : Component
{
    [DataField]
    public CQCList? SelectedCombo; // Выбранное комбо, которое меняется при вызове события

    public readonly List<EntProtoId> BaseCQC = new() // Список всех Action, которые будут выдаваться пользователю при концентрации
    {
        "ActionPowerPunchCQCAttack",
        "ActionMutedCQCAttack",
        "ActionRelaxCQC",
        "ActionCQCStepPunch",
    };

    [DataField]
    public List<EntityUid> BaseCQCActionEntities = new();

    [DataField]
    public EntProtoId CQCConcentrationAction = "ActionConcentrationCQC";

    [DataField]
    public EntityUid? CQCConcentrationActionEntity;

    [DataField]
    public bool Concentrated;

    [DataField]
    public CQCParams Params; // Передача всех переменных и хранение всех переменных, хранится в MartialArtsTrainingComponent
}

public enum CQCList
{
    PowerPunch,
    MuteAttack,
}
