using Content.Shared.DeadSpace.Prison;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Prison.Components;

[RegisterComponent, Access(typeof(PrisonSystem))]
public sealed partial class PrisonFactionMemberComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PrisonFactionPrototype> Faction;
}
