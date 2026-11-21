// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed partial class LimbImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimbImmuneComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<LimbImmuneComponent> ent, ref ComponentStartup args)
    {
        RemComp<LimbBodyComponent>(ent);
        RemComp<LimbTargetComponent>(ent);
        RemComp<LimbAimComponent>(ent);
        RemComp<LimbActiveComponent>(ent);
        RemComp<LimbWoundVisualsComponent>(ent);
    }
}
