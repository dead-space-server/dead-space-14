// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Implants.Revive.Components;

[RegisterComponent]
public sealed partial class ReviveImplantComponent : Component
{
    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan WritheDuration = TimeSpan.FromSeconds(4);

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier ImplantedSound = new SoundPathSpecifier("/Audio/_DeadSpace/Autosurgeon/sound_weapons_circsawhit.ogg");

    [DataField]
    public int NumberPossibleRevive = 1;

    [DataField]
    public int NumberOfDeath = 0;

    [DataField]
    public EntProtoId AutosurgeonUsed = "AutosurgeonUsed";

    [DataField]
    public float ThresholdRevive = 175f;

    [DataField]
    public float ThresholdHeal = 95f;

    [DataField]
    public DamageSpecifier HealCount = default!;
}
