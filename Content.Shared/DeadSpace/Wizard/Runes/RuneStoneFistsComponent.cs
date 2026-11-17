// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneStoneFistsComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Knockback = 4.5f;

    [DataField]
    public float KnockbackSpeed = 12f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneExamineNoteComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public LocId Text;
}
