// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Implants.Revive.Components;

[RegisterComponent]
public sealed partial class ReviveImplantComponent : Component
{
    [DataField]
    public float InjectingTime = 4.0f;

    [DataField]
    public float HealDuration = 4.0f;

    [DataField]
    public SoundSpecifier ImplantedSound = default!;

    [DataField]
    public int PossibleRevives = 1;

    [DataField]
    public int NumberOfDeath = 0;

    [DataField]
    public EntProtoId SpawnAfterUse = "AutosurgeonUsed";

    [DataField]
    public float ThresholdRevive = 175f;

    [DataField]
    public float ThresholdHeal = 95f;

    [DataField]
    public DamageSpecifier HealAmount = default!;
}
