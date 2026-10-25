// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Interaction.Events;

namespace Content.Shared.DeadSpace.Surgery;

public sealed class SurgeryXraySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryXrayComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(Entity<SurgeryXrayComponent> ent, ref UseInHandEvent args)
    {
        ent.Comp.On = !ent.Comp.On;
        Dirty(ent);
        args.Handled = true;
    }
}
