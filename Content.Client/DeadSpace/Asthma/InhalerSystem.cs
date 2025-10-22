using Content.Client.DeadSpace.Asthma.UI;
using Content.Client.Items;
using Content.Shared.DeadSpace.Asthma.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Client.DeadSpace.Asthma;

public sealed class InhalerSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _containerSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<InhalerComponent>(ent => new InhalerStatusControl(ent, _containerSystem));
    }
}
