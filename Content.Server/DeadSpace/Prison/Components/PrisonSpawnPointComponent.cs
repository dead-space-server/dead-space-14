using Content.Shared.DeadSpace.Prison;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Prison.Components;

[RegisterComponent, Access(typeof(PrisonSystem), typeof(PrisonMapSystem))]
public sealed partial class PrisonSpawnPointComponent : Component
{
    [DataField]
    public ProtoId<PrisonFactionPrototype>? Faction;
}
