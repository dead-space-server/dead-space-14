// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MartialArts.Arkalyse.Components;

[RegisterComponent]
public sealed partial class ArkalyseComponent : Component
{
    [DataField]
    public ArkalyseParams Params; // Передача всех переменных и хранение всех переменных, хранится в MartialArtsTrainingComponent

    [DataField]
    public ArkalyseList? SelectedCombo; // Выбранное комбо, которое меняется при вызове события

    public readonly List<EntProtoId> BaseArkalyse = new() // Список всех Action, которые будут выдаваться пользователю
    {
        "ActionDamageArkalyseAttack",
        "ActionStunArkalyseAttack",
        "ActionMutedArkalyseAttack",
        "ActionRelaxArkalyseAttack",
    };

    public bool LearnedFromManual;
}

public enum ArkalyseList
{
    DamageAttack,
    StunAttack,
    MuteAttack,
    RelaxHand,
}
