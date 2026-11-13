// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Prototypes;

namespace Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;

[RegisterComponent, NetworkedComponent]

public sealed partial class NecromorfAfterInfectionComponent : Component
{
    [DataField("necroPrototype", required: false)]
    public string? NecroPrototype { get; set; } = null;
}
