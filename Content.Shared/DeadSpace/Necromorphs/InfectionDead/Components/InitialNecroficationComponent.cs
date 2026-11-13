// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Prototypes;

namespace Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;

[RegisterComponent]
public sealed partial class InitialNecroficationComponent : Component
{

    [DataField("necroPrototype", required: false)]
    public string? NecroPrototype { get; set; } = null;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan StartTick;

    public InitialNecroficationComponent(InfectionDeadStrainData sd)
    {
        StrainData = sd;
    }

    [DataField]
    public InfectionDeadStrainData StrainData = new InfectionDeadStrainData();
}

[ByRefEvent]
public readonly record struct InitialNecroficationEvent();
